from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SOURCE = ROOT / "Assets" / "AreaSurvivors" / "Sprites" / "External" / "GeneratedSources" / "AdvancedWeaponsGeneratedSource.png"
AREA_EFFECT_SOURCE = ROOT / "Assets" / "AreaSurvivors" / "Sprites" / "External" / "GeneratedSources" / "AdvancedWeaponAreaGeneratedSource.png"
EXTRA_EFFECT_SOURCES = [
    ("BoomerangSwordBlade", ROOT / "Assets" / "AreaSurvivors" / "Sprites" / "External" / "GeneratedSources" / "BoomerangSwordBladeGeneratedSource.png"),
    ("GunBullet", ROOT / "Assets" / "AreaSurvivors" / "Sprites" / "External" / "GeneratedSources" / "GunBulletGeneratedSource.png"),
    ("AuraSwordSlash", ROOT / "Assets" / "AreaSurvivors" / "Sprites" / "External" / "GeneratedSources" / "AuraSwordSlashGeneratedSource.png"),
]
OUTPUT_DIR = ROOT / "Assets" / "AreaSurvivors" / "Sprites" / "Generated"
SOURCE_OUTPUT_DIR = ROOT / "Assets" / "AreaSurvivors" / "Sprites" / "External" / "GeneratedSources"

ROW_TILE_NAMES = [
    ["Flag", "BoomerangSword", "AuraSword", "ArrowRain"],
    ["Gun", "Frost", "ThunderBall"],
]


def is_background(pixel: tuple[int, int, int, int]) -> bool:
    r, g, b, a = pixel
    if a == 0:
        return True

    # Green-screen sources from image generation.
    if g > 170 and r < 120 and b < 130:
        return True

    # White/checkerboard sources. Keep this edge-connected only so highlights inside
    # the weapon art are preserved.
    if r > 214 and g > 214 and b > 214:
        return True

    # Light grey grid separator lines from exported sheets.
    if abs(r - g) < 10 and abs(g - b) < 10 and 185 <= r <= 235:
        return True

    return False


def is_sheet_background(pixel: tuple[int, int, int, int]) -> bool:
    r, g, b, a = pixel
    if a == 0:
        return True
    if r > 214 and g > 214 and b > 214:
        return True
    if abs(r - g) < 10 and abs(g - b) < 10 and 185 <= r <= 235:
        return True
    return False


def detect_content_intervals(sheet: Image.Image, y0: int, y1: int, expected_count: int) -> list[tuple[int, int]]:
    width, _ = sheet.size
    columns: list[int] = []
    pixels = sheet.load()
    for x in range(width):
        content_pixels = 0
        for y in range(y0, y1):
            if not is_sheet_background(pixels[x, y]):
                content_pixels += 1
        if content_pixels > 2:
            columns.append(x)

    intervals: list[tuple[int, int]] = []
    if columns:
        start = previous = columns[0]
        for x in columns[1:]:
            # Multiple separated parts can belong to one icon, e.g. arrows or bullets.
            if x - previous <= 80:
                previous = x
            else:
                intervals.append((start, previous))
                start = previous = x
        intervals.append((start, previous))

    if len(intervals) != expected_count:
        raise RuntimeError(f"Expected {expected_count} icon groups, but detected {len(intervals)}: {intervals}")

    return intervals


def remove_edge_background(tile: Image.Image) -> Image.Image:
    image = tile.convert("RGBA")
    pixels = image.load()
    width, height = image.size
    visited = set()
    queue: deque[tuple[int, int]] = deque()

    for x in range(width):
        queue.append((x, 0))
        queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))

    while queue:
        x, y = queue.popleft()
        if (x, y) in visited:
            continue
        visited.add((x, y))
        if not is_background(pixels[x, y]):
            continue

        pixels[x, y] = (0, 0, 0, 0)
        if x > 0:
            queue.append((x - 1, y))
        if x < width - 1:
            queue.append((x + 1, y))
        if y > 0:
            queue.append((x, y - 1))
        if y < height - 1:
            queue.append((x, y + 1))

    return image


