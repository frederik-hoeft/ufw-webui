#!/usr/bin/env python3
"""Generate the checked-in IBM Plex WOFF2 assets from the approved source archive."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import tempfile
import zipfile

try:
    from fontTools.ttLib import woff2
except ImportError as exception:
    raise SystemExit("fontTools with WOFF2/Brotli support is required to regenerate webfonts.") from exception

FONT_MEMBERS = {
    "IBM_Plex_Sans/static/IBMPlexSans-Regular.ttf": "IBMPlexSans-Regular.woff2",
    "IBM_Plex_Sans/static/IBMPlexSans-Medium.ttf": "IBMPlexSans-Medium.woff2",
    "IBM_Plex_Sans/static/IBMPlexSans-SemiBold.ttf": "IBMPlexSans-SemiBold.woff2",
    "IBM_Plex_Mono/IBMPlexMono-Regular.ttf": "IBMPlexMono-Regular.woff2",
    "IBM_Plex_Mono/IBMPlexMono-Medium.ttf": "IBMPlexMono-Medium.woff2",
    "IBM_Plex_Mono/IBMPlexMono-SemiBold.ttf": "IBMPlexMono-SemiBold.woff2",
}
LICENSE_MEMBERS = (
    "IBM_Plex_Sans/OFL.txt",
    "IBM_Plex_Mono/OFL.txt",
)
DEFAULT_OUTPUT = Path(__file__).resolve().parents[1] / "src/Ufw.Client/wwwroot/fonts/ibm-plex"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path, help="IBM Plex Sans/Mono source ZIP")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT, help="output directory")
    return parser.parse_args()


def normalize_license(raw: bytes) -> bytes:
    text = raw.decode("utf-8")
    return ("\n".join(line.rstrip() for line in text.splitlines()) + "\n").encode("utf-8")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    args = parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)

    with zipfile.ZipFile(args.archive) as archive, tempfile.TemporaryDirectory() as temp_dir_name:
        temp_dir = Path(temp_dir_name)

        sans_license = normalize_license(archive.read(LICENSE_MEMBERS[0]))
        mono_license = normalize_license(archive.read(LICENSE_MEMBERS[1]))
        if sans_license != mono_license:
            raise SystemExit("IBM Plex Sans and Mono archives contain different OFL notices.")

        (output / "OFL.txt").write_bytes(sans_license)

        for member, output_name in FONT_MEMBERS.items():
            source = temp_dir / Path(member).name
            source.write_bytes(archive.read(member))
            destination = output / output_name
            woff2.compress(source, destination)
            print(f"{output_name}: {sha256(destination)}")


if __name__ == "__main__":
    main()
