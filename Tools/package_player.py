"""Package a staged GO GHOST desktop player, including its controls and licenses."""

import argparse
from contextlib import ExitStack
import os
from pathlib import Path
import stat
from uuid import uuid4
import zipfile


MACH_O_HEADERS = {bytes.fromhex(value) for value in (
    "cafebabe", "cafebabf", "cffaedfe", "feedfacf", "cefaedfe", "feedface",
)}


def package_player(staging, output):
    staging, output = staging.resolve(), output.resolve()
    if not staging.is_dir() or output.is_relative_to(staging) or output.suffix.lower() != ".zip":
        raise ValueError("Choose a staging directory and an output ZIP outside it.")
    if not (staging / "PLAYTEST-README.txt").is_file() or not (staging / "Licenses").is_dir():
        raise ValueError("Stage PLAYTEST-README.txt and Licenses alongside the player.")

    with ExitStack() as cleanup:
        archive = output.with_name(f".{output.name}.{uuid4().hex}.tmp")
        with zipfile.ZipFile(archive, "x", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
            cleanup.callback(archive.unlink, missing_ok=True)
            for path in sorted(staging.rglob("*")):
                relative = path.relative_to(staging)
                if any("DontShip" in part or "DoNotShip" in part for part in relative.parts):
                    continue
                name = (Path(output.stem) / relative).as_posix()
                if path.is_symlink():
                    link = zipfile.ZipInfo(name)
                    link.create_system = 3
                    link.external_attr = (stat.S_IFLNK | 0o777) << 16
                    z.writestr(link, os.readlink(path).replace(os.sep, "/").encode("utf-8"))
                elif path.is_file():
                    with path.open("rb") as stream:
                        native = stream.read(4) in MACH_O_HEADERS
                    executable = native or path.suffix.lower() in {".exe", ".dll"} or path.stat().st_mode & 0o111
                    z.write(path, name)
                    info = z.getinfo(name)
                    info.create_system = 3
                    info.external_attr = (stat.S_IFREG | (0o755 if executable else 0o644)) << 16

        with zipfile.ZipFile(archive) as z:
            damaged = z.testzip()
            if damaged:
                raise ValueError(f"ZIP verification failed for {damaged}")
            entries = z.namelist()
            prefix = output.stem + "/"
            names = {name[len(prefix):] for name in entries if name.startswith(prefix)}
            players = [exe for exe in staging.glob("*.exe")
                       if (staging / (exe.stem + "_Data")).is_dir()]
            apps = list(staging.glob("*.app"))
            if len(players) == 1 and not apps:
                exe = players[0]
                data = exe.stem + "_Data"
                required = {exe.name, "UnityPlayer.dll", data + "/globalgamemanagers"}
                if "GameAssembly.dll" in names:
                    required |= {"GameAssembly.dll", data + "/il2cpp_data/Metadata/global-metadata.dat"}
                else:
                    required |= {data + "/Managed/Assembly-CSharp.dll",
                                 "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll"}
            elif len(apps) == 1 and not players:
                app = apps[0].name + "/Contents/"
                if not any(name.startswith(app + "MacOS/") for name in names):
                    raise ValueError("Missing macOS player executable.")
                required = {app + "Resources/Data/globalgamemanagers"}
                if (app + "Resources/Data/Managed/Assembly-CSharp.dll" not in names
                        and app + "Resources/Data/il2cpp_data/Metadata/global-metadata.dat" not in names):
                    raise ValueError("Missing macOS game assemblies or IL2CPP metadata.")
            else:
                raise ValueError("Expected exactly one desktop player.")
            if required - names:
                raise ValueError(f"Missing player files: {sorted(required - names)}")
        archive.replace(output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("staging", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    package_player(args.staging, args.output)
