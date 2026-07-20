import argparse
import json
import subprocess
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


parser = argparse.ArgumentParser(description="Build the Area Survivors promotional trailer.")
parser.add_argument("--language", choices=("ja", "en"), default="ja")
args = parser.parse_args()

LANGUAGE = args.language
PROJECT = Path(__file__).resolve().parents[2]
FFMPEG = (
    Path.home()
    / ".cache/codex-video-tools/ffmpeg-8.1.2/runtime/ffmpeg-8.1.2-essentials_build/bin/ffmpeg.exe"
)
WORK = PROJECT / "Tools/Video/Work" / LANGUAGE
OUTPUT_DIR = PROJECT / "Docs/SteamStore/Trailer"
OUTPUT = OUTPUT_DIR / f"area-survivors-promo-trailer-{LANGUAGE}-30s.mp4"
FILTER_SCRIPT = WORK / "filter_complex.txt"

VIDEO_INPUTS = [
    (Path(r"C:\Users\yni87\Videos\1タイトルシーン.mkv"), 3.4),
    (Path(r"C:\Users\yni87\Videos\2ゲーム開始.mkv"), 7.0),
    (Path(r"C:\Users\yni87\Videos\3敵を倒してレベルアップ.mkv"), 13.0),
    (Path(r"C:\Users\yni87\Videos\4トークン獲得と強化.mkv"), 2.5),
    (Path(r"C:\Users\yni87\Videos\5ボスとの戦闘.mkv"), 8.0),
]

FINAL_ART = PROJECT / "Docs/SteamStore/Capsules/area-survivors-main-capsule-knight-with-title.png"
BGM = Path(r"C:\Users\yni87\Downloads\BGM\やえいちひょうじょう.mp3")

CAPTIONS_BY_LANGUAGE = {
    "ja": [
        ("scene1a.png", "床を塗って領土を広げる"),
        ("scene1b.png", "本格2Dアクション系タワーディフェンス"),
        ("scene2.png", "エリアを塗って領土を広げよう"),
        ("scene3.png", "敵を倒してレベルアップし、武器を強化"),
        ("scene4.png", "プレイ中に獲得したトークンでスキルを獲得可能"),
        ("scene5a.png", "防衛設備や新たな武器を獲得し"),
        ("scene5b.png", "ステージの最後に出現するボスを討伐しよう"),
    ],
    "en": [
        ("scene1a.png", "Paint the Ground. Expand Your Territory."),
        ("scene1b.png", "2D Action Meets Tower Defense"),
        ("scene2.png", "Paint the Area and Expand Your Territory"),
        ("scene3.png", "Defeat Enemies, Level Up, and Upgrade Your Weapons"),
        ("scene4.png", "Use Tokens Earned During Each Run to Unlock Skills"),
        ("scene5a.png", "Unlock Defenses and New Weapons"),
        ("scene5b.png", "Defeat the Boss Waiting at the End of the Stage"),
    ],
}
CAPTIONS = CAPTIONS_BY_LANGUAGE[LANGUAGE]


def require_file(path: Path) -> None:
    if not path.is_file():
        raise FileNotFoundError(path)


