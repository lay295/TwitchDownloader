#!/usr/bin/env python3
"""Generates the tiny synthetic video fixtures with ffmpeg (a colour test pattern and a sine tone, no real footage):

  Downloaders/vod_ts/0-2.ts      three consecutive 1 s MPEG-TS parts of one stream (64x36 H.264 + mono AAC), served as a fake VOD
  Downloaders/vod_fmp4/          a 2 s fragmented-MP4 VOD: init-0.mp4 plus parts 0.mp4 and 1.mp4
  Downloaders/clip.mp4           a 1 s clip
  E2E/part0-2.ts                 three consecutive 1 s video-only MPEG-TS parts (32x18) for the `tsmerge` end-to-end tests

The exact bytes depend on the ffmpeg build, so regenerated files differ from the committed ones but are equivalent for the tests.

Usage: python3 media.py [TestData dir]   (default: the TestData folder containing this script)
Requires: ffmpeg on PATH
"""
from __future__ import annotations

import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

PATTERN_64 = ["-f", "lavfi", "-i", "testsrc=size=64x36:rate=10"]
PATTERN_32 = ["-f", "lavfi", "-i", "testsrc=size=32x18:rate=10"]
TONE = ["-f", "lavfi", "-i", "sine=frequency=440:sample_rate=22050"]
# A keyframe every second (10 frames) so every part starts with one, like Twitch's segments
H264 = ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-g", "10", "-keyint_min", "10", "-sc_threshold", "0"]
# Low quality keeps each downloader fixture at a few KB
SMALL = ["-crf", "40"]
AAC = ["-c:a", "aac", "-ac", "1", "-ar", "22050", "-b:a", "16k"]


def ffmpeg(*args: str) -> None:
    subprocess.run(["ffmpeg", "-v", "error", "-y", *args], check=True)


def vod_ts(out: Path) -> None:
    out.mkdir(parents=True, exist_ok=True)
    ffmpeg(*PATTERN_64, *TONE, "-t", "3", *H264, *SMALL, *AAC,
           "-f", "segment", "-segment_format", "mpegts", "-segment_time", "1", "-reset_timestamps", "0", str(out / "%d.ts"))


def vod_fmp4(out: Path) -> None:
    out.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as work:
        playlist = Path(work) / "index.m3u8"
        ffmpeg(*PATTERN_64, *TONE, "-t", "2", *H264, *SMALL, *AAC,
               "-f", "hls", "-hls_time", "1", "-hls_segment_type", "fmp4", "-hls_fmp4_init_filename", "init-0.mp4",
               "-hls_segment_filename", str(Path(work) / "%d.mp4"), str(playlist))
        for name in ("init-0.mp4", "0.mp4", "1.mp4"):
            shutil.copyfile(Path(work) / name, out / name)


def clip(path: Path) -> None:
    ffmpeg(*PATTERN_64, *TONE, "-t", "1", *H264, *SMALL, *AAC, "-movflags", "+faststart", str(path))


def e2e_parts(out: Path) -> None:
    out.mkdir(parents=True, exist_ok=True)
    ffmpeg(*PATTERN_32, "-t", "3", "-preset", "ultrafast", *H264,
           "-f", "segment", "-segment_format", "mpegts", "-segment_time", "1", "-reset_timestamps", "0", str(out / "part%d.ts"))


def main() -> None:
    if shutil.which("ffmpeg") is None:
        sys.exit("ffmpeg was not found on PATH")
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent
    vod_ts(root / "Downloaders" / "vod_ts")
    vod_fmp4(root / "Downloaders" / "vod_fmp4")
    clip(root / "Downloaders" / "clip.mp4")
    e2e_parts(root / "E2E")
    print(f"Wrote media fixtures under {root}")


if __name__ == "__main__":
    main()
