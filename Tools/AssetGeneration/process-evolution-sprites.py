from __future__ import annotations

import argparse
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path

from PIL import Image


TARGET_SIZES = {
    "ExcaliburIcon.png": 96,
    "ExcaliburEffect.png": 192,
    "GoldenBowIcon.png": 96,
    "GoldenBowEffect.png": 128,
    "ArrowShowerIcon.png": 96,
    "ArrowShowerEffect.png": 192,
    "MachineGunIcon.png": 96,
    "MachineGunEffect.png": 96,
    "FireMissileIcon.png": 96,
    "FireMissileEffect.png": 128,
    "FrostStormIcon.png": 96,
    "FrostStormEffect.png": 192,
    "ThunderStormIcon.png": 96,
    "ThunderStormEffect.png": 128,
    "DualShieldIcon.png": 96,
    "DualShieldEffect.png": 128,
    "GoddessBlessingIcon.png": 96,
    "GoddessBlessingEffect.png": 192,
}


@dataclass(frozen=True)
class Validation:
    name: str
    size: tuple[int, int]
    coverage: float
    corner_alpha: tuple[int, int, int, int]
    fringe_pixels: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--python", required=True, type=Path)
    parser.add_argument("--helper", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--external-dir", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    return parser.parse_args()


def read_sources(manifest: Path, external_dir: Path) -> list[tuple[Path, str]]:
    entries: list[tuple[Path, str]] = []
    for line_number, raw_line in enumerate(manifest.read_text(encoding="utf-8-sig").splitlines(), 1):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        parts = [part.strip() for part in line.split("|")]
        if len(parts) != 2:
            raise ValueError(f"manifest line {line_number} must have two fields")
        source_name = parts[1]
        if not source_name.endswith("Source.png"):
            raise ValueError(f"manifest line {line_number} destination must end in Source.png")
        output_name = source_name.removesuffix("Source.png") + ".png"
        if output_name not in TARGET_SIZES:
            raise ValueError(f"no target size configured for {output_name}")
        source_path = external_dir / source_name
        if not source_path.is_file():
            raise FileNotFoundError(source_path)
        entries.append((source_path, output_name))
    if len(entries) != len(TARGET_SIZES):
        raise ValueError(f"expected {len(TARGET_SIZES)} manifest entries, found {len(entries)}")
    return entries


def run_chroma_helper(
    python: Path,
    helper: Path,
    source: Path,
    destination: Path,
    edge_contract: bool,
) -> None:
    command = [
        str(python),
        str(helper),
        "--input",
        str(source),
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
            f"chroma helper failed for {source.name}: exit={completed.returncode}\n"
            f"stdout={completed.stdout}\nstderr={completed.stderr}"
        )


def crop_resize(source: Path, destination: Path, target_size: int) -> None:
    with Image.open(source) as loaded:
        rgba = loaded.convert("RGBA")
        alpha = rgba.getchannel("A")
        bounds = alpha.getbbox()
        if bounds is None:
            raise ValueError(f"no visible subject after chroma removal: {source}")
        cropped = rgba.crop(bounds)
        padding = 6 if target_size <= 128 else 8
        usable = target_size - padding * 2
        scale = min(usable / cropped.width, usable / cropped.height)
        resized_width = max(1, round(cropped.width * scale))
        resized_height = max(1, round(cropped.height * scale))
        resized = cropped.resize((resized_width, resized_height), Image.Resampling.NEAREST)
        canvas = Image.new("RGBA", (target_size, target_size), (0, 0, 0, 0))
        offset = ((target_size - resized_width) // 2, (target_size - resized_height) // 2)
        canvas.alpha_composite(resized, offset)
        destination.parent.mkdir(parents=True, exist_ok=True)
        canvas.save(destination, format="PNG", optimize=False)


def validate(destination: Path) -> Validation:
    with Image.open(destination) as loaded:
        if loaded.mode != "RGBA":
            raise ValueError(f"{destination.name}: expected RGBA, found {loaded.mode}")
        rgba = loaded.copy()
    width, height = rgba.size
    expected = TARGET_SIZES[destination.name]
    if (width, height) != (expected, expected):
        raise ValueError(f"{destination.name}: unexpected size {width}x{height}")
    corners = (
        rgba.getpixel((0, 0))[3],
        rgba.getpixel((width - 1, 0))[3],
        rgba.getpixel((0, height - 1))[3],
        rgba.getpixel((width - 1, height - 1))[3],
    )
    alpha_values = list(rgba.getchannel("A").get_flattened_data())
    visible = sum(alpha > 0 for alpha in alpha_values)
    coverage = visible / (width * height)
    fringe = 0
    for red, green, blue, alpha in rgba.get_flattened_data():
        if alpha > 0 and green > 100 and green > red + 40 and green > blue + 40:
            fringe += 1
    if any(corners):
        raise ValueError(f"{destination.name}: corners are not transparent: {corners}")
    if not 0.01 <= coverage <= 0.90:
        raise ValueError(f"{destination.name}: implausible coverage {coverage:.4f}")
    return Validation(destination.name, rgba.size, coverage, corners, fringe)


def main() -> int:
    args = parse_args()
    for required in (args.python, args.helper, args.manifest):
        if not required.is_file():
            raise FileNotFoundError(required)
    if not args.external_dir.is_dir() or not args.output_dir.is_dir():
        raise FileNotFoundError("external-dir and output-dir must already exist")
    entries = read_sources(args.manifest, args.external_dir)
    validations: list[Validation] = []
    with tempfile.TemporaryDirectory(prefix="area-evolution-sprites-") as temporary:
        temporary_dir = Path(temporary)
        for index, (source, output_name) in enumerate(entries, 1):
            matte = temporary_dir / output_name
            run_chroma_helper(args.python, args.helper, source, matte, edge_contract=False)
            output = args.output_dir / output_name
            crop_resize(matte, output, TARGET_SIZES[output_name])
            result = validate(output)
            if result.fringe_pixels:
                run_chroma_helper(args.python, args.helper, source, matte, edge_contract=True)
                crop_resize(matte, output, TARGET_SIZES[output_name])
                result = validate(output)
            if result.fringe_pixels:
                raise ValueError(f"{output_name}: key fringe pixels remain: {result.fringe_pixels}")
            validations.append(result)
            print(
                f"processed={index}/{len(entries)} name={result.name} "
                f"size={result.size[0]}x{result.size[1]} coverage={result.coverage:.4f} "
                f"corners={result.corner_alpha} fringe={result.fringe_pixels}",
                flush=True,
            )
    print(
        f"validated={len(validations)} alpha=RGBA transparent_corners={len(validations)} "
        f"fringe_free={sum(item.fringe_pixels == 0 for item in validations)}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"ERROR: {error}", file=sys.stderr, flush=True)
        raise
