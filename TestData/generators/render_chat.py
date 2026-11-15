#!/usr/bin/env python3
"""Generates TestData/Render/synthetic_chat.json, the 3 second chat the ChatRenderer tests render.

Every embedded image is a unique solid colour so tests can find it in a rendered frame by colour alone. The colours must stay
in sync with FixtureColors in TwitchDownloaderCore.Tests/RenderTests/RenderHarness.cs.

Usage: python3 render_chat.py [output file]   (default: TestData/Render/synthetic_chat.json)
Requires: Pillow (pip install pillow)
"""
from __future__ import annotations

import base64
import io
import json
import sys
from pathlib import Path
from typing import Any

from PIL import Image

JsonDict = dict[str, Any]
Rgb = tuple[int, int, int]
Rgba = tuple[int, int, int, int]


def png(width: int, height: int, rgba: Rgba | None, inner: tuple[int, Rgba] | None = None) -> str:
    """Base64 PNG filled with `rgba` (transparent if None), optionally with an opaque (size, colour) square centred in it."""
    image = Image.new("RGBA", (width, height), rgba if rgba else (0, 0, 0, 0))
    if inner:
        size, color = inner
        offset = (width - size) // 2
        image.paste(Image.new("RGBA", (size, size), color), (offset, offset))
    out = io.BytesIO()
    image.save(out, "PNG", optimize=True)
    return base64.b64encode(out.getvalue()).decode()


def gif(width: int, height: int, colors: list[Rgb], durations_ms: list[int]) -> str:
    """Base64 animated GIF with one solid frame per colour."""
    frames = [Image.new("RGB", (width, height), color) for color in colors]
    out = io.BytesIO()
    frames[0].save(out, "GIF", save_all=True, append_images=frames[1:], duration=durations_ms, loop=0, disposal=1, optimize=False)
    return base64.b64encode(out.getvalue()).decode()


def emote(emote_id: str, name: str, data: str, width: int = 56, height: int = 56, zero_width: bool | None = None) -> JsonDict:
    entry: JsonDict = {"id": emote_id, "imageScale": 2, "data": data, "name": name, "width": width, "height": height}
    if zero_width is not None:
        entry["isZeroWidth"] = zero_width
    return entry


def comment(i: int, offset: float, login: str, display: str, user_id: str, body: str, fragments: list[JsonDict] | None = None,
            badges: list[JsonDict] | None = None, bits: int = 0, color: str = "#C0C0C0") -> JsonDict:
    seconds = int(offset)
    millis = int(round((offset - seconds) * 1000))
    return {
        "_id": f"00000000-0000-0000-0000-{i:012d}",
        "created_at": f"2026-01-01T00:00:{seconds:02d}.{millis:03d}Z",
        "channel_id": "1000", "content_type": "video", "content_id": "2000",
        "content_offset_seconds": offset,
        "commenter": {"display_name": display, "_id": user_id, "name": login, "bio": None,
                      "created_at": "2020-01-01T00:00:00Z", "updated_at": "2020-01-01T00:00:00Z", "logo": None},
        "message": {"body": body, "bits_spent": bits,
                    "fragments": fragments or [{"text": body, "emoticon": None}],
                    "user_badges": badges or [], "user_color": color, "emoticons": []},
    }


def badge(title: str, data: str) -> JsonDict:
    return {"title": title, "description": title, "bytes": data}


def render_chat() -> JsonDict:
    return {
        "FileInfo": {"Version": {"Major": 1, "Minor": 4, "Patch": 0}, "CreatedAt": "2026-01-01T00:00:00Z", "UpdatedAt": "0001-01-01T00:00:00"},
        "streamer": {"name": "TestStreamer", "login": "teststreamer", "id": 1000},
        "video": {"title": "Synthetic chat render fixture", "description": None, "id": "2000", "created_at": "2026-01-01T00:00:00Z",
                  "start": 0, "end": 3, "length": 3, "viewCount": 0, "game": "Testing", "chapters": []},
        "comments": [
            comment(0, 0, "alice", "Alice", "1", "hi TestEmote AnimThird StaticThird ZeroWidth",
                    fragments=[{"text": "hi ", "emoticon": None},
                               {"text": "TestEmote", "emoticon": {"emoticon_id": "1001"}},
                               {"text": " AnimThird StaticThird ZeroWidth", "emoticon": None}],
                    badges=[{"_id": "subscriber", "version": "0"}, {"_id": "moderator", "version": "1"}]),
            comment(1, 1, "bob", "Bob", "2", "Cheer100 Cheer1 thanks", bits=101),
            comment(2, 2, "teststreamer", "TestStreamer", "1000", "welcome everyone", badges=[{"_id": "broadcaster", "version": "1"}]),
        ],
        "embeddedData": {
            "thirdParty": [
                emote("tp1", "StaticThird", png(56, 56, (0, 255, 255, 255))),
                emote("tp2", "AnimThird", gif(56, 56, [(255, 0, 0), (0, 255, 0), (0, 0, 255)], [100, 200, 300])),
                emote("tp3", "ZeroWidth", png(56, 56, None, inner=(16, (255, 128, 0, 255))), zero_width=True),
            ],
            "firstParty": [emote("1001", "TestEmote", png(56, 56, (255, 0, 255, 255)))],
            "twitchBadges": [
                {"name": "subscriber", "versions": {"0": badge("Subscriber", png(36, 36, (255, 255, 0, 255)))}},
                {"name": "moderator", "versions": {"1": badge("Moderator", png(36, 36, (0, 128, 0, 255)))}},
                {"name": "broadcaster", "versions": {"1": badge("Broadcaster", png(36, 36, (128, 0, 0, 255)))}},
                {"name": "bits", "versions": {"1000": badge("cheer 1000", png(36, 36, (128, 128, 0, 255)))}},
            ],
            "twitchBits": [{"prefix": "Cheer", "tierList": {
                "1": emote("cheer1", "Cheer1", png(56, 56, (128, 64, 0, 255))),
                "100": emote("cheer100", "Cheer100", png(56, 56, (0, 64, 128, 255))),
            }}],
        },
    }


def main() -> None:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent / "Render" / "synthetic_chat.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(render_chat(), f, indent=2)
        f.write("\n")
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
