import hashlib
import json
import urllib.request
import zipfile
from pathlib import Path


URL = (
    "https://github.com/GyanD/codexffmpeg/releases/download/8.1.2/"
    "ffmpeg-8.1.2-essentials_build.zip"
)
EXPECTED_SHA256 = "db580001caa24ac104c8cb856cd113a87b0a443f7bdf47d8c12b1d740584a2ec"

cache_parent = (Path.home() / ".cache" / "codex-video-tools").resolve()
cache_root = (cache_parent / "ffmpeg-8.1.2").resolve()
if cache_root.parent != cache_parent:
    raise RuntimeError(f"Unsafe cache path: {cache_root}")

archive_path = cache_root / "ffmpeg-8.1.2-essentials_build.zip"
extract_path = cache_root / "runtime"
cache_root.mkdir(parents=True, exist_ok=True)

urllib.request.urlretrieve(URL, archive_path)

digest = hashlib.sha256()
with archive_path.open("rb") as source:
    for block in iter(lambda: source.read(1024 * 1024), b""):
        digest.update(block)
actual_sha = digest.hexdigest().lower()
if actual_sha != EXPECTED_SHA256:
    raise RuntimeError(
        f"FFmpeg archive hash mismatch: expected={EXPECTED_SHA256} actual={actual_sha}"
    )

extract_path.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(archive_path) as archive:
    archive.extractall(extract_path)

ffmpeg_matches = list(extract_path.rglob("ffmpeg.exe"))
ffprobe_matches = list(extract_path.rglob("ffprobe.exe"))
if len(ffmpeg_matches) != 1 or len(ffprobe_matches) != 1:
    raise RuntimeError(
        f"Unexpected executable count: ffmpeg={len(ffmpeg_matches)} ffprobe={len(ffprobe_matches)}"
    )

print(
    json.dumps(
        {
            "archive": str(archive_path),
            "sha256": actual_sha,
            "ffmpeg": str(ffmpeg_matches[0]),
            "ffprobe": str(ffprobe_matches[0]),
        },
        indent=2,
    )
)
