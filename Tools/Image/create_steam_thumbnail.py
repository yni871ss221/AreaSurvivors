import argparse
from pathlib import Path

from PIL import Image


def crop_to_aspect(image: Image.Image, target_width: int, target_height: int) -> Image.Image:
    source_width, source_height = image.size
    target_ratio = target_width / target_height
    source_ratio = source_width / source_height

    if source_ratio < target_ratio:
        crop_height = round(source_width / target_ratio)
        return image.crop((0, 0, source_width, crop_height))

    crop_width = round(source_height * target_ratio)
    left = (source_width - crop_width) // 2
    return image.crop((left, 0, left + crop_width, source_height))


def main() -> None:
    parser = argparse.ArgumentParser(description="Create an exact-size Steam thumbnail from a source image.")
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--width", type=int, default=1920)
    parser.add_argument("--height", type=int, default=1080)
    args = parser.parse_args()

    if not args.source.is_file():
        raise FileNotFoundError(args.source)
    if args.width <= 0 or args.height <= 0:
        raise ValueError("width and height must be positive")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(args.source) as source:
        source_rgba = source.convert("RGBA")
        cropped = crop_to_aspect(source_rgba, args.width, args.height)
        thumbnail = cropped.resize((args.width, args.height), Image.Resampling.LANCZOS)
        thumbnail.save(args.output, format="PNG", optimize=True)

    with Image.open(args.output) as result:
        print(f"output={args.output.resolve()}")
        print(f"size={result.width}x{result.height}")
        print(f"mode={result.mode}")


if __name__ == "__main__":
    main()
