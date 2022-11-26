<p align="center">
  <a href="https://github.com/lay295/TwitchDownloader">
    <img src="TwitchDownloaderWPF/Images/Logo.png" alt="Logo" width="80" height="80">
    
  </a>

  <h3 align="center">Twitch Downloader</h3>

  <p align="center">
    Twitch VOD/Clip/Chat Downloader and Chat Renderer
    <br />
    <br />
    <a href="https://github.com/lay295/TwitchDownloader/issues">Report Bug</a>
  </p>
</p>



## Chat Render Example
https://user-images.githubusercontent.com/1060681/197653099-c3fd12c2-f03a-4580-84e4-63ce3f36be8d.mp4


## What can it do?
- Download Twitch VODs
- Download Twitch Clips
- Download chat for VODS and Clips, in either a [JSON with all the original information](https://pastebin.com/raw/YDgRe6X4), a browser HTML file, or a [plain text file](https://pastebin.com/raw/016azeQX)
- Update the contents of a previously generated JSON chat file with an option to save as another format
- Use a previously generated JSON chat file to render the chat with FFZ, BTTV and 7TV support (including GIFS)

# GUI

![](https://i.imgur.com/bLegxGX.gif)

## Video Demonstration
https://www.youtube.com/watch?v=0W3MhfhnYjk
(older version, same concept)

## Linux?

Check twitch-downloader-gui on [github](https://github.com/mohad12211/twitch-downloader-gui) or on the [AUR](https://aur.archlinux.org/packages/twitch-downloader-gui) for a Linux GUI wrapper for the CLI.

## MacOS?

No GUI is avaiable for MacOS yet :(

# CLI

The CLI is cross-platform and implements the main functions of the program. It works on Windows, Linux, and MacOS*.

<sup>*Only Intel Macs have been tested</sup>

### [CLI Documentation here](TwitchDownloaderCLI/README.md). 

If you're on Linux, ensure both `fontconfig` and `libfontconfig1` are installed. `apt-get install fontconfig libfontconfig1` on Ubuntu.

For example, you could copy-paste the following code into a `.bat` file on Windows to download a VOD and chat, then render the chat in a single action.
```
@echo off
set /p vodid="Enter VOD ID: "
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```
---
### Linux – Getting started

1. Go to [Releases](https://github.com/lay295/TwitchDownloader/releases/) and download the latest version for Linux.
2. Extract `TwitchDownloaderCLI`
3. Browse to where you extracted the file and give it executable rights in Terminal:
```
sudo chmod +x TwitchDownloaderCLI
```
4. If you do not have ffmpeg, you should install it via your distro package manager, however you can also get it as a standalone file from [ffmpeg.org](https://ffmpeg.org/download.html) or by using TwitchDownloaderCLI:
```
./TwitchDownloaderCLI ffmpeg --download
```
If downloaded as a standalone file, you must also give it executable rights with:
```
sudo chmod +x ffmpeg
```
5. You can now start using the downloader, for example:
```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```
For Arch Linux, there's an [AUR Package](https://aur.archlinux.org/packages/twitch-downloader-bin/)

### MacOS – Getting started
1. Go to [Releases](https://github.com/lay295/TwitchDownloader/releases/) and download the latest version for MacOS.
2. Extract `TwitchDownloaderCLI`
3. Browse to where you extracted the file and give it executable rights in Terminal:
```
chmod +x TwitchDownloaderCLI
```
4. If you do not have ffmpeg, you can install it via [Homebrew package manager](https://brew.sh/), or you can get it as a standalone file from [ffmpeg.org](https://ffmpeg.org/download.html) or by using TwitchDownloaderCLI:
```
./TwitchDownloaderCLI ffmpeg --download
```
If downloaded as a standalone file, you must also give it executable rights with:
```
chmod +x ffmpeg
```
5. You can now start using the downloader, for example:
```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

# License

[MIT](./LICENSE.txt)