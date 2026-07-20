import argparse
import json
import subprocess
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


parser = argparse.ArgumentParser(description="Verify an Area Survivors promotional trailer.")
parser.add_argument("--language", choices=("ja", "en"), default="ja")
args = parser.parse_args()

LANGUAGE = args.language
PROJECT = Path(__file__).resolve().parents[2]
RUNTIME = Path.home() / ".cache/codex-video-tools/ffmpeg-8.1.2/runtime/ffmpeg-8.1.2-essentials_build/bin"
FFMPEG = RUNTIME / "ffmpeg.exe"
FFPROBE = RUNTIME / "ffprobe.exe"
VIDEO = PROJECT / f"Docs/SteamStore/Trailer/area-survivors-promo-trailer-{LANGUAGE}-30s.mp4"
WORK = PROJECT / "Tools/Video/Work" / LANGUAGE
CONTACT_SHEET = WORK / f"trailer-review-contact-sheet-{LANGUAGE}.png"

REVIEW_FRAMES = [
    (1.0, "Title: top caption"),
    (3.6, "Title: top + bottom"),
    (6.5, "Paint territory"),
    (11.5, "Level up and weapons"),
    (16.5, "Tokens and skills"),
    (21.0, "Boss: top caption"),
    (23.6, "Boss: top + bottom"),
    (25.5, "Final CTA 0.5s"),
    (26.2, "Final CTA 1.2s"),
    (27.0, "Final CTA 2.0s"),
    (27.8, "Final CTA 2.8s"),
    (28.6, "Final CTA 3.6s"),
]


for required in (FFMPEG, FFPROBE, VIDEO):
    if not required.is_file():
        raise FileNotFoundError(required)

WORK.mkdir(parents=True, exist_ok=True)

probe = subprocess.run(
    [
        str(FFPROBE),
        "-v",
        "error",
        "-show_entries",
        "format=duration,size:stream=index,codec_type,codec_name,width,height,r_frame_rate,sample_rate,channels",
        "-of",
        "json",
        str(VIDEO),
    ],
    text=True,
    capture_output=True,
    encoding="utf-8",
    errors="replace",
)
if probe.returncode != 0:
    raise RuntimeError(probe.stderr)

font_path = Path(r"C:\Windows\Fonts\arial.ttf")
font = ImageFont.truetype(str(font_path), 20) if font_path.is_file() else ImageFont.load_default()
sheet = Image.new("RGB", (1920, 1080), (8, 13, 20))
tile_width = 480
tile_height = 360

for index, (timestamp, label) in enumerate(REVIEW_FRAMES):
    frame_path = WORK / f"review-{index + 1:02d}.png"
    frame = subprocess.run(
        [
            str(FFMPEG),
            "-hide_banner",
            "-loglevel",
            "error",
            "-y",
            "-ss",
            f"{timestamp:.2f}",
            "-i",
            str(VIDEO),
            "-frames:v",
            "1",
            str(frame_path),
        ],
        text=True,
        capture_output=True,
        encoding="utf-8",
        errors="replace",
    )
    if frame.returncode != 0:
        raise RuntimeError(frame.stderr)

    with Image.open(frame_path) as source:
        tile = source.convert("RGB").resize((tile_width, tile_height), Image.Resampling.LANCZOS)
    draw = ImageDraw.Draw(tile, "RGBA")
    draw.rectangle((0, 0, tile_width, 38), fill=(0, 0, 0, 190))
    draw.text((12, 7), f"{timestamp:04.1f}s  {label}", font=font, fill=(255, 255, 255, 255))
    x = (index % 4) * tile_width
    y = (index // 4) * tile_height
    sheet.paste(tile, (x, y))

sheet.save(CONTACT_SHEET)
metadata = json.loads(probe.stdout)
print(json.dumps({"metadata": metadata, "contact_sheet": str(CONTACT_SHEET)}, indent=2))
