# TwitchDownloaderCLI
A cross platform command line tool that can do the main functions of the GUI program, which can download VODs/Clips/Chats and render chats.
Also can concatenate/combine/merge Transport Stream files, either those parts downloaded with the CLI itself or from another source.

- [TwitchDownloaderCLI](#twitchdownloadercli)
  - [Global arguments](#global-arguments)
  - [Arguments for mode videodownload](#arguments-for-mode-videodownload)
  - [Arguments for mode clipdownload](#arguments-for-mode-clipdownload)
  - [Arguments for mode chatdownload](#arguments-for-mode-chatdownload)
  - [Arguments for mode chatupdate](#arguments-for-mode-chatupdate)
  - [Arguments for mode chatrender](#arguments-for-mode-chatrender)
  - [Arguments for mode ffmpeg](#arguments-for-mode-ffmpeg)
  - [Arguments for mode cache](#arguments-for-mode-cache)
  - [Arguments for mode tsmerge](#arguments-for-mode-tsmerge)
  - [Example Commands](#example-commands)
  - [Additional Notes](#additional-notes)
    - [ID parsing](#id-parsing)
    - [String arguments](#string-arguments)
    - [Boolean flags](#boolean-flags)
    - [Enum flag arguments](#enum-flag-arguments)
    - [Time durations](#time-durations)
    - [Rendering prerequisites](#rendering-prerequisites)
    - [TSMerge notes](#tsmerge-notes)

---

## Global arguments
#### Common arguments shared between all the modes

**--banner**
(Default: `true`) Displays a banner containing version and copyright information.

**--log-level**
(Default: `Status,Info,Warning,Error`) Sets the log level flags. Applicable values are: `None`, `Status`, `Verbose`, `Info`, `Warning`, `Error`, `Ffmpeg`. When `None` is passed, any other log level flags are ignored and `--banner=false` is implied.

## Arguments for mode videodownload
#### Downloads a stream VOD or highlight from Twitch

**-u / --id (REQUIRED)**
The ID or URL of the VOD to download.

**-o / --output (REQUIRED)**
File the program will output to. File extension will be used to determine download type. Valid extensions are: `.mp4` and `.m4a`.

**-q / --quality**
The quality that the program will attempt to download, for example "1080p60". If not found, the highest quality stream will be downloaded.

**-b / --beginning**
Time to trim beginning. See [Time durations](#time-durations) for a more detailed explanation.

**-e / --ending**
Time to trim ending. See [Time durations](#time-durations) for a more detailed explanation.

**-t / --threads**
(Default: `4`) Number of download threads.

**--bandwidth**
(Default: `-1`) The maximum bandwidth a thread will be allowed to use in kibibytes per second (KiB/s), or `-1` for no maximum.

**--oauth**
OAuth access token to download subscriber only VODs. <ins>**DO NOT SHARE YOUR OAUTH TOKEN WITH ANYONE.**</ins>

**--ffmpeg-path**
Path to FFmpeg executable.

**--temp-path**
Path to temporary folder for cache.

## Arguments for mode clipdownload
#### Downloads a clip from Twitch

**-u / --id (REQUIRED)**
The ID or URL of the Clip to download.

**-o / --output (REQUIRED)**
File the program will output to.

**-q / --quality**
The quality that the program will attempt to download, for example "1080p60". If not found, the highest quality video will be downloaded.

**--bandwidth**
(Default: `-1`) The maximum bandwidth the clip downloader is allowed to use in kibibytes per second (KiB/s), or `-1` for no maximum.

**--encode-metadata**
(Default: `true`) Uses FFmpeg to add metadata to the clip output file.

**--ffmpeg-path**
Path to FFmpeg executable.

**--temp-path**
Path to temporary folder for cache.

## Arguments for mode chatdownload
#### Downloads the chat of a VOD, highlight, or clip

**-u / --id (REQUIRED)**
The ID or URL of the VOD or clip to download.

**-o / --output (REQUIRED)**
File the program will output to. File extension will be used to determine download type. Valid extensions are: `.json`, `.html`, and `.txt`.

**--compression**
(Default: `None`) Compresses an output json chat file using a specified compression, usually resulting in 40-90% size reductions. Valid values are: `None`, `Gzip`. More formats will be supported in the future.

**-b / --beginning**
Time to trim beginning. See [Time durations](#time-durations) for a more detailed explanation.

**-e / --ending**
Time to trim ending. See [Time durations](#time-durations) for a more detailed explanation.

**-E / --embed-images**
(Default: `false`) Embed first party emotes, badges, and cheermotes into the download file for offline rendering. Useful for archival purposes, file size will be larger.

**--bttv**
(Default: `true`) BTTV emote embedding. Requires `-E / --embed-images`.

**--ffz**
(Default: `true`) FFZ emote embedding. Requires `-E / --embed-images`.

**--stv**
(Default: `true`) 7TV emote embedding. Requires `-E / --embed-images`.

**--timestamp-format**
(Default: `Relative`) Sets the timestamp format for .txt chat logs. Valid values are: `Utc`, `UtcFull`, `Relative`, and `None`.

**--chat-connections**
(Default: `4`) The number of parallel downloads for chat.

**--temp-path**
Path to temporary folder for cache.

## Arguments for mode chatupdate
#### Updates the embedded emotes, badges, bits, and trims a chat JSON and/or converts a JSON chat to another format

**-i / --input (REQUIRED)**
Path to input file. Valid extensions are: `.json`, `.json.gz`.

**-o / --output (REQUIRED)**
Path to output file. File extension will be used to determine new chat type. Valid extensions are: `.json`, `.html`, and `.txt`.

**-c / --compression**
(Default: `None`) Compresses an output json chat file using a specified compression, usually resulting in 40-90% size reductions. Valid values are: `None`, `Gzip`. More formats will be supported in the future.

**-E / --embed-missing**
(Default: `false`) Embed missing emotes, badges, and cheermotes. Already embedded images will be untouched.

**-R / --replace-embeds**
(Default: `false`) Replace all embedded emotes, badges, and cheermotes in the file. All embedded data will be overwritten!

**b / --beginning**
(Default: `-1`) New time for chat beginning (`-1` = keep current trim). See [Time durations](#time-durations) for a more detailed explanation. Comments may be added but not removed.

**-e / --ending**
(Default: `-1`) New time for chat ending (`-1` = keep current trim). See [Time durations](#time-durations) for a more detailed explanation. Comments may be added but not removed.

**--bttv**
(Default: `true`) Enable embedding BTTV emotes.

**--ffz**
(Default: `true`) Enable embedding FFZ emotes.

**--stv**
(Default: `true`) Enable embedding 7TV emotes.

**--timestamp-format**
(Default: `Relative`) Sets the timestamp format for .txt chat logs. Valid values are: `Utc`, `Relative`, and `None`.

**--temp-path**
Path to temporary folder for cache.

## Arguments for mode chatrender
#### Renders a chat JSON as a video

**-i / --input (REQUIRED)**
The path to the `.json` or `.json.gz` chat file input.

**-o / --output (REQUIRED)**
File the program will output to.

**--background-color**
(Default: `#111111`) The render background color in the string format of `#RRGGBB` or `#AARRGGBB` in hexadecimal.

**--alt-background-color**
(Default: `#191919`) The alternate message background color in the string format of `#RRGGBB` or `#AARRGGBB` in hexadecimal. Requires `--alternate-backgrounds`.

**--message-color**
(Default: `#ffffff`) The message text color in the string format of `#RRGGBB` or `#AARRGGBB` in hexadecimal.

**-w / --chat-width**
(Default: `350`) Width of chat render.

**-h / --chat-height**
(Default: `600`) Height of chat render.

**-b / --beginning**
(Default: `-1`) Time to trim the beginning of the render (`-1` = keep current trim). See [Time durations](#time-durations) for a more detailed explanation.

**-e / --ending**
(Default: `-1`) Time to trim the ending of the render (`-1` = keep current trim). See [Time durations](#time-durations) for a more detailed explanation.

**--bttv**
(Default: `true`) Enable BTTV emotes.

**--ffz**
(Default: `true`) Enable FFZ emotes.

**--stv**
(Default: `true`) Enable 7TV emotes.

**--allow-unlisted-emotes**
(Default: `true`) Allow unlisted 7TV emotes in the render.

**--sub-messages**
(Default: `true`) Enable sub / re-sub messages.

**--badges**
(Default: `true`) Enable chat badges.

**--outline**
(Default: `false`) Enable outline around chat messages.

**--outline-size**
(Default: `4`) Size of outline if outline is enabled.

**-f / --font**
(Default: `Inter Embedded`) Font to use.

**--font-size**
(Default: `12`) Font size.

**--message-fontstyle**
(Default: `normal`) Font style of message. Valid values are **normal**, **bold**, and **italic**.

**--username-fontstyle**
(Default: `bold`) Font style of username. Valid values are **normal**, **bold**, and **italic**.

**--timestamp**
(Default: `false`) Enables timestamps to the left of messages, similar to VOD chat on Twitch.

**--generate-mask**
(Default: `false`) Generates a mask file of the chat in addition to the rendered chat.

**--sharpening**
(Default: `false`) Appends `-filter_complex "smartblur=lr=1:ls=-1.0"` to the `input-args`. Works best with `font-size` 24 or larger.

**--framerate**
(Default: `30`) Framerate of the render.

**--update-rate**
(Default: `0.2`) Time in seconds to update chat render output.

**--input-args**
(Default: `-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -`) Input arguments for FFmpeg chat render.

**--output-args**
(Default: `-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p "{save_path}"`) Output arguments for FFmpeg chat render.

**--ignore-users**
(Default: ` `) List of usernames to ignore when rendering, separated by commas. Not case-sensitive.

**--ban-words**
(Default: ` `) List of words or phrases to ignore when rendering, separated by commas. Not case-sensitive.

**--badge-filter**
(Default: `0`) Bitmask of types of Chat Badges to filter out. Add the numbers of the types of badges you want to filter. For example, to filter out Moderator and Broadcaster badges only enter the value of 6.

Other = `1`, Broadcaster = `2`, Moderator = `4`, VIP = `8`, Subscriber = `16`, Predictions = `32`, NoAudioVisual = `64`, PrimeGaming = `128`

**--dispersion**
(Default: `false`) In November 2022 a Twitch API change made chat messages download only in whole seconds. This option uses additional metadata to attempt to restore messages to when they were actually sent. This may result in a different comment order. Requires an update rate less than 1.0 for effective results.

**--alternate-backgrounds**
(Default: `false`) Alternates the background color of every other chat message to help tell them apart.

**--offline**
(Default: `false`) Render completely offline using only embedded emotes, badges, and bits from the input json.

**--emoji-vendor**
(Default: `notocolor`) The emoji vendor used for rendering emojis. Valid values are: `twitter` / `twemoji`, `google` / `notocolor`, `none`.

**--ffmpeg-path**
(Default: ` `) Path to FFmpeg executable.

**--temp-path**
(Default: ` `) Path to temporary folder for cache.

**--skip-drive-waiting**
(Default: `false`) Do not wait for the output drive to transmit a ready signal before writing the next frame. Waiting is usually only necessary on low-end USB drives. Skipping can result in 1-5% render speed increases.

**--scale-emote**
(Default: `1.0`) Number to scale emote images.

**--scale-badge**
(Default: `1.0`) Number to scale badge images.

**--scale-emoji**
(Default: `1.0`) Number to scale emoji images.

**--scale-vertical**
(Default: `1.0`) Number to scale vertical padding.

**--scale-side-padding**
(Default: `1.0`) Number to scale side padding.

**--scale-section-height**
(Default: `1.0`) Number to scale section height of comments.

**--scale-word-space**
(Default: `1.0`) Number to scale spacing between words.

**--scale-emote-space**
(Default: `1.0`) Number to scale spacing between emotes.

**--scale-highlight-stroke**
(Default: `1.0`) Number to scale highlight stroke size (sub messages).

**--scale-highlight-indent**
(Default: `1.0`) Number to scale highlight indent size (sub messages).

## Arguments for mode ffmpeg
#### Manage standalone FFmpeg

**-d / --download**
(Default: `false`) Downloads FFmpeg as a standalone file.

## Arguments for mode cache
#### Manage the working cache.

**-c / --clear**
(Default: `false`) Clears the default cache folder.

**--force-clear**
(Default: `false`) Clears the default cache folder, bypassing the confirmation prompt.

## Arguments for mode tsmerge
#### Concatenates multiple .ts/.tsv/.tsa/.m2t/.m2ts (MPEG Transport Stream) files into a single file

**-i / --input (REQUIRED)**
Path a text file containing the absolute paths of the files to concatenate, separated by newlines. M3U/M3U8 is also supported.

**-o / --output (REQUIRED)**
File the program will output to.

---

## Example Commands
#### Examples of typical TwitchDownloaderCLI use cases.

Note: Commands are formatted for unix systems (i.e. Mac, Linux). For usage on Windows, replace `./TwitchDownloaderCLI` with `TwitchDownloaderCLI.exe` (cmd) or `./TwitchDownloaderCLI.exe` (powershell).

Download a VOD with defaults

    ./TwitchDownloaderCLI videodownload --id 612942303 -o video.mp4

Download a small portion of a VOD

    ./TwitchDownloaderCLI videodownload --id 612942303 -b 0:01:40 -e 0:03:20 -o video.mp4

Download a Clip with defaults

    ./TwitchDownloaderCLI clipdownload --id NurturingCalmHamburgerVoHiYo -o clip.mp4

Download a Chat JSON with embedded emotes/badges from Twitch and emotes from Bttv

    ./TwitchDownloaderCLI chatdownload --id 612942303 --embed-images --bttv=true --ffz=false --stv=false -o chat.json

Download a Chat as plain text with timestamps

    ./TwitchDownloaderCLI chatdownload --id 612942303 --timestamp-format Relative -o chat.txt

Add embeds to a chat file that was downloaded without embeds

    ./TwitchDownloaderCLI chatupdate -i chat.json -o chat_embedded.json --embed-missing

Convert a JSON chat file to HTML

    ./TwitchDownloaderCLI chatupdate -i chat.json -o chat.html

Render a chat with defaults

    ./TwitchDownloaderCLI chatrender -i chat.json -o chat.mp4

Render a chat with custom video settings and message outlines

    ./TwitchDownloaderCLI chatrender -i chat.json -h 1440 -w 720 --framerate 60 --outline -o chat.mp4

Render a chat with custom FFmpeg arguments

    ./TwitchDownloaderCLI chatrender -i chat.json --output-args='-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p "{save_path}"' -o chat.mp4

Download a portable FFmpeg binary for your system

    ./TwitchDownloaderCLI ffmpeg --download

Clear the default TwitchDownloader cache folder

    ./TwitchDownloaderCLI cache --clear

Concatenate several ts files into a single output file

    TwitchDownloaderCLI tsmerge -i list.txt -o output.ts

Print the available operations

    ./TwitchDownloaderCLI help

Print the available options for the VOD downloader

    ./TwitchDownloaderCLI videodownload --help

---

## Additional Notes

### ID parsing
All `--id` inputs will accept either video/clip IDs or full video/clip URLs. i.e. `--id 612942303` or `--id https://twitch.tv/videos/612942303`.

### String arguments
String arguments that contain spaces should be wrapped in either single quotes <kbd>'</kbd> or double quotes <kbd>"</kbd> depending on your shell. i.e. `--output 'my output file.mp4'` or `--output "my output file.mp4"`

### Boolean flags
Default true boolean flags must be assigned: `--default-true-flag=false`. Default false boolean flags should still be raised normally: `--default-false-flag`.

### Enum flag arguments
Enum flag arguments may be assigned without spaces `--flag Value1,Value2,Value3` or with spaces when wrapped in quotes `--flag "Value1, Value2, Value3"` (see [String arguments](#string-arguments)).

### Time durations
Time duration arguments may be formatted in milliseconds `###ms`, seconds `###s`, minutes `###m`, hours `###h`, or [time](https://learn.microsoft.com/en-us/dotnet/api/system.timespan.parse?view=net-6.0) (i.e. `hh:mm:ss`, `hh:mm`, `dd.hh:mm:ss.ms`).
If the time duration is given as a number without a unit, seconds will be assumed. Decimals are supported.

"Beginning" arguments set when trimming begins. For example, `--beginning 17s` will make the output start 17 seconds after the source begins.

"Ending" arguments set when trimming ends. For example, `--ending 17s` will make the output end at 17 seconds after the source begins.

If `--beginning 17s` and `--ending 27s` are used together, the resulting output will be of seconds `17-27` of the source and will have a duration of 10 seconds.

### Rendering prerequisites
For Linux users, ensure both `fontconfig` and `libfontconfig1` are installed. `apt-get install fontconfig libfontconfig1` on Ubuntu.

Some distros, like Linux Alpine, lack fonts for some languages (Arabic, Persian, Thai, etc.) If this is the case for you, install additional fonts families such as [Noto](https://fonts.google.com/noto/specimen/Noto+Sans) or check your distro's wiki page on fonts as it may have an install command for this specific scenario, such as the [Linux Alpine](https://wiki.alpinelinux.org/wiki/Fonts) font page.

### TSMerge notes
The list file for `tsmerge` may contain relative or absolute paths, with one path per line.
Alternatively, the list file may also be an M3U8 playlist file.