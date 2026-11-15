#!/usr/bin/env python3
"""Generates TestData/E2E/chat.json, the 10 second chat the CLI end-to-end tests feed to `chatupdate` and `chatrender --offline`.

It has an empty video id (so `chatupdate` skips fetching video info from Twitch) and one embedded first-party emote (red),
third-party emote (blue) and badge (green); the render test looks for those colours in the output.

Usage: python3 e2e_chat.py [output file]   (default: TestData/E2E/chat.json)
"""
from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

from solid_png import solid_png_base64

JsonDict = dict[str, Any]


def comment(i: int, offset: float, user: str, text: str, fragments: list[JsonDict] | None = None, emoticons: list[JsonDict] | None = None,
            badges: list[JsonDict] | None = None, color: str | None = "#FFD700") -> JsonDict:
    return {
        "_id": f"00000000-0000-0000-0000-{i:012d}",
        "created_at": f"2024-01-01T00:00:{int(offset):02d}.{int(offset * 1000) % 1000:03d}Z",
        "channel_id": "1",
        "content_type": "video",
        "content_id": "",
        "content_offset_seconds": offset,
        "commenter": {
            "display_name": user, "_id": str(1000 + i), "name": user.lower(), "bio": None,
            "created_at": "2020-01-01T00:00:00Z", "updated_at": "2020-01-01T00:00:00Z", "logo": None,
        },
        "message": {
            "body": text, "bits_spent": 0,
            "fragments": fragments or [{"text": text, "emoticon": None}],
            "user_badges": badges or [], "user_color": color, "emoticons": emoticons or [],
        },
    }


def e2e_chat() -> JsonDict:
    badge = [{"_id": "e2ebadge", "version": "1"}]
    comments = [
        comment(1, 0.5, "E2E_Alpha", "hello from alpha", badges=badge),
        comment(2, 1.5, "E2E_Bravo", "nice E2eFirst",
                fragments=[{"text": "nice ", "emoticon": None}, {"text": "E2eFirst", "emoticon": {"emoticon_id": "e2efp1"}}],
                emoticons=[{"_id": "e2efp1", "begin": 5, "end": 12}]),
        comment(3, 2.5, "E2E_Charlie", "third party E2eThird here"),
        comment(4, 3.5, "E2E_Delta", "message four"),
        comment(5, 5.0, "E2E_Echo", "message five", color=None),
        comment(6, 6.5, "E2E_Foxtrot", "message six"),
        comment(7, 8.0, "E2E_Golf", "message seven"),
        comment(8, 9.5, "E2E_Alpha", "goodbye from alpha", badges=badge),
    ]
    return {
        "FileInfo": {"Version": {"Major": 1, "Minor": 4, "Patch": 0}, "CreatedAt": "2024-01-01T00:00:00Z", "UpdatedAt": "0001-01-01T00:00:00"},
        "streamer": {"name": "E2E_Streamer", "login": "e2e_streamer", "id": 1},
        "clipper": None,
        "video": {"title": "E2E synthetic chat", "description": None, "id": "", "created_at": "2024-01-01T00:00:00Z",
                  "start": 0, "end": 10, "length": 10, "viewCount": 0, "game": "Testing", "chapters": []},
        "comments": comments,
        "embeddedData": {
            "thirdParty": [{"id": "e2etp1", "imageScale": 2, "data": solid_png_base64(56, 56, (0, 0, 254)), "name": "E2eThird", "width": 28,
                            "height": 28}],
            "firstParty": [{"id": "e2efp1", "imageScale": 2, "data": solid_png_base64(56, 56, (253, 0, 0)), "name": "E2eFirst", "width": 28,
                            "height": 28}],
            "twitchBadges": [{"name": "e2ebadge", "versions": {"1": {"title": "E2E Badge", "description": "E2E Badge",
                                                                    "bytes": solid_png_base64(36, 36, (0, 127, 0))}}}],
            "twitchBits": [],
        },
    }


def main() -> None:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent / "E2E" / "chat.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(e2e_chat(), f, indent=2)
        f.write("\n")
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
