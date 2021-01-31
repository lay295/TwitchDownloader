
# TwitchDownloader
Twitch VOD/Clip/Chat downloader and chat renderer I wrote.

**THIS USES UNDOCUMENTED API ENDPOINTS, MAY BREAK EASILY. I'LL TRY AND UPDATE WHEN IT DOES.**

## Chat Render Example
![](https://i.imgur.com/I4Z2bWo.gif)

## What can it do?
- Download Twitch VODs
- Download Twitch Clips
- Download chat for VODS and Clips, in either a [JSON with all the information](https://pastebin.com/raw/YDgRe6X4) or a [simple text file](https://pastebin.com/raw/016azeQX)
- Use a previously generated JSON chat file to render the chat with FFZ and BTTV support (including GIFS)

## Things still to be done
- Fix bugs that slipped by
- More options for chat rendering

# GUI

![](https://i.imgur.com/BmGqYbm.gif)

## Video Demonstration
https://www.youtube.com/watch?v=0W3MhfhnYjk
(older version, same concept)

## Linux? MacOS?
Sorry, the GUI version is only avaliable for Windows :(  

# CLI

The CLI is cross platform and performs the main functions of the program. It works on Windows and Linux, but has not been tested on MacOS. 

[Documentation here](https://github.com/lay295/TwitchDownloader/blob/master/TwitchDownloaderCLI/README.md). 

I've never really made a command line utility before so things may change in the future. If you're on Linux, make sure `fontconfig` and `libfontconfig1` are installed `(apt-get install fontconfig libfontconfig1)`.

For example, you could copy/paste this into a `.bat` file on Windows, to download a VOD, chat, and then render in a single go.  
```
@echo off
set /p vodid="Enter VOD ID: "
TwitchDownloaderCLI -m VideoDownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI -m ChatDownload --id %vodid% -o %vodid%_chat.json
TwitchDownloaderCLI -m ChatRender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```
---
### Linux – Getting started

1. Go to [Releases](https://github.com/lay295/TwitchDownloader/releases/) and download the latest version for Linux.
2. Extract `TwitchDownloaderCLI`
3. Browse to where you extracted the file and give it executable rights in Terminal:
```
sudo chmod +x TwitchDownloaderCLI
```
4. You can now start using the donwloader, for example:
```
TwitchDownloaderCLI -m VideoDownload --id <vod-id-here> -o out.mp4
```
For Arch Linux, there's an [AUR Package](https://aur.archlinux.org/packages/twitch-downloader-bin/)
