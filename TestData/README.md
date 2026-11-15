# Test data

Every file the tests read lives here. The test projects copy this folder (minus `generators/` and this README) into their
output as `TestData/` (see the `<None Include="..\TestData\**" ...>` item in each test `.csproj`), and tests open files with
`TestData.Path("Chat", "sample_chat_small.json")` from `TwitchDownloaderCore.Tests/TestHelpers/TestData.cs`.

Everything is small and synthetic, except `Render/real_chat_60s.json.gz` (a real chat with anonymized commenters). Nothing here
is real video: the media files are ffmpeg test patterns.

## Where each file comes from

| File | What it is | Used by | How to regenerate |
|---|---|---|---|
| `Chat/sample_chat_small.json` | Schema 1.4.0 chat: 31 comments over 120 s with normal messages, first-party/BTTV/FFZ/7TV/zero-width emotes, cheers, subs, gifts, a raid, CJK text and emoji; every emote, badge and cheermote is embedded | `ChatJsonTests` and other chat tests, `render_preview.py` | `generators/chat_fixtures.py` |
| `Chat/chat_v1_3_0.json`, `chat_v1_2_2.json`, `chat_legacy_v5.json` | The same kind of chat in older file formats, for `ChatJson`'s upgrade code | `ChatJsonTests` | `generators/chat_fixtures.py` |
| `Chat/anim_delays_*.gif` | 8x6, 3-frame GIFs with specific frame delays | `TwitchEmoteTests` | `generators/chat_fixtures.py` |
| `CLI/info_*.json`, `CLI/info_video_playlist.m3u8` | Twitch GQL and playlist responses for the `info` command | `InfoHandlerTests`, `ProgramTests` | Hand-written; edit directly |
| `Downloaders/chat_update_input.json` | A "stale" chat (old title, 10-40 s trim, placeholder emote image) for `chatupdate` | `ChatUpdaterTests` | `generators/chat_update_input.py` |
| `Downloaders/vod_ts/0-2.ts` | Three consecutive 1 s MPEG-TS parts of one 64x36 stream with audio, served as a fake VOD | `FakeTwitch`, `VideoDownloaderTests` | `generators/media.py` |
| `Downloaders/vod_fmp4/` | A 2 s fragmented-MP4 VOD (`init-0.mp4`, `0.mp4`, `1.mp4`) | `VideoDownloaderTests` | `generators/media.py` |
| `Downloaders/clip.mp4` | A 1 s clip | `ClipDownloaderTests` and CLI clip tests | `generators/media.py` |
| `E2E/chat.json` | 10 s chat with one red first-party emote, one blue third-party emote and one green badge; empty video id so nothing calls Twitch | CLI end-to-end tests | `generators/e2e_chat.py` |
| `E2E/part0-2.ts` | Three consecutive 1 s video-only MPEG-TS parts (32x18) | `TsMergeTests` | `generators/media.py` |
| `Render/synthetic_chat.json` | 3 s chat whose embedded images are unique solid colours, so tests can find each one in a rendered frame | Render tests (`RenderHarness`) | `generators/render_chat.py`; keep its colours in sync with `FixtureColors` in `RenderHarness.cs` |
| `Render/real_chat_60s.json.gz` | First 60 s of a real highlight chat (559 messages, real emotes and badges), commenters anonymized | The `/render-preview` PR comment | `generators/real_chat.py` |

## Regenerating

Run the generators from anywhere; by default each one writes its files into this folder. Use `python3` (on Windows, `py -3`
or `python`). They need Python 3.10 or newer.

```sh
python3 TestData/generators/chat_fixtures.py       # needs Pillow:  python3 -m pip install pillow
python3 TestData/generators/render_chat.py         # needs Pillow
python3 TestData/generators/e2e_chat.py
python3 TestData/generators/chat_update_input.py
python3 TestData/generators/media.py               # needs ffmpeg on PATH
python3 TestData/generators/real_chat.py --cli <path to a built TwitchDownloaderCLI>   # downloads from Twitch
```

- The chat generators are deterministic: running one without changing it rewrites identical files, so `git diff` shows exactly
  what your change to a generator did.
- `media.py` output depends on the ffmpeg version, so regenerated media differs byte-wise but is equivalent for the tests.
- `real_chat.py` downloads whatever Twitch returns today, so a re-download is similar but not identical. To only re-anonymize a chat
  you already have, use `--input chat.json`. If the highlight ever disappears, pass `--video-id` with another long-lived video.

After regenerating, run the tests (see `CLAUDE.md`) and commit the data together with the generator change.

## Adding a fixture

- Prefer extending a generator over committing a hand-made file, so the next person can change it.
- Keep files small (a few KB; chats with embedded images up to a few tens of KB) and never commit real video.
- Anonymize anything that comes from real users.
- Add new binary file types to `.gitattributes` in this folder so line-ending conversion never touches them.
- Add a row to the table above.
