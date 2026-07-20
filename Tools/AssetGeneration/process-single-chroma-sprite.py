from __future__ import annotations

import argparse
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Remove a flat chroma background, crop alpha bounds, resize, and validate one sprite."
    )
    parser.add_argument("--python", required=True, type=Path)
    parser.add_argument("--helper", required=True, type=Path)
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--size", type=int)
    parser.add_argument("--width", type=int)
    parser.add_argument("--height", type=int)
    parser.add_argument("--padding", type=int, default=8)
    parser.add_argument("--anchor-bottom", action="store_true")
    return parser.parse_args()


def remove_chroma(args: argparse.Namespace, destination: Path, edge_contract: bool) -> None:
    # The fringe retry writes to the same temporary matte path. The helper
    # intentionally refuses accidental overwrites, so clear only this
    # script-owned temporary output before invoking it again.
    if destination.exists():
        destination.unlink()
    command = [
        str(args.python),
        str(args.helper),
        "--input",
        str(args.input),
        "--out",
        str(destination),
        "--auto-key",
        "border",
        "--soft-matte",
        "--transparent-threshold",
        "12",
        "--opaque-threshold",
        "220",
        "--despill",
    ]
    if edge_contract:
        command.extend(["--edge-contract", "1"])
    completed = subprocess.run(command, check=False, capture_output=True, text=True)
    if completed.returncode != 0:
        raise RuntimeError(
            f"chroma helper failed: exit={completed.returncode}\n"
            f"stdout={completed.stdout}\nstderr={completed.stderr}"
        )


def crop_resize(
    source: Path,
    destination: Path,
    output_width: int,
    output_height: int,
    padding: int,
    anchor_bottom: bool,
) -> None:
    with Image.open(source) as loaded:
        rgba = loaded.convert("RGBA")
        bounds = rgba.getchannel("A").getbbox()
        if bounds is None:
            raise ValueError("no visible subject remains after chroma removal")
        cropped = rgba.crop(bounds)
        usable_width = output_width - padding * 2
        usable_height = output_height - padding * 2
        if usable_width <= 0 or usable_height <= 0:
            raise ValueError("padding leaves no usable output area")
        scale = min(usable_width / cropped.width, usable_height / cropped.height)
        width = max(1, round(cropped.width * scale))
        height = max(1, round(cropped.height * scale))
        resized = cropped.resize((width, height), Image.Resampling.NEAREST)
        canvas = Image.new("RGBA", (output_width, output_height), (0, 0, 0, 0))
        x = (output_width - width) // 2
        y = output_height - padding - height if anchor_bottom else (output_height - height) // 2
        canvas.alpha_composite(resized, (x, y))
        destination.parent.mkdir(parents=True, exist_ok=True)
        canvas.save(destination, format="PNG", optimize=False)


def validate(path: Path, expected_width: int, expected_height: int) -> tuple[float, int]:
    with Image.open(path) as loaded:
        if loaded.mode != "RGBA" or loaded.size != (expected_width, expected_height):
            raise ValueError(f"unexpected sprite format: mode={loaded.mode} size={loaded.size}")
        rgba = loaded.copy()

    corners = (
        rgba.getpixel((0, 0))[3],
        rgba.getpixel((expected_width - 1, 0))[3],
        rgba.getpixel((0, expected_height - 1))[3],
        rgba.getpixel((expected_width - 1, expected_height - 1))[3],
    )
    if any(corners):
        raise ValueError(f"sprite corners are not transparent: {corners}")

    pixels = list(rgba.get_flattened_data())
    visible = sum(alpha > 0 for _, _, _, alpha in pixels)
    coverage = visible / (expected_width * expected_height)
    if not 0.01 <= coverage <= 0.90:
        raise ValueError(f"implausible visible coverage: {coverage:.4f}")

    magenta_fringe = sum(
        alpha > 0 and red > 120 and blue > 120 and green + 50 < min(red, blue)
        for red, green, blue, alpha in pixels
    )
    return coverage, magenta_fringe


def main() -> int:
    args = parse_args()
    if args.size is not None:
        if args.width is not None or args.height is not None:
            raise ValueError("use either --size or --width/--height")
        output_width = output_height = args.size
    elif args.width is not None and args.height is not None:
        output_width = args.width
        output_height = args.height
    else:
        raise ValueError("provide --size or both --width and --height")
    if not 16 <= output_width <= 2048 or not 16 <= output_height <= 2048:
        raise ValueError("output dimensions must be between 16 and 2048")
    if args.padding < 0:
        raise ValueError("padding must be non-negative")
    for required in (args.python, args.helper, args.input):
        if not required.is_file():
            raise FileNotFoundError(required)
    if args.output.exists():
        raise FileExistsError(args.output)

    with tempfile.TemporaryDirectory(prefix="area-single-sprite-") as temp:
        matte = Path(temp) / "matte.png"
        remove_chroma(args, matte, edge_contract=False)
        crop_resize(matte, args.output, output_width, output_height, args.padding, args.anchor_bottom)
        coverage, fringe = validate(args.output, output_width, output_height)
        if fringe:
            args.output.unlink()
            remove_chroma(args, matte, edge_contract=True)
            crop_resize(matte, args.output, output_width, output_height, args.padding, args.anchor_bottom)
            coverage, fringe = validate(args.output, output_width, output_height)
        if fringe:
            raise ValueError(f"magenta fringe remains: {fringe} pixels")

    print(
        f"single_chroma_sprite: passed output={args.output} size={output_width}x{output_height} "
        f"coverage={coverage:.4f} magenta_fringe={fringe}"
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise
