"""Focused regressions for the desktop package contract, using tiny synthetic players."""

from pathlib import Path
import tempfile
import unittest
import zipfile

from package_player import package_player


class PackagePlayerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.stage = self.root / "Player"
        self.output = self.root / "GO-GHOST-Windows.zip"
        self.files = (
            "GO-GHOST.exe", "UnityPlayer.dll", "GO-GHOST_Data/globalgamemanagers",
            "GO-GHOST_Data/boot.config", "GO-GHOST_Data/Managed/Assembly-CSharp.dll",
            "MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll",
            "PLAYTEST-README.txt", "Licenses/Fonts/OFL.txt",
        )
        for name in self.files:
            self.write(name, name.encode("utf-8"))

    def write(self, name, content):
        path = self.stage / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)

    def test_recorded_voice_player_does_not_require_removed_synthesis(self):
        self.write("GO-GHOST_BackUpThisFolder_ButDontShipItWithYourGame/debug.txt", b"private")
        package_player(self.stage, self.output)
        with zipfile.ZipFile(self.output) as archive:
            names = archive.namelist()
            self.assertTrue(any(name.endswith("Assembly-CSharp.dll") for name in names))
            self.assertFalse(any("DontShip" in name for name in names))
            self.assertFalse(any(name.endswith("model.int8.onnx") for name in names))

    def test_missing_runtime_keeps_previous_package(self):
        self.output.write_bytes(b"previous valid package")
        (self.stage / "UnityPlayer.dll").unlink()
        with self.assertRaises(ValueError):
            package_player(self.stage, self.output)
        self.assertEqual(b"previous valid package", self.output.read_bytes())

    def test_missing_game_assembly_is_rejected(self):
        (self.stage / "GO-GHOST_Data/Managed/Assembly-CSharp.dll").unlink()
        with self.assertRaises(ValueError):
            package_player(self.stage, self.output)
        self.assertFalse(self.output.exists())

if __name__ == "__main__":
    unittest.main()
