#!/usr/bin/env python3
"""Generates the synthetic chat fixtures in TestData/Chat/.

Writes sample_chat_small.json (schema 1.4.0), the older-schema fixtures chat_v1_3_0.json, chat_v1_2_2.json and
chat_legacy_v5.json, and the three animated GIFs used by the TwitchEmote tests. All names, ids and messages are made up.

Usage: python3 chat_fixtures.py [output dir]   (default: TestData/Chat next to this script's folder)
Requires: Pillow (pip install pillow)
"""
from __future__ import annotations

import base64
import io
import json
import re
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw

JsonDict = dict[str, Any]
Rgb = tuple[int, int, int]
# A message segment is plain text or ("emote", <first-party emote name>)
Segment = str | tuple[str, str]
# (offset seconds, display name, segments, badges as (name, version), user notice msg_id)
CommentSpec = tuple[float, str, list[Segment], list[tuple[str, str]], str | None]

VIDEO_ID = "1000000001"
STREAMER: JsonDict = {"name": "TestStreamer", "login": "teststreamer", "id": 100000001}
START = datetime(2024, 1, 1, 0, 0, 0, tzinfo=timezone.utc)
RGB: list[Rgb] = [(255, 0, 0), (0, 255, 0), (0, 0, 255)]
PALETTE: list[Rgb] = [(220, 20, 60), (65, 105, 225), (46, 139, 87), (255, 140, 0), (128, 0, 128), (0, 128, 128), (184, 134, 11), (112, 128, 144)]

# display name -> (id, login, chat color)
USERS: dict[str, tuple[str, str, str | None]] = {
    "TestStreamer": ("100000001", "teststreamer", "#9146FF"),
    "ModUser": ("100000002", "moduser", "#00FF7F"),
    "VipUser": ("100000003", "vipuser", "#FF69B4"),
    "SubUser": ("100000004", "subuser", "#1E90FF"),
    "PrimeUser": ("100000005", "primeuser", None),
    "GifterUser": ("100000006", "gifteruser", "#DAA520"),
    "BitsUser": ("100000007", "bitsuser", "#FF4500"),
    "RaiderUser": ("100000008", "raideruser", "#8A2BE2"),
    "viewer_one": ("100000011", "viewer_one", None),
    "viewer_two": ("100000012", "viewer_two", "#2E8B57"),
    "viewer_three": ("100000013", "viewer_three", "#B22222"),
    "テスト視聴者": ("100000014", "jp_viewer", "#5F9EA0"),  # CJK display name
}

EMOTES_1P: dict[str, str] = {"Kappa": "25", "LUL": "425618", "TestAnimated": "emotesv2_0000000000000000000000000000000a"}
EMOTES_3P: dict[str, str] = {"TestBttv": "000000000000000000000b01", "TestFfz": "100001", "TestStv": "01TESTSTVEMOTE0000000000S1",
                             "TestZeroWidth": "01TESTSTVEMOTE0000000000Z1"}

COMMENTS: list[CommentSpec] = [
    (1.0, "TestStreamer", ["Welcome to the stream everyone!"], [("broadcaster", "1"), ("subscriber", "0")], None),
    (3.5, "viewer_one", ["hello chat"], [], None),
    (5.0, "ModUser", [("emote", "Kappa"), " hi everyone"], [("moderator", "1")], None),
    (8.25, "SubUser", [("emote", "LUL"), " that was funny ", ("emote", "LUL")], [("subscriber", "3")], None),
    (10.0, "VipUser", ["TestBttv TestFfz TestStv"], [("vip", "1")], None),
    (12.0, "viewer_two", ["TestStv TestZeroWidth stacked"], [], None),
    (15.0, "BitsUser", ["Cheer100 great stream"], [("bits", "100")], None),
    (18.0, "BitsUser", ["Cheer1000 Cheer1 take my bits"], [("bits", "100")], None),
    (20.0, "SubUser", ["SubUser subscribed at Tier 1. They've subscribed for 3 months! Hello again"], [("subscriber", "3")], None),
    (23.0, "PrimeUser", ["PrimeUser subscribed with Prime. They've subscribed for 2 months! "], [("premium", "1"), ("subscriber", "0")], None),
    (26.0, "GifterUser", ["GifterUser gifted a Tier 1 sub to viewer_one! "], [("sub-gifter", "5")], None),
    (27.0, "GifterUser", ["GifterUser is gifting 5 Tier 1 Subs to TestStreamer's community! They've gifted a total of 10 in the channel! "],
     [("sub-gifter", "5")], None),
    (30.0, "RaiderUser", ["12 raiders from RaiderUser have joined! "], [], None),
    (33.0, "viewer_three", ["this message is highlighted"], [], "highlighted-message"),
    (36.0, "テスト視聴者", ["こんにちは \U0001F600"], [], None),
    (39.0, "viewer_two", ["\U0001F389\U0001F389 party time"], [], None),
    (42.0, "viewer_one", ['<b>not bold</b> & "quoted"'], [], None),
    (45.0, "ModUser", [("emote", "TestAnimated"), " animated emote test"], [("moderator", "1")], None),
    (48.0, "viewer_three", ["This is a deliberately long chat message that should wrap across several lines when it is rendered so that line "
                            "wrapping code paths get exercised by the renderer tests too."], [], None),
    (52.0, "TestStreamer", ["thanks for the raid ", ("emote", "Kappa")], [("broadcaster", "1"), ("subscriber", "0")], None),
    (55.5, "viewer_two", ["pog"], [], None),
    (60.0, "VipUser", ["TestBttv"], [("vip", "1")], None),
    (64.0, "viewer_one", ["what game is this?"], [], None),
    (68.0, "ModUser", ["!commands"], [("moderator", "1")], None),
    (72.0, "SubUser", ["GG"], [("subscriber", "3")], None),
    (78.0, "viewer_three", ["lol"], [], None),
    (85.0, "BitsUser", ["one more Cheer100"], [("bits", "100")], None),
    (93.0, "viewer_two", ["brb"], [], None),
    (101.0, "PrimeUser", ["back"], [("premium", "1"), ("subscriber", "0")], None),
    (110.0, "viewer_one", [("emote", "LUL")], [], None),
    (118.75, "TestStreamer", ["That is all for today, bye!"], [("broadcaster", "1"), ("subscriber", "0")], None),
]