def crop_alpha(image: Image.Image) -> Image.Image:
    bbox = image.getbbox()
    if bbox is None:
        return Image.new("RGBA", (1, 1), (0, 0, 0, 0))
    return image.crop(bbox)


def pad_square(image: Image.Image, padding: int = 8) -> Image.Image:
    width, height = image.size
    size = max(width, height) + padding * 2
    output = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    output.alpha_composite(image, ((size - width) // 2, (size - height) // 2))
    return output


def pad_aspect(image: Image.Image, padding: int = 8) -> Image.Image:
    width, height = image.size
    output = Image.new("RGBA", (width + padding * 2, height + padding * 2), (0, 0, 0, 0))
    output.alpha_composite(image, (padding, padding))
    return output


def resize_for_game(image: Image.Image, max_size: int = 128) -> Image.Image:
    width, height = image.size
    scale = min(max_size / width, max_size / height)
    new_size = (max(1, round(width * scale)), max(1, round(height * scale)))
    return image.resize(new_size, Image.Resampling.NEAREST)


def process_tile(tile: Image.Image) -> Image.Image:
    transparent = remove_edge_background(tile)
    cropped = crop_alpha(transparent)
    squared = pad_square(cropped)
    return resize_for_game(squared)


def process_effect_tile(tile: Image.Image) -> Image.Image:
    transparent = remove_edge_background(tile)
    cropped = crop_alpha(transparent)
    padded = pad_aspect(cropped)
    return resize_for_game(padded)


def main() -> None:
    parser = argparse.ArgumentParser(description="Slice generated advanced weapon icon sheet into transparent game sprites.")
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    args = parser.parse_args()

    source_path = args.source
    if not source_path.is_absolute():
        source_path = ROOT / source_path
    if not source_path.exists():
        raise FileNotFoundError(source_path)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    SOURCE_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    sheet = Image.open(source_path).convert("RGBA")
    generated: list[Path] = []
    _, sheet_height = sheet.size
    row_height = sheet_height // 2
    for row_index, names in enumerate(ROW_TILE_NAMES):
        top = row_index * row_height
        bottom = sheet_height if row_index == len(ROW_TILE_NAMES) - 1 else (row_index + 1) * row_height
        intervals = detect_content_intervals(sheet, top, bottom, len(names))
        for name, (left, right) in zip(names, intervals):
            margin = 24
            tile = sheet.crop((max(0, left - margin), top, min(sheet.width, right + margin + 1), bottom))

            processed = process_tile(tile)
            processed.save(OUTPUT_DIR / f"{name}.png")
            processed.save(SOURCE_OUTPUT_DIR / f"{name}Source.png")
            generated.append(OUTPUT_DIR / f"{name}.png")

    if AREA_EFFECT_SOURCE.exists():
        area_source = Image.open(AREA_EFFECT_SOURCE).convert("RGBA")
        area_processed = process_tile(area_source)
        area_processed.save(OUTPUT_DIR / "AdvancedWeaponArea.png")
        area_processed.save(SOURCE_OUTPUT_DIR / "AdvancedWeaponAreaSource.png")
        generated.append(OUTPUT_DIR / "AdvancedWeaponArea.png")

    for name, source in EXTRA_EFFECT_SOURCES:
        if not source.exists():
            continue
        effect_source = Image.open(source).convert("RGBA")
        effect_processed = process_effect_tile(effect_source)
        effect_processed.save(OUTPUT_DIR / f"{name}.png")
        effect_processed.save(SOURCE_OUTPUT_DIR / f"{name}Source.png")
        generated.append(OUTPUT_DIR / f"{name}.png")

    for path in generated:
        print(path.relative_to(ROOT))


if __name__ == "__main__":
    main()
