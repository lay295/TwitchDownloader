
### TwitchDownloader
A Twitch VOD/Clip/Chat downloader I wrote, as well as a chat render feature.

**THIS USES UNDOCUMENTED API ENDPOINTS, MAY BREAK EASILY. I'LL TRY AND UPDATE WHEN IT DOES.**
![](https://i.imgur.com/BmGqYbm.gif)

## Chat render example
![](https://i.imgur.com/I4Z2bWo.gif)

## Video Demonstration (older version, same concept)
https://www.youtube.com/watch?v=0W3MhfhnYjk

## What can this do?
- Can download Twitch VODs
- Can download Twitch Clips
- Can download chat for VODS and Clips, in either a [JSON with all the information](https://pastebin.com/raw/YDgRe6X4) or a [simple text file](https://pastebin.com/raw/016azeQX)
- Can use a previously generated JSON chat file, to render the chat with FFZ and BTTV support (including GIFS)

## Things still needed to be done
- Fix bugs that slipped by
- More options for chat rendering

## Linux? MacOS?
Sorry the GUI version is only avaliable for Windows :( but there is a command line version avaliable.
This is a cross platform client that can do the main functions of the program without a GUI. It works on Windows and Linux, haven't tested it on MacOS though. 

[Some documentation here](https://github.com/lay295/TwitchDownloader/blob/master/TwitchDownloaderCLI/README.md), for example, you could copy/paste this into a .bat file on Windows and you can download a VOD, download chat, then render it in a single go. I've never really made a command line utility before, so things may change in the future. If you're on Linux, make sure fontconfig and libfontconfig1 are installed. (apt-get install fontconfig libfontconfig1)
```
@echo off
set /p vodid="Enter VOD ID: "
TwitchDownloaderCLI -m VideoDownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI -m ChatDownload --id %vodid% -o %vodid%_chat.json
TwitchDownloaderCLI -m ChatRender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```
=======

### Linux - Getting started

1. Go to releases and download the latest version for Linux
2. Extract the `TwitchDownloaderCLI`
3. Browse to where you extracted the file and give it executable rights by opening a terminal and executing the following:
```
sudo chmod +x TwitchDownloaderCLI
```
4. You can now start using the donwloader, for example:
```
TwitchDownloaderCLI -m VideoDownload --id <vod-id-here> -o out.mp4
```
For Arch Linux, there's an [AUR Package](https://aur.archlinux.org/packages/twitch-downloader-bin/)