BADGES: list[tuple[str, dict[str, str]]] = [
    ("broadcaster", {"1": "Broadcaster"}),
    ("moderator", {"1": "Moderator"}),
    ("vip", {"1": "VIP"}),
    ("subscriber", {"0": "Subscriber", "3": "3-Month Subscriber"}),
    ("premium", {"1": "Prime Gaming"}),
    ("bits", {"100": "cheer 100"}),
    ("sub-gifter", {"5": "5 Gift Subs"}),
]


def png(width: int, height: int, color: Rgb, mark: bool = False) -> bytes:
    """A two-colour palette PNG: solid `color`, optionally with a white square in the middle."""
    image = Image.new("P", (width, height))
    image.putpalette(list(color) + [255, 255, 255])
    if mark:
        ImageDraw.Draw(image).rectangle((width // 4, height // 4, width * 3 // 4 - 1, height * 3 // 4 - 1), fill=1)
    out = io.BytesIO()
    image.save(out, "PNG", optimize=True)
    return out.getvalue()


def gif(width: int, height: int, colors: list[Rgb], delays_cs: list[int]) -> bytes:
    """An animated GIF with one solid frame per colour and the given per-frame delays in centiseconds."""
    frames = []
    for color in colors:
        frame = Image.new("P", (width, height))
        frame.putpalette(list(color) + [0, 0, 0])
        frames.append(frame)
    out = io.BytesIO()
    frames[0].save(out, "GIF", save_all=True, append_images=frames[1:], duration=[d * 10 for d in delays_cs], loop=0, disposal=1,
                   optimize=False)
    return out.getvalue()


def b64(data: bytes) -> str:
    return base64.b64encode(data).decode()


def iso(dt: datetime) -> str:
    return dt.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"


def bits_of(body: str) -> int:
    match = re.search(r"(?:(?<=\s)|^)Cheer([1-9]\d{0,6})(?=\s|$)", body)
    return int(match.group(1)) if match else 0


def emote_entry(emote_id: str, name: str | None, data: bytes, zero_width: bool | None = None, width: int = 28, height: int = 28) -> JsonDict:
    entry: JsonDict = {"id": emote_id, "imageScale": 2, "data": b64(data), "name": name, "width": width, "height": height}
    if zero_width is not None:
        entry["isZeroWidth"] = zero_width
    return entry


def write_json(path: Path, data: JsonDict) -> None:
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write("\n")


def sample_chat() -> JsonDict:
    comments: list[JsonDict] = []
    for i, (offset, user, segments, badges, notice) in enumerate(COMMENTS):
        user_id, login, color = USERS[user]
        body = ""
        fragments: list[JsonDict] = []
        emoticons: list[JsonDict] = []
        for segment in segments:
            if isinstance(segment, tuple):
                name = segment[1]
                emote_id = EMOTES_1P[name]
                emoticons.append({"_id": emote_id, "begin": len(body), "end": len(body) + len(name) + 1})
                fragments.append({"text": name, "emoticon": {"emoticon_id": emote_id}})
                body += name
            else:
                fragments.append({"text": segment, "emoticon": None})
                body += segment
        message: JsonDict = {
            "body": body,
            "bits_spent": bits_of(body),
            "fragments": fragments,
            "user_badges": [{"_id": b, "version": v} for b, v in badges],
            "user_color": color,
        }
        if notice:
            message["user_notice_params"] = {"msg_id": notice}
        message["emoticons"] = emoticons
        comments.append({
            "_id": f"00000000-0000-4000-8000-{i + 1:012d}",
            "created_at": iso(START + timedelta(seconds=offset)),
            "channel_id": str(STREAMER["id"]),
            "content_type": "video",
            "content_id": VIDEO_ID,
            "content_offset_seconds": offset,
            "commenter": {
                "display_name": user, "_id": user_id, "name": login, "bio": None,
                "created_at": "2020-01-01T00:00:00Z", "updated_at": "2024-01-01T00:00:00Z", "logo": None,
            },
            "message": message,
        })

    third_party = [emote_entry(emote_id, name, png(56, 56, PALETTE[i % len(PALETTE)], mark=True), zero_width=(name == "TestZeroWidth"))
                   for i, (name, emote_id) in enumerate(EMOTES_3P.items())]
    first_party = [
        emote_entry("25", None, png(56, 56, (100, 100, 100), mark=True)),
        emote_entry("425618", None, png(56, 56, (200, 160, 0), mark=True)),
        emote_entry("emotesv2_0000000000000000000000000000000a", None, gif(56, 56, RGB, [10, 10, 10])),
    ]
    badge_sets = [{"name": name, "versions": {
        version: {"title": title, "description": title, "bytes": b64(png(36, 36, PALETTE[(i + j) % len(PALETTE)], mark=True))}
        for j, (version, title) in enumerate(versions.items())}}
        for i, (name, versions) in enumerate(BADGES)]
    bits = [{"prefix": "Cheer", "tierList": {
        str(tier): emote_entry(f"Cheer{tier}", f"Cheer{tier}", png(56, 56, PALETTE[k], mark=True)) for k, tier in enumerate([1, 100, 1000])}}]

    return {
        "FileInfo": {"Version": {"Major": 1, "Minor": 4, "Patch": 0}, "CreatedAt": "2024-01-02T00:00:00Z", "UpdatedAt": "0001-01-01T00:00:00"},
        "streamer": STREAMER,
        "video": {
            "title": "Sample synthetic stream", "description": "Synthetic chat used by unit tests", "id": VIDEO_ID,
            "created_at": "2024-01-01T00:00:00Z", "start": 0, "end": 120, "length": 120, "viewCount": 42,
            "game": "Software and Game Development",
            "chapters": [{"id": "", "startMilliseconds": 0, "lengthMilliseconds": 120000, "type": "GAME_CHANGE",
                          "description": "Software and Game Development", "subDescription": "", "thumbnailUrl": None,
                          "gameId": "1469308723", "gameDisplayName": "Software and Game Development",
                          "gameBoxArtUrl": "https://static-cdn.jtvnw.net/ttv-boxart/1469308723_IGDB-40x53.jpg"}],
        },
        "comments": comments,
        "embeddedData": {"thirdParty": third_party, "firstParty": first_party, "twitchBadges": badge_sets, "twitchBits": bits},
    }


def small_comment(i: int, offset: float, user: str, user_id: str, login: str, body: str, badges: list[tuple[str, str]] | None = None,
                  emote: tuple[str, str] | None = None, logo: str | None = None) -> JsonDict:
    """A comment for the older-schema fixtures; `emote` is (name, id) of a first-party emote at the start of `body`."""
    fragments: list[JsonDict] = [{"text": body, "emoticon": None}]
    emoticons: list[JsonDict] = []
    if emote:
        fragments = [{"text": emote[0], "emoticon": {"emoticon_id": emote[1]}}, {"text": body[len(emote[0]):], "emoticon": None}]
        emoticons = [{"_id": emote[1], "begin": 0, "end": len(emote[0]) + 1}]
    return {
        "_id": f"00000000-0000-4000-9000-{i:012d}",
        "created_at": iso(START + timedelta(seconds=offset)),
        "channel_id": "100000001",
        "content_type": "video",
        "content_id": VIDEO_ID,
        "content_offset_seconds": offset,
        "commenter": {"display_name": user, "_id": user_id, "name": login, "bio": None,
                      "created_at": "2020-01-01T00:00:00Z", "updated_at": "2024-01-01T00:00:00Z", "logo": logo},
        "message": {"body": body, "bits_spent": 0, "fragments": fragments,
                    "user_badges": [{"_id": b, "version": v} for b, v in badges or []], "user_color": "#1E90FF", "emoticons": emoticons},
    }


def older_schema_chats() -> dict[str, JsonDict]:
    small_png = png(56, 56, (65, 105, 225), mark=True)
    badge_png = png(36, 36, (220, 20, 60), mark=True)
    v130: JsonDict = {
        "FileInfo": {"Version": {"Major": 1, "Minor": 3, "Patch": 0}, "CreatedAt": "2023-07-23T13:11:54.4269559-04:00",
                     "UpdatedAt": "0001-01-01T00:00:00"},
        # 1.3.0 files did not store the streamer login
        "streamer": {"name": "TestStreamer", "id": 100000001},
        "video": {"title": "Schema 1.3.0 fixture", "id": VIDEO_ID, "created_at": "2024-01-01T00:00:00Z", "start": 0, "end": 60, "length": 60,
                  "chapters": []},
        "comments": [
            small_comment(1, 2.0, "viewer_one", "100000011", "viewer_one", "Kappa hello", badges=[("subscriber", "0")], emote=("Kappa", "25")),
            small_comment(2, 30.5, "viewer_two", "100000012", "viewer_two", "TestBttv nice"),
            small_comment(3, 59.0, "viewer_one", "100000011", "viewer_one", "bye"),
        ],
        "embeddedData": {
            "thirdParty": [emote_entry("000000000000000000000b01", "TestBttv", small_png, zero_width=False)],
            "firstParty": [emote_entry("25", None, small_png)],
            "twitchBadges": [{"name": "subscriber", "versions": {"0": {"title": "Subscriber", "description": "Subscriber", "bytes": b64(badge_png)}}}],
            "twitchBits": [],
        },
    }
    v122: JsonDict = {
        "FileInfo": {"Version": {"Major": 1, "Minor": 2, "Patch": 2}, "CreatedAt": "2023-03-01T00:00:00Z", "UpdatedAt": "0001-01-01T00:00:00"},
        "streamer": {"name": "TestStreamer", "id": 100000001},
        "video": {"title": "Schema 1.2.2 fixture", "id": VIDEO_ID, "created_at": "2024-01-01T00:00:00Z", "start": 0, "end": 60, "length": 60,
                  "chapters": []},
        "comments": [
            small_comment(1, 2.0, "viewer_one", "100000011", "viewer_one", "hello", badges=[("subscriber", "0")]),
            small_comment(2, 40.0, "viewer_two", "100000012", "viewer_two", "bye", badges=[("subscriber", "3")]),
        ],
        "embeddedData": {
            "thirdParty": [],
            "firstParty": [emote_entry("25", None, small_png)],
            # Before 1.3.0 a badge version mapped straight to its image bytes
            "twitchBadges": [{"name": "subscriber", "versions": {"0": b64(badge_png), "3": b64(badge_png)}}],
            "twitchBits": [{"prefix": "Cheer", "tierList": {"1": emote_entry("Cheer1", "Cheer1", small_png)}}],
        },
    }
    # Pre-FileInfo chats (2020-2022): no FileInfo, no streamer, Twitch v5 video fields, "emotes" instead of "embeddedData",
    # 300x300 avatars and bits_spent never filled in.
    legacy: JsonDict = {
        "video": {"id": VIDEO_ID, "created_at": "2024-01-01T00:00:00Z", "start": 0, "end": 0,
                  "user_id": "100000001", "user_name": "TestStreamer", "duration": "1h2m3s"},
        "comments": [
            small_comment(1, 5.0, "TestStreamer", "100000001", "teststreamer", "welcome", badges=[("broadcaster", "1")],
                          logo="https://static-cdn.jtvnw.net/jtv_user_pictures/test-profile_image-300x300.png"),
            small_comment(2, 10.0, "viewer_one", "100000011", "viewer_one", "Cheer100 for you",
                          logo="https://static-cdn.jtvnw.net/jtv_user_pictures/test2-profile_image-300x300.png"),
            small_comment(3, 20.0, "viewer_two", "100000012", "viewer_two", "notCheer50 is not a cheer"),
        ],
        "emotes": {
            "thirdParty": [emote_entry("000000000000000000000b01", "TestBttv", small_png)],
            "firstParty": [emote_entry("25", None, small_png)],
        },
    }
    return {"chat_v1_3_0.json": v130, "chat_v1_2_2.json": v122, "chat_legacy_v5.json": legacy}


def main() -> None:
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent / "Chat"
    out.mkdir(parents=True, exist_ok=True)
    for name, delays in [("anim_delays_2_0_5.gif", [2, 0, 5]), ("anim_delays_all_0.gif", [0, 0, 0]), ("anim_delays_all_1.gif", [1, 1, 1])]:
        (out / name).write_bytes(gif(8, 6, RGB, delays))
    write_json(out / "sample_chat_small.json", sample_chat())
    for name, chat in older_schema_chats().items():
        write_json(out / name, chat)
    print(f"Wrote {out}")


if __name__ == "__main__":
    main()
