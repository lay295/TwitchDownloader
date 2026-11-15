#!/usr/bin/env python3
"""Generates TestData/Downloaders/chat_update_input.json, the existing chat file the ChatUpdater tests update.

It is deliberately "stale": old title/game, a 10-40 s trim window, and a placeholder 2x2 image for emote 25 that the
updater must replace when asked to re-embed images.

Usage: python3 chat_update_input.py [output file]   (default: TestData/Downloaders/chat_update_input.json)
"""
from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

from solid_png import solid_png_base64

JsonDict = dict[str, Any]


def comment(comment_id: str, offset: int, user_id: str, body: str, fragments: list[JsonDict], badges: list[JsonDict], bits: int = 0,
            emoticons: list[JsonDict] | None = None) -> JsonDict:
    return {"_id": comment_id, "created_at": f"2024-01-01T00:00:{offset:02d}Z", "channel_id": "11111", "content_type": "video",
            "content_id": "1234567890", "content_offset_seconds": offset,
            "commenter": {"display_name": f"User{user_id}", "_id": user_id, "name": f"user{user_id}", "bio": None, "created_at": "0001-01-01T00:00:00",
                          "updated_at": "0001-01-01T00:00:00", "logo": None},
            "message": {"body": body, "bits_spent": bits, "fragments": fragments, "user_badges": badges, "user_color": "#00FF00",
                        "emoticons": emoticons or []}}


def chat_update_input() -> JsonDict:
    stale = solid_png_base64(2, 2, (0, 0, 0))
    return {
        "FileInfo": {"Version": {"Major": 1, "Minor": 4, "Patch": 0}, "CreatedAt": "2024-01-02T00:00:00Z", "UpdatedAt": "0001-01-01T00:00:00"},
        "streamer": {"name": "FakeStreamer", "login": "fakestreamer", "id": 11111},
        "video": {"title": "Old title", "description": "Old description", "id": "1234567890", "created_at": "2024-01-01T00:00:00Z", "start": 10,
                  "end": 40, "length": 60, "viewCount": 1, "game": "Old game",
                  "chapters": [{"id": "", "startMilliseconds": 0, "lengthMilliseconds": 30000, "type": "GAME_CHANGE", "description": "Old game",
                                "subDescription": "", "thumbnailUrl": "", "gameId": "1", "gameDisplayName": "Old game", "gameBoxArtUrl": ""}]},
        "comments": [
            comment("c2", 12, "1", "hello Kappa", [{"text": "hello ", "emoticon": None}, {"text": "Kappa", "emoticon": {"emoticon_id": "25"}}],
                    [{"_id": "subscriber", "version": "1"}], emoticons=[{"_id": "25", "begin": 6, "end": 12}]),
            comment("c3", 20, "2", "bttvFake Cheer100", [{"text": "bttvFake Cheer100", "emoticon": None}], [], bits=100),
            comment("c4", 33, "3", "still here", [{"text": "still here", "emoticon": None}], []),
        ],
        "embeddedData": {"thirdParty": [], "firstParty": [{"id": "25", "imageScale": 2, "data": stale, "name": "25", "width": 1, "height": 1}],
                         "twitchBadges": [], "twitchBits": []},
    }


def main() -> None:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent / "Downloaders" / "chat_update_input.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(chat_update_input(), indent=2) + "\n")
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
