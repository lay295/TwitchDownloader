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

## Windows

![](https://i.imgur.com/bLegxGX.gif)

### Functionality

The Windows GUI implements all of the main functions of the program along with some extra quality of life functions:
- Queue up multiple download/render jobs to run simultaneously
- Create a list of download jobs from a list of vod/clip links
- Search for and download multiple vods/clips from any streamer without leaving the app

### Theming
The Windows GUI comes with a light and dark theme and will update according the current Windows theme by default. The GUI also supports user created themes! See `Themes/README.txt` (generated on every run) for more details.

### Video Demonstration
https://www.youtube.com/watch?v=0W3MhfhnYjk
(older version, same concept)

## Linux?

Check twitch-downloader-gui on [github](https://github.com/mohad12211/twitch-downloader-gui) or on the [AUR](https://aur.archlinux.org/packages/twitch-downloader-gui) for a Linux GUI wrapper for the CLI.

## MacOS?

No GUI is available for MacOS yet :(

# CLI

### [See the full CLI documentation here](TwitchDownloaderCLI/README.md).

The CLI is cross-platform and implements the main functions of the program. It works on Windows, Linux, and MacOS<sup>*</sup>.

<sup>*Only Intel Macs have been tested</sup>

With the CLI, it is possible to automate video processing using external scripts. For example, you could copy-paste the following code into a `.bat` file on Windows to download a VOD and its chat, and then render the chat, all from a single input.
```bat
@echo off
set /p vodid="Enter VOD ID: "
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json -E
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```

## Windows - Getting started

1. Go to [Releases](https://github.com/lay295/TwitchDownloader/releases/) and download the latest version for Windows or [build from source](#building-from-source).
2. Extract `TwitchDownloaderCLI.exe`.
3. Browse to where you extracted the file in the terminal.
4. If you do not have ffmpeg, you can install it via [Chocolatey package manager](https://community.chocolatey.org/), or you can get it as a standalone file from [ffmpeg.org](https://ffmpeg.org/download.html) or by using TwitchDownloaderCLI:
```
TwitchDownloaderCLI.exe ffmpeg --download
```
5. You can now start using the downloader, for example:
```
TwitchDownloaderCLI.exe videodownload --id <vod-id-here> -o out.mp4
```

## Linux – Getting started

1. Some distros, like Linux Alpine, lack fonts for some languages (Arabic, Persian, Thai, etc.) If this is the case for you, install additional fonts families such as [Noto](https://fonts.google.com/noto/specimen/Noto+Sans) or check your distro's wiki page on fonts as it may have an install command for this specific scenario, such as the [Linux Alpine](https://wiki.alpinelinux.org/wiki/Fonts) font page.
2. Ensure both `fontconfig` and `libfontconfig1` are installed. `apt-get install fontconfig libfontconfig1` on Ubuntu.
3. Go to [Releases](https://github.com/lay295/TwitchDownloader/releases/) and download the latest binary for Linux, grab the [AUR Package](https://aur.archlinux.org/packages/twitch-downloader-bin/) for Arch Linux, or [build from source](#building-from-source).
4. Extract `TwitchDownloaderCLI`.
5. Browse to where you extracted the file and give it executable rights in the terminal:
```
sudo chmod +x TwitchDownloaderCLI
```
6. a) If you do not have ffmpeg, you should install it via your distro package manager, however you can also get it as a standalone file from [ffmpeg.org](https://ffmpeg.org/download.html) or by using TwitchDownloaderCLI:
```
./TwitchDownloaderCLI ffmpeg --download
```
6. b) If downloaded as a standalone file, you must also give it executable rights with:
```
sudo chmod +x ffmpeg
```
7. You can now start using the downloader, for example:
```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

## MacOS – Getting started
1. Go to [Releases](https://github.com/lay295/TwitchDownloader/releases/) and download the latest binary for MacOS or [build from source](#building-from-source).
2. Extract `TwitchDownloaderCLI`.
3. Browse to where you extracted the file and give it executable rights in the terminal:
```
chmod +x TwitchDownloaderCLI
```
4. a) If you do not have ffmpeg, you can install it via [Homebrew package manager](https://brew.sh/), or you can get it as a standalone file from [ffmpeg.org](https://ffmpeg.org/download.html) or by using TwitchDownloaderCLI:
```
./TwitchDownloaderCLI ffmpeg --download
```
4. b) If downloaded as a standalone file, you must also give it executable rights with:
```
chmod +x ffmpeg
```
5. You can now start using the downloader, for example:
```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

# Building from source

## Requirements

- [.NET 6.0.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

## Build Instructions

1. Clone the repository:
```
git clone https://github.com/lay295/TwitchDownloader.git
```
2. Navigate to the solution folder:
```
cd TwitchDownloader
```
3. Restore the solution:
```
dotnet restore
```
4. a) Build the GUI:
```
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows -p:DebugType=None -p:DebugSymbols=false
```
4. b) Build the CLI:
```
dotnet publish TwitchDownloaderCLI -p:PublishProfile=<Profile> -p:DebugType=None -p:DebugSymbols=false
```
- Applicable Profiles: `Windows`, `Linux`, `LinuxAlpine`, `LinuxArm`, `MacOS`
5. a) Navigate to the GUI build folder:
```
cd TwitchDownloaderWPF/bin/Release/net6.0-windows/publish/win-x64
```
5. b) Navigate to the CLI build folder:
```
cd TwitchDownloaderCLI/bin/Release/net6.0/publish
```

# License

[MIT](./LICENSE.txt)