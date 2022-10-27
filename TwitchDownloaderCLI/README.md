
### TwitchDownloaderCLI
A cross platform command line tool that can do the main functions of the GUI program, which can download VODs/Clips/Chats and render chats.

 - [Global Arguments](#global-arguments)
 - [Arguments for mode VideoDownload](#arguments-for-mode-videodownload)
 - [Arguments for mode ClipDownload](#arguments-for-mode-clipdownload)
 - [Arguments for mode ChatDownload](#arguments-for-mode-chatdownload)
 - [Arguments for mode ChatRender](#arguments-for-mode-chatrender)
 - [Example Commands](#example-commands)
 - [Notes](#notes)

## Global Arguments
**-m/-\-mode (REQUIRED)**
Set the run mode for the program. Valid values are **VideoDownload**, **ClipDownload**, **ChatDownload**, and **ChatRender**.

**-o/-\-output (REQUIRED)**
File the program will output to.

**-\-download-ffmpeg**
Downloads ffmpeg and exits.

**-\-ffmpeg-path**
Path to ffmpeg executable.

**-\-temp-path**
Path to temporary folder for cache.

**-\-clear-cache**
Clears the default cache folder.

## Arguments for mode VideoDownload
**-u/-\-id**
The ID of the VOD to download, currently only accepts Integer IDs and will accept URLs in the future.

**-q/-\-quality**
The quality the program will attempt to download, for example "1080p60", if not found will download highest quality stream.

**-b/-\-beginning**
Time in seconds to crop beginning. For example if I had a 10 second stream but only wanted the last 7 seconds of it I would use `-b 3` to skip the first 3 seconds.

**-e/-\-ending**
Time in seconds to crop ending. For example if I had a 10 second stream but only wanted the first 4 seconds of it I would use `-e 4` to end on the 4th second.
Extra example, if I wanted only seconds 3-6 in a 10 second stream I would do `-b 3 -e 6`

**-t/-\-threads**
(Default: 10) Number of download threads.

**-\-oauth**
OAuth to be passed when downloading a VOD. Used when downloading sub only VODs.
## Arguments for mode ClipDownload
**-u/-\-id**
The ID of the Clip to download, currently only accepts the string identifier and will accept URLs in the future.

**-q/-\-quality**
The quality the program will attempt to download, for example "1080p60", if not found will download highest quality video.
## Arguments for mode ChatDownload
**IMPORTANT NOTE**: If output file argument does not end in .json or .html it will download it as a plain text file

**-u/-\-id**
The ID of the VOD or clip to download. Does not currently accept URLs.

**-b/-\-beginning**
Time in seconds to crop beginning. For example if I had a 10 second stream but only wanted the last 7 seconds of it I would use `-b 3` to skip the first 3 seconds.

**-e/-\-ending**
Time in seconds to crop ending. For example if I had a 10 second stream but only wanted the first 4 seconds of it I would use `-e 4` to end on the 4th second.

**-\-timestamp-format**
(Default: Relative) Sets the timestamp format for .txt chat logs. Valid values are Utc, Relative, and None.

**-\-embed-emotes**
(Default: false) Embeds emotes into the JSON file so in the future when an emote is removed from Twitch or a 3rd party, it will still render correctly. Useful for archival purposes, file size will be larger.

**-\-bttv**
(Default: true) BTTV emote embedding. Requires `-\-embed-emotes`.

**-\-ffz**
(Default: true) FFZ emote embedding. Requires `-\-embed-emotes`.

**-\-stv**
(Default: true) 7TV emote embedding. Requires `-\-embed-emotes`.

**-\-chat-connections**
(Default: 4) The number of parallel downloads for chat.
## Arguments for mode ChatRender
**-i/-\-input**
Path to JSON chat file input.

**-\-background-color**
(Default: #111111) Color of background in HEX string format.

**-\-message-color**
(Default: #ffffff) Color of messages in HEX string format.

**-h/-\-chat-height**
(Default: 600) Height of chat render.

**-w/-\-chat-width**
(Default: 350) Width of chat render.

**-\-bttv**
(Default: true) Enable BTTV emotes.

**-\-ffz**
(Default: true) Enable FFZ emotes.

**-\-stv**
(Default: true) Enable 7TV emotes.

**-\-sub-messages**
(Default: true) Enable sub/re-sub messages.

**-\-badges**
(Default: true) Enable chat badges.

**-\-outline**
(Default: false) Enable outline around chat messages.

**-\-outline-size**
(Default: 4) Size of outline if outline is enabled.

**-f/-\-font**
(Default: Inter Embedded) Font to use.

**-\-font-size**
(Default: 12) Size of font.

**-\-message-fontstyle**
(Default: normal) Font style to use for message. Valid values are **normal**, **bold**, and **italic**.

**-\-username-fontstyle**
(Default: bold) Font style to use for username. Valid values are **normal**, **bold**, and **italic**.

**-\-timestamp**
(Default: false) Enables timestamps to left of messages, similar to VOD chat on Twitch.

**-\-generate-mask**
(Default: false) Generates a mask file in addition to the regular chat file.

**-\-framerate**
(Default: 30) Framerate of chat render output.

**-\-update-rate**
(Default: 0.2) Time in seconds to update chat render output.

**-\-input-args**
 (Default: -framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -) Input arguments for ffmpeg chat render.

**-\-output-args**
(Default: -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p "{save_path}") Output arguments for ffmpeg chat render.

**-\-ignore-users**
(Default: ) List of usernames to ignore when rendering, separated by commas.

**-\-badge-filter**
(Default: 0) Bitmask of types of Chat Badges to filter out. Add the numbers of the types of badges you want to filter. For example, to filter out Moderator and Broadcaster badges only enter the value of 6.

Other = 1
Broadcaster = 2
Moderator = 4
VIP = 8
Subscriber = 16
Predictions = 32
NoAudioVisual = 64
PrimeGaming = 128

## Example Commands
Download a VOD

    TwitchDownloaderCLI -m VideoDownload --id 612942303 -o video.mp4
Download a Clip

    TwitchDownloaderCLI -m ClipDownload --id NurturingCalmHamburgerVoHiYo -o clip.mp4
Download a Chat (plain text with timestamps)

    TwitchDownloaderCLI -m ChatDownload --id 612942303 --timestamp-format Relative -o chat.txt
Download a Chat (JSON with embeded emotes from Twitch and Bttv)

    TwitchDownloaderCLI -m ChatDownload --id 612942303 --embed-emotes --bttv=true --ffz=false --stv=false -o chat.json
Render a chat with defaults

    TwitchDownloaderCLI -m ChatRender -i chat.json -o chat.mp4
Render a chat with different heights and values

    TwitchDownloaderCLI -m ChatRender -i chat.json -h 1440 -w 720 --framerate 60 --outline -o chat.mp4
Render a chat with custom ffmpeg arguments

    TwitchDownloaderCLI -m ChatRender -i chat.json --output-args='-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p "{save_path}"' -o chat.mp4

## Notes

Default true boolean flags must be assigned as `--true-bool=false`. Default false boolean flags must be raised normally `--false-bool`