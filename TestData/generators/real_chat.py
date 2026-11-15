#!/usr/bin/env python3
"""Generates TestData/Render/real_chat_60s.json.gz, the real chat the render preview (`/render-preview`) renders.

It is the first 60 s of Twitch highlight 1822879862 (Jerma985), downloaded with embedded images and anonymized: every
commenter's name, display name, id, avatar and bio is replaced, while message text, colours, badges and emotes stay real.
Highlights don't expire, but if this one is ever deleted, pass a different video id.

Usage:
  python3 real_chat.py --cli <path to TwitchDownloaderCLI> [--video-id ID]   download, anonymize and write the fixture
  python3 real_chat.py --input chat.json                                      anonymize an already downloaded chat
Requires: a built TwitchDownloaderCLI (dotnet build TwitchDownloaderCLI) and network access when downloading
"""
from __future__ import annotations

import argparse
import gzip
import json
import subprocess
import tempfile
from pathlib import Path
from typing import Any

DEFAULT_OUTPUT = Path(__file__).resolve().parent.parent / "Render" / "real_chat_60s.json.gz"


def download(cli: str, video_id: str, destination: Path) -> None:
    subprocess.run([cli, "chatdownload", "--id", video_id, "-b", "0", "-e", "60", "-E", "-o", str(destination), "--banner", "false",
                    "--collision", "Overwrite"], check=True)


def anonymize(chat: dict[str, Any]) -> int:
    """Replaces every commenter's identity in place with ViewerNNN (numbered in order of first appearance). Returns the user count."""
    ids: dict[str, int] = {}
    for comment in chat["comments"]:
        commenter = comment["commenter"]
        number = ids.setdefault(commenter["_id"], len(ids) + 1)
        commenter.update(display_name=f"Viewer{number:03d}", name=f"viewer{number:03d}", _id=str(900000000 + number), logo=None, bio=None)
    chat["FileInfo"]["CreatedAt"] = "2026-01-01T00:00:00Z"
    return len(ids)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--cli", help="TwitchDownloaderCLI executable used to download the chat")
    source.add_argument("--input", type=Path, help="An already downloaded chat JSON (with embedded images) to anonymize")
    parser.add_argument("--video-id", default="1822879862")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()

    with tempfile.TemporaryDirectory() as work:
        raw = args.input or Path(work) / "chat.json"
        if args.cli:
            download(args.cli, args.video_id, raw)
        chat: dict[str, Any] = json.loads(Path(raw).read_text(encoding="utf-8"))

    users = anonymize(chat)
    # No file name and mtime=0 in the gzip header, so the output depends only on the chat
    with open(args.output, "wb") as file, gzip.GzipFile(filename="", fileobj=file, mode="wb", mtime=0) as f:
        f.write(json.dumps(chat, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
    print(f"Wrote {args.output}: {len(chat['comments'])} comments from {users} anonymized users")


if __name__ == "__main__":
    main()
