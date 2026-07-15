from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser(description="Split the generated 3-column Arrow Shower falling-arrow sheet into keyed square frames.")
    parser.add_argument("--input", required=True)
    parser.add_argument("--out-dir", required=True)
    parser.add_argument("--size", type=int, default=192)
    args = parser.parse_args()

    source_path = Path(args.input)
    out_dir = Path(args.out_dir)
    if not source_path.is_file():
        raise FileNotFoundError(source_path)
    if args.size <= 0:
        raise ValueError("--size must be positive")

    out_dir.mkdir(parents=True, exist_ok=True)
    with Image.open(source_path) as source:
        source = source.convert("RGBA")
        width, height = source.size
        if width % 3 != 0:
            raise ValueError(f"3-column sheet width must be divisible by 3: {width}x{height}")

        frame_width = width // 3
        boxes = (
            (0, 0, frame_width, height),
            (frame_width, 0, frame_width * 2, height),
            (frame_width * 2, 0, width, height),
        )
        for index, box in enumerate(boxes, start=1):
            frame = source.crop(box).resize((args.size, args.size), Image.Resampling.NEAREST)
            output_path = out_dir / f"ArrowShowerImpactFrame{index:02d}Key.png"
            frame.save(output_path)
            print(f"frame={index}; source_box={box}; output={output_path}")


if __name__ == "__main__":
    main()
