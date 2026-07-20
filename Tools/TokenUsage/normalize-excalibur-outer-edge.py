"""Rebuild Excalibur's annular-sector texture without stretching blue edge art.

The outer and inner blue decorative bands are copied at their authored pixel
height. Only the central white slash band is vertically nearest-neighbor
stretched, so the Mesh can be thick without turning the blue edge strokes into
long radial lines. The copied outer band retains a deliberately subtle jagged
silhouette.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from PIL import Image


OUTER_START_ROW = 6
OUTER_END_ROW = 11
CORE_START_ROW = 11
CORE_END_ROW = 17
CORE_OUTPUT_HEIGHT = 28
INNER_START_ROW = 17
INNER_END_ROW = 22


def inside_project(path: Path, project_root: Path) -> bool:
    try:
        path.relative_to(project_root)
        return True
    except ValueError:
        return False


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--target", required=True)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    project_root = Path.cwd().resolve()
    source = Path(args.source).resolve()
    target = Path(args.target).resolve()
    if not inside_project(source, project_root) or not inside_project(target, project_root):
        raise ValueError("source and target must stay inside the project root")
    if source.suffix.lower() != ".png" or target.suffix.lower() != ".png":
        raise ValueError("source and target must be PNG files")
    if not source.is_file():
        raise FileNotFoundError(source)

    image = Image.open(source).convert("RGBA")
    width, height = image.size
    if height < INNER_END_ROW:
        raise ValueError(f"source texture height is too small: {height}")

    outer = image.crop((0, OUTER_START_ROW, width, OUTER_END_ROW))
    core = image.crop((0, CORE_START_ROW, width, CORE_END_ROW))
    inner = image.crop((0, INNER_START_ROW, width, INNER_END_ROW))
    stretched_core = core.resize((width, CORE_OUTPUT_HEIGHT), Image.Resampling.NEAREST)
    output = Image.new("RGBA", (width, outer.height + stretched_core.height + inner.height))
    output.paste(outer, (0, 0))
    output.paste(stretched_core, (0, outer.height))
    output.paste(inner, (0, outer.height + stretched_core.height))

    print(f"compose_excalibur_sector_source: {source}")
    print(f"compose_excalibur_sector_target: {target}")
    print(f"compose_excalibur_sector_bands: outer={outer.height}, core={core.height}->{stretched_core.height}, inner={inner.height}")
    print(f"compose_excalibur_sector_output_size: {output.width}x{output.height}")
    if args.dry_run:
        print("compose_excalibur_sector_dry_run: passed")
        return 0

    output.save(target)
    print("compose_excalibur_sector: applied")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"compose_excalibur_sector_error: {error}", file=sys.stderr)
        raise SystemExit(1)