def find_font() -> Path:
    candidates = [
        Path(r"C:\Windows\Fonts\YuGothB.ttc"),
        Path(r"C:\Windows\Fonts\meiryob.ttc"),
        Path(r"C:\Windows\Fonts\msgothic.ttc"),
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    raise FileNotFoundError("No supported Japanese font was found.")


def caption_font_size(text: str) -> int:
    if len(text) >= 23:
        return 54
    if len(text) >= 19:
        return 58
    return 66


def create_caption(path: Path, text: str, font_path: Path) -> None:
    font = ImageFont.truetype(str(font_path), caption_font_size(text))
    probe = Image.new("RGBA", (1, 1))
    probe_draw = ImageDraw.Draw(probe)
    bbox = probe_draw.textbbox((0, 0), text, font=font, stroke_width=1)
    text_width = bbox[2] - bbox[0]
    text_height = bbox[3] - bbox[1]

    width = min(1700, max(760, text_width + 180))
    height = 146
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    draw.rounded_rectangle(
        (4, 4, width - 4, height - 4),
        radius=24,
        fill=(5, 18, 29, 218),
        outline=(241, 202, 93, 245),
        width=4,
    )
    draw.rounded_rectangle(
        (24, 26, 37, height - 26),
        radius=6,
        fill=(48, 151, 217, 255),
    )

    x = (width - text_width) // 2
    y = (height - text_height) // 2 - bbox[1]
    draw.text(
        (x + 3, y + 4),
        text,
        font=font,
        fill=(0, 0, 0, 185),
        stroke_width=1,
        stroke_fill=(0, 0, 0, 185),
    )
    draw.text(
        (x, y),
        text,
        font=font,
        fill=(255, 249, 225, 255),
        stroke_width=1,
        stroke_fill=(26, 55, 82, 255),
    )
    image.save(path)


def create_cta(path: Path, text: str, font_path: Path, width: int, height: int, font_size: int, primary: bool) -> None:
    font = ImageFont.truetype(str(font_path), font_size)
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    fill = (17, 80, 122, 238) if primary else (5, 18, 29, 218)
    outline = (255, 214, 95, 255)
    draw.rounded_rectangle(
        (5, 5, width - 5, height - 5),
        radius=28,
        fill=fill,
        outline=outline,
        width=5,
    )

    bbox = draw.textbbox((0, 0), text, font=font, stroke_width=1)
    text_width = bbox[2] - bbox[0]
    text_height = bbox[3] - bbox[1]
    x = (width - text_width) // 2
    y = (height - text_height) // 2 - bbox[1]
    draw.text(
        (x + 3, y + 4),
        text,
        font=font,
        fill=(0, 0, 0, 175),
        stroke_width=1,
        stroke_fill=(0, 0, 0, 175),
    )
    draw.text(
        (x, y),
        text,
        font=font,
        fill=(255, 249, 225, 255),
        stroke_width=1,
        stroke_fill=(26, 55, 82, 255),
    )
    image.save(path)


def slide_left(start: float) -> str:
    return (
        "min((W-w)/2\\,-w+(((W-w)/2)+w)*(t-"
        f"{start:.2f})/0.35)"
    )


def slide_right(start: float) -> str:
    return (
        "max((W-w)/2\\,W-(W-(W-w)/2)*(t-"
        f"{start:.2f})/0.35)"
    )


def overlay_line(
    base: str,
    overlay_input: int,
    output: str,
    start: float,
    end: float,
    direction: str,
    y: int = 805,
) -> str:
    x_expression = slide_left(start) if direction == "left" else slide_right(start)
    return (
        f"[{base}][{overlay_input}:v]overlay="
        f"x='{x_expression}':y={y}:enable='between(t\\,{start:.2f}\\,{end:.2f})'"
        f"[{output}]"
    )


for source, _ in VIDEO_INPUTS:
    require_file(source)
require_file(FINAL_ART)
require_file(BGM)
require_file(FFMPEG)

WORK.mkdir(parents=True, exist_ok=True)
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

font_path = find_font()
caption_paths = []
for filename, text in CAPTIONS:
    caption_path = WORK / filename
    create_caption(caption_path, text, font_path)
    caption_paths.append(caption_path)

release_date_path = WORK / "release-date.png"
wishlist_path = WORK / "wishlist-now.png"
create_cta(release_date_path, "August 7, 2026", font_path, 650, 112, 58, False)
create_cta(wishlist_path, "Wishlist Now", font_path, 760, 136, 72, True)
cta_paths = [release_date_path, wishlist_path]

filters = []
for index in range(5):
    filters.append(
        f"[{index}:v]scale=1920:1080:flags=lanczos,fps=60,setsar=1,"
        "setpts=PTS-STARTPTS,eq=contrast=1.03:saturation=1.05,"
        f"trim=duration=5[base{index}]"
    )

filters.append(overlay_line("base0", 6, "scene0a", 0.20, 4.80, "left", y=96))
filters.append(overlay_line("scene0a", 7, "scene0", 2.50, 4.80, "right"))
filters.append(overlay_line("base1", 8, "scene1", 0.25, 4.75, "right"))
filters.append(overlay_line("base2", 9, "scene2", 0.25, 4.75, "left"))
filters.append(overlay_line("base3", 10, "scene3", 0.25, 4.75, "right"))
filters.append(overlay_line("base4", 11, "scene4a", 0.20, 4.80, "left", y=96))
filters.append(overlay_line("scene4a", 12, "scene4", 2.50, 4.80, "right"))

filters.append(
    "[5:v]scale=1920:1100:force_original_aspect_ratio=increase:flags=lanczos,"
    "crop=1920:1080,zoompan="
    "z='min(zoom+0.00017\\,1.05)':"
    "x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s=1920x1080:fps=60,"
    "trim=duration=5,setpts=PTS-STARTPTS[scene5base]"
)
filters.append(
    "[scene5base][13:v]overlay=x='(W-w)/2':"
    "y='760-12*abs(sin(2*PI*t/1.35))'[scene5date]"
)
filters.append(
    "[scene5date][14:v]overlay=x='(W-w)/2':"
    "y='905-20*abs(sin(2*PI*(t+0.15)/0.90))'[scene5cta]"
)
filters.append(
    "[scene5cta]fade=t=in:st=0:d=0.35,"
    "fade=t=out:st=4.10:d=0.90[scene5]"
)

filters.append(
    "[scene0][scene1][scene2][scene3][scene4][scene5]"
    "concat=n=6:v=1:a=0,format=yuv420p[outv]"
)
filters.append(
    "[15:a]atrim=duration=30,asetpts=PTS-STARTPTS,aresample=48000,"
    "volume=0.72,afade=t=in:st=0:d=0.40,afade=t=out:st=27:d=3[aout]"
)

FILTER_SCRIPT.write_text(";\n".join(filters), encoding="utf-8")

command = [str(FFMPEG), "-hide_banner", "-y"]
for source, start in VIDEO_INPUTS:
    command.extend(["-ss", f"{start:.3f}", "-t", "5", "-i", str(source)])

command.extend(["-loop", "1", "-framerate", "60", "-t", "5", "-i", str(FINAL_ART)])

for caption_path in caption_paths:
    command.extend(["-loop", "1", "-framerate", "60", "-t", "5", "-i", str(caption_path)])

for cta_path in cta_paths:
    command.extend(["-loop", "1", "-framerate", "60", "-t", "5", "-i", str(cta_path)])

command.extend(["-t", "30", "-i", str(BGM)])
command.extend(
    [
        "-filter_complex_script",
        str(FILTER_SCRIPT),
        "-map",
        "[outv]",
        "-map",
        "[aout]",
        "-t",
        "30",
        "-c:v",
        "libx264",
        "-preset",
        "medium",
        "-crf",
        "18",
        "-profile:v",
        "high",
        "-level",
        "4.2",
        "-pix_fmt",
        "yuv420p",
        "-c:a",
        "aac",
        "-b:a",
        "256k",
        "-movflags",
        "+faststart",
        str(OUTPUT),
    ]
)

completed = subprocess.run(command, text=True, capture_output=True, encoding="utf-8", errors="replace")
if completed.returncode != 0:
    tail = "\n".join(completed.stderr.splitlines()[-50:])
    raise RuntimeError(f"FFmpeg failed with exit code {completed.returncode}:\n{tail}")

print(
    json.dumps(
        {
            "output": str(OUTPUT),
            "font": str(font_path),
            "filter_script": str(FILTER_SCRIPT),
            "size_bytes": OUTPUT.stat().st_size,
        },
        ensure_ascii=False,
        indent=2,
    )
)
