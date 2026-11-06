"""Install Voice Studio's editor-only environment into the project's ignored Library."""

import os
import subprocess
import sys
import venv
from pathlib import Path

if __name__ == "__main__":
    if sys.version_info[:2] != (3, 12):
        raise SystemExit("Select Python 3.12 (64-bit) to install the audio environment.")
    root = Path(__file__).resolve().parents[2]
    dest = root / "Library/VoiceStudio/venv"
    venv.EnvBuilder(with_pip=True).create(dest)
    exe = dest / ("Scripts/python.exe" if os.name == "nt" else "bin/python")
    subprocess.run(
        [
            str(exe),
            "-m",
            "pip",
            "install",
            "--disable-pip-version-check",
            "--only-binary=:all:",
            "numpy>=1.26,<3",
            "praat-parselmouth==0.4.7",
            "imageio-ffmpeg==0.6.0",
            "sherpa-onnx==2.0.1",
        ],
        check=True,
    )
    subprocess.run([str(exe), "-c", "import numpy, parselmouth, imageio_ffmpeg, sherpa_onnx"], check=True)
