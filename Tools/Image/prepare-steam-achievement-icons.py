from pathlib import Path

from PIL import Image, ImageOps


ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = ROOT / "Assets" / "AreaSurvivors" / "Sprites" / "External" / "SteamAchievements"
OUTPUT_ROOT = ROOT / "Docs" / "SteamStore" / "Achievements" / "Icons"

ICON_SOURCES = {
    "first-sortie": "FirstSortieSource.png",
    "kill-100": "Kill100Source.png",
    "kill-1000": "Kill1000Source.png",
    "kill-10000": "Kill10000Source.png",
    "clear-stage-1": "ClearStage1Source.png",
    "clear-stage-2": "ClearStage2Source.png",
    "clear-stage-3": "ClearStage3Source.png",
    "clear-stage-4": "ClearStage4Source.png",
    "first-evolution": "FirstEvolutionSource.png",
    "all-evolutions": "AllEvolutionsSource.png",
    "max-all-skills": "MaxAllSkillsSource.png",
    "all-relics": "AllRelicsSource.png",
    "clear-all-difficulty-5": "ClearAllDifficulty5Source.png",
}


def prepare_icon(stem: str, source_name: str) -> None:
    source_path = SOURCE_ROOT / source_name
    if not source_path.is_file():
        raise FileNotFoundError(f"Missing achievement source: {source_path}")

    unlocked_dir = OUTPUT_ROOT / "Unlocked"
    locked_dir = OUTPUT_ROOT / "Locked"
    unlocked_dir.mkdir(parents=True, exist_ok=True)
    locked_dir.mkdir(parents=True, exist_ok=True)

    with Image.open(source_path) as source:
        unlocked = source.convert("RGB").resize((256, 256), Image.Resampling.NEAREST)

    unlocked_path = unlocked_dir / f"{stem}.png"
    locked_path = locked_dir / f"{stem}.png"
    unlocked.save(unlocked_path, format="PNG", optimize=False)
    ImageOps.grayscale(unlocked).convert("RGB").save(locked_path, format="PNG", optimize=False)

    with Image.open(unlocked_path) as check_unlocked, Image.open(locked_path) as check_locked:
        if check_unlocked.size != (256, 256) or check_locked.size != (256, 256):
            raise RuntimeError(f"Achievement icon size validation failed: {stem}")
        for red, green, blue in check_locked.convert("RGB").get_flattened_data():
            if red != green or green != blue:
                raise RuntimeError(f"Locked achievement icon is not grayscale: {stem}")


def main() -> None:
    for stem, source_name in ICON_SOURCES.items():
        prepare_icon(stem, source_name)
    print(f"Prepared {len(ICON_SOURCES)} unlocked and locked Steam achievement icons.")


if __name__ == "__main__":
    main()
