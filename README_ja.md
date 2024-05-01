<div align="center">
  <a href="https://github.com/lay295/TwitchDownloader">
    <img src="TwitchDownloaderWPF/Images/Logo.png" alt="Logo" width="80" height="80">
  </a>
  <h3 align="center">Twitch Downloader</h3>
  <div align="center">
    Twitch VOD/Clip/Chat Downloader and Chat Renderer
    <br />
    <br />
    <a href="https://github.com/lay295/TwitchDownloader/issues">バグを報告する</a>
  </div>
</div>

## チャットレンダリングの例

<https://user-images.githubusercontent.com/1060681/197653099-c3fd12c2-f03a-4580-84e4-63ce3f36be8d.mp4>

## なにができる？

- TwitchのVODsをダウンロードする
- Twitchのクリップをダウンロードする
- VODs、クリップのチャットを[全ての情報を含んだJSON](https://github.com/lay295/TwitchDownloader/files/13495494/ExampleMoonMoonJsonFile.json)、ブラウザ用のHTMLファイル、[プレーンテキスト](https://github.com/lay295/TwitchDownloader/files/13495523/ExampleMoonMoonTextFile.txt)でダウンロードする
- 以前に生成されたJSOn形式のチャットファイルを別の形式で保存し、内容を変更する
- 生成されたJSON形式のチャットファイルから、Twitter Twemoji・Google Noto Color emoji・BTTV・FFZ・7TV・スタンプ・GIFスタンプと一緒にチャットをレンダリングします

# GUI

## Windows WPF

![](https://i.imgur.com/bLegxGX.gif)

### [完全なWPFのドキュメントを見る](TwitchDownloaderWPF/README.md)

### 機能性

Windows WPF GUIは、プログラムのすべての主要機能といくつかのQOL機能を実装しています:

- 複数のダウンロード・レンダリングジョブを同時にキューに追加する
- VODs・クリップのリンクからダウンロードジョブのリストを作成する
- ストリーマーから複数のVODs・クリップを検索してダウンロードする

### 複数言語サポート

Windows WPF GUIは、コミュニティの翻訳により複数の言語で利用可能です。[WPF README](TwitchDownloaderWPF/README.md) の [Localization section](TwitchDownloaderWPF/README.md#localization) で詳細を確認できます。

### テーマ

Windows WPF GUI では、ライトテーマとダークテーマの両方が実装されており、現在のWindowsテーマに同期する設定もあります。また、ユーザーが作成したテーマもサポートされています！[WPF README](TwitchDownloaderWPF/README.md) の [Theming section](TwitchDownloaderWPF/README.md#theming) で詳細を確認できます。

### ビデオデモンストレーション

<https://www.youtube.com/watch?v=0W3MhfhnYjk>
(古いバージョンでも同様です)

## Linux?

twitch-downloader-gui を [GitHub](https://github.com/mohad12211/twitch-downloader-gui) や [AUR](https://aur.archlinux.org/packages/twitch-downloader-gui) でチェックしてください

## MacOS?

MacOS用のGUIはまだありません。:(

# CLI

### [See the full CLI documentation here](TwitchDownloaderCLI/README.md)

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
3. Browse to where you extracted the executable:

```
cd C:\folder\containing\TwitchDownloaderCLI
```

4. If you do not have FFmpeg, you can install it via [Chocolatey package manager](https://community.chocolatey.org/), or you can get it as a standalone file from [ffmpeg.org](https://ffmpeg.org/download.html) or by using TwitchDownloaderCLI:

```
TwitchDownloaderCLI.exe ffmpeg --download
```

5. You can now start using TwitchDownloaderCLI, for example:

```
TwitchDownloaderCLI.exe videodownload --id <vod-id-here> -o out.mp4
```

You can find more example commands in the [CLI README](TwitchDownloaderCLI/README.md#example-commands).

## Linux – Getting started

1. Some distros, like Linux Alpine, lack fonts for some languages (Arabic, Persian, Thai, etc.) If this is the case for you, install additional fonts families such as [Noto](https://fonts.google.com/noto/specimen/Noto+Sans) or check your distro's wiki page on fonts as it may have an install command for this specific scenario, such as the [Linux Alpine](https://wiki.alpinelinux.org/wiki/Fonts) font page.
2. Ensure both `fontconfig` and `libfontconfig1` are installed. `apt-get install fontconfig libfontconfig1` on Ubuntu.
3. Go to [Releases](https://github.com/lay295/TwitchDownloader/releases/) and download the latest binary for Linux, grab the [AUR Package](https://aur.archlinux.org/packages/twitch-downloader-bin/) for Arch Linux, or [build from source](#building-from-source).
4. Extract `TwitchDownloaderCLI`.
5. Browse to where you extracted the binary:

```
cd directory/containing/TwitchDownloaderCLI
```

6. Give the binary executable rights:

```
sudo chmod +x TwitchDownloaderCLI
```

7. a) If you do not have FFmpeg, you should install it system-wide via your distro package manager, however you can also get it as a standalone file from [ffmpeg.org](https://ffmpeg.org/download.html) or by using TwitchDownloaderCLI:

```
./TwitchDownloaderCLI ffmpeg --download
```

7. b) If downloaded as a standalone file, you must also give it executable rights with:

```
sudo chmod +x ffmpeg
```

8. You can now start using TwitchDownloaderCLI, for example:

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

You can find more example commands in the [CLI README](TwitchDownloaderCLI/README.md#example-commands).

## MacOS – Getting started

1. If your device has an Apple Silicon M-series processor, ensure that you download the arm64 binary, however if you would like to use the x64 binary on Apple Silicon it must be run via a terminal session running under Rosetta 2:

```
arch -x86_64 zsh
```

2. Go to [Releases](https://github.com/lay295/TwitchDownloader/releases/) and download the latest binary for MacOS or [build from source](#building-from-source).
3. Extract `TwitchDownloaderCLI`.
4. Browse to where you extracted the binary:

```
cd directory/containing/TwitchDownloaderCLI
```

5. Give the binary executable rights in the terminal:

```
chmod +x TwitchDownloaderCLI
```

6. a) If you do not have FFmpeg, you can install it system-wide via the [Homebrew package manager](https://brew.sh/), or you can get it as a standalone file from [ffmpeg.org](https://ffmpeg.org/download.html) or by using TwitchDownloaderCLI:

```
./TwitchDownloaderCLI ffmpeg --download
```

6. b) If downloaded as a standalone file, you must also give it executable rights with:

```
chmod +x ffmpeg
```

7. You can now start using TwitchDownloaderCLI, for example:

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

You can find more example commands in the [CLI README](TwitchDownloaderCLI/README.md#example-commands).

# Building from source

## Requirements

- [.NET 6.0.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- About 1GB of disk space

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

- Non-Windows devices may need to explicitly specify a project to restore, i.e. `dotnet restore TwitchDownloaderCLI`

4. a) Build the GUI:

```
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows
```

4. b) Build the CLI:

```
dotnet publish TwitchDownloaderCLI -p:PublishProfile=<Profile>
```

- Applicable Profiles: `Windows`, `Linux`, `LinuxAlpine`, `LinuxArm`, `LinuxArm64`, `MacOS`, `MacOSArm64`

5. a) Navigate to the GUI build folder:

```
cd TwitchDownloaderWPF/bin/Release/net6.0-windows/publish/win-x64
```

5. b) Navigate to the CLI build folder:

```
cd TwitchDownloaderCLI/bin/Release/net6.0/publish
```

# Third Party Credits

Chat Renders are rendered with [SkiaSharp](https://github.com/mono/SkiaSharp) and [HarfBuzzSharp](https://github.com/mono/SkiaSharp) © Microsoft Corporation.

Chat Renders are encoded and Video Downloads are finalized with [FFmpeg](https://ffmpeg.org/) © The FFmpeg developers.

Chat Renders may use [Noto Color Emoji](https://github.com/googlefonts/noto-emoji) © Google and contributors.

Chat Renders may use [Twemoji](https://github.com/twitter/twemoji) © Twitter and contributors.

Bundled FFmpeg binaries are fetched from [gyan.dev](https://www.gyan.dev/ffmpeg/) © Gyan Doshi.

FFmpeg binaries fetched are runtime are downloaded using [Xabe.FFmpeg.Downloader](https://github.com/tomaszzmuda/Xabe.FFmpeg) © Xabe.

Chat Html exports utilize the _Inter_ typeface hosted by the [Google Fonts API](https://fonts.google.com/) © Google.

For a full list of utilized external libraries, see [THIRD-PARTY-LICENSES.txt](./TwitchDownloaderCore/Resources/THIRD-PARTY-LICENSES.txt).

# License

[MIT](./LICENSE.txt)

TwitchDownloader is in no way associated with Twitch Interactive, Inc. or its affiliates.
