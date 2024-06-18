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

- Twitchのアーカイブをダウンロードする
- Twitchのクリップをダウンロードする
- アーカイブ、クリップのチャットを[全ての情報を含んだJSON](https://github.com/lay295/TwitchDownloader/files/13495494/ExampleMoonMoonJsonFile.json)、ブラウザ用のHTMLファイル、[プレーンテキスト](https://github.com/lay295/TwitchDownloader/files/13495523/ExampleMoonMoonTextFile.txt)でダウンロードする
- 以前に生成されたJSOn形式のチャットファイルを別の形式で保存し、内容を変更する
- 生成されたJSON形式のチャットファイルから、Twitter Twemoji・Google Noto Color emoji・BTTV・FFZ・7TV・スタンプ・GIFスタンプと一緒にチャットをレンダリングします

# GUI

## Windows WPF

![](https://i.imgur.com/bLegxGX.gif)

### [完全なWPFのドキュメントを見る](TwitchDownloaderWPF/README.md)

### 機能性

Windows WPF GUIは、プログラムのすべての主要機能といくつかのQOL機能を実装しています:

- 複数のダウンロード・レンダリングジョブを同時にキューに追加する
- アーカイブ・クリップのリンクからダウンロードジョブのリストを作成する
- ストリーマーから複数のアーカイブ・クリップを検索してダウンロードする

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

### [完全なCLIのドキュメントを見る](TwitchDownloaderCLI/README.md)

CLIはクロスプラットフォームであり、プログラムの主要な機能を実装しています。Windows, Linux, MacOSで動作します<sup>*</sup>。

<sup>*Intel Macのみでテストされています。</sup>

CLIを使用すると、外部スクリプトを使用してビデオ処理を自動化することができます。  
例えば、以下のコードをWindowsの`.bat`ファイルにコピーペーストすると、アーカイブとそのチャットをダウンロードし、チャットをレンダリングすることができます。

```bat
@echo off
set /p vodid="Enter VOD ID: "
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json -E
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```

## Windows - はじめに

1. [Releases](https://github.com/lay295/TwitchDownloader/releases/)に行き、Windows用の最新バージョンをダウンロードするか[ソースコードからビルドする](#ソースコードからビルドする)
2. `TwitchDownloaderCLI.exe`を展開する。
3. 実行ファイルを展開した場所に移動する:

```
cd C:\folder\containing\TwitchDownloaderCLI
```

4. FFmpegを持っていない場合は、[Chocolatey package manager](https://community.chocolatey.org/) でインストールするか、[ffmpeg.org](https://ffmpeg.org/download.html) からスタンドアロンファイルとして入手するか、TwitchDownloaderCLIを使用してください:

```
TwitchDownloaderCLI.exe ffmpeg --download
```

1. これで、TwitchDownloaderCLIを使用する準備は整いました。例:

```
TwitchDownloaderCLI.exe videodownload --id <vod-id-here> -o out.mp4
```

その他のコマンド例は [CLI README](TwitchDownloaderCLI/README.md#example-commands) で見つけることができます。

## Linux – はじめに

1. いくつかのディストリビューション（例えば、Alpine Linuxなど）には、一部の言語（アラビア語、ペルシャ語、タイ語など）のフォントが含まれていません。  
   このような場合は、[Noto](https://fonts.google.com/noto/specimen/Noto+Sans) のような追加のフォントファミリーをインストールするか、特定のシナリオに対応したインストールコマンドが記載されているディストリビューションのフォントに関するWiki（例えば、[Linux Alpine](https://wiki.alpinelinux.org/wiki/Fonts) のフォントページ）を確認してください。
2. `fontconfig` と `libfontconfig1` の両方がインストールされていることを確認してください。Ubuntuでは、`apt-get install fontconfig libfontconfig1`
3. [Releases](https://github.com/lay295/TwitchDownloader/releases/) で、Linux用の最新バイナリをダウンロードする。または Arch Linux の場合は [AUR Package](https://aur.archlinux.org/packages/twitch-downloader-bin/) から入手するか、[ソースコードからビルドする](#ソースコードからビルドする)。
4. `TwitchDownloaderCLI`を展開する。
5. バイナリを展開した場所に移動する:

```
cd directory/containing/TwitchDownloaderCLI
```

1. バイナリの実行権限を与える:

```
sudo chmod +x TwitchDownloaderCLI
```

7. a) FFmpegを持っていない場合は、使用しているディストリビューションのパッケージマネージャーからシステム全体にインストールする必要がありますが、[ffmpeg.org](https://ffmpeg.org/download.html) からスタンドアロンファイルとして入手するか、TwitchDownloaderCLIを使用して入手することもできます:

```
./TwitchDownloaderCLI ffmpeg --download
```

1. b) スタンドアロンファイルとしてダウンロードした場合は、実行権限を与える必要があります。:

```
sudo chmod +x ffmpeg
```

9. これで、TwitchDownloaderCLIを使用する準備は整いました。例:

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

その他のコマンド例は [CLI README](TwitchDownloaderCLI/README.md#example-commands) で見つけることができます。

## MacOS – Getting started

1. あなたのデバイスが Apple Silicon M シリーズプロセッサを搭載している場合は、arm64 バイナリをダウンロードしてください。ただし、Apple Silicon で x64 バイナリを使用する場合は、Rosetta 2 で実行されたターミナルセッションから実行する必要があります。:

```
arch -x86_64 zsh
```

1. [Releases](https://github.com/lay295/TwitchDownloader/releases/) で、Mac用の最新バイナリをダウンロードする。または [ソースコードからビルドする](#ソースコードからビルドする)。
2. `TwitchDownloaderCLI`を展開する。
3. バイナリを展開した場所に移動する:

```
cd directory/containing/TwitchDownloaderCLI
```

5. ターミナルでバイナリの実行権限を与える:
```
chmod +x TwitchDownloaderCLI
```

1. a) FFmpegを持っていない場合は [Homebrew package manager](https://brew.sh/) からシステム全体にインストールするか、[ffmpeg.org](https://ffmpeg.org/download.html) からスタンドアロンファイルとして入手するか、TwitchDownloaderCLIを使用してください:

```
./TwitchDownloaderCLI ffmpeg --download
```

6. b) スタンドアロンファイルとしてダウンロードした場合は、実行権限を与える必要があります:

```
chmod +x ffmpeg
```

7. これで、TwitchDownloaderCLIを使用する準備は整いました。例:

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

その他のコマンド例は [CLI README](TwitchDownloaderCLI/README.md#example-commands) で見つけることができます。

# ソースコードからビルドする

## 必要条件

- [.NET 6.0.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- 約1GBのディスク空き容量

## ビルド手順

1. リポジトリをクローン:

```
git clone https://github.com/lay295/TwitchDownloader.git
```

2. ソルーションフォルダに移動する:

```
cd TwitchDownloader
```

3. ソリューションをリストアする:

```
dotnet restore
```

- Windows以外のデバイスでは、リストアするプロジェクトを明示的に指定する必要があります。`dotnet restore TwitchDownloaderCLI`

1. a) GUIをビルドする:

```
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows
```

4. b) CLIをビルドする:

```
dotnet publish TwitchDownloaderCLI -p:PublishProfile=<Profile>
```

- 適用可能なProfile: `Windows`, `Linux`, `LinuxAlpine`, `LinuxArm`, `LinuxArm64`, `MacOS`, `MacOSArm64`

5. a) GUIのビルドフォルダに移動する:

```
cd TwitchDownloaderWPF/bin/Release/net6.0-windows/publish/win-x64
```

1. b) CLIのビルドフォルダに移動する:

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

TwitchDownloaderは、Twitch Interactive, Inc. およびその関連会社とは一切関係ありません。  
(TwitchDownloader is in no way associated with Twitch Interactive, Inc. or its affiliates.)
