<div align="center">
    <a href="https://github.com/lay295/TwitchDownloader">
        <img src="TwitchDownloaderWPF/Images/Logo.png" alt="Logo" width="80" height="80">
    </a>
    <h3 align="center">Twitch Downloader</h3>
    <div align="center">
        Twitch 點播 / 剪輯 / 聊天下載及聊天渲染
        <br />
        <br />
        <a href="https://github.com/lay295/TwitchDownloader/issues">
            反饋漏洞
        </a>
    </div>
</div>

## 聊天渲染示例

<https://user-images.githubusercontent.com/1060681/197653099-c3fd12c2-f03a-4580-84e4-63ce3f36be8d.mp4>

## 它能做什麼？

- 下載 Twitch 點播
- 下載 Twitch 剪輯
- 以 [包含所有原始資訊的 JSON](https://github.com/lay295/TwitchDownloader/files/13495494/ExampleMoonMoonJsonFile.json)、瀏覽器 HTML 檔案或 [純文字檔案](https://github.com/lay295/TwitchDownloader/files/13495523/ExampleMoonMoonTextFile.txt) 的形式下載點播和剪輯的聊天內容
- 更新之前生成的 JSON 聊天檔案的內容，並提供另一種格式的儲存選項
- 使用之前生成的 JSON 聊天檔案，用 Twitter Twemoji 或 Google Noto Color 表情符號以及 BTTV、FFZ、7TV 靜態和動畫表情來呈現聊天內容

# GUI

## Windows 呈現基礎 (WPF)

![WindowsWPF](https://i.imgur.com/bLegxGX.gif)

### [點選此處檢視完整的 WPF 文件（英語）](TwitchDownloaderWPF/README.md)

### 功能介紹

Windows WPF GUI 實現了程式的所有主要功能以及一些額外的提升效率的功能：

- 排隊同時執行多個下載 / 渲染任務
- 從點播 / 剪輯連結列表中建立下載任務列表
- 搜尋並下載來自任何流媒體的多個點播 / 剪輯，無需離開應用程式

### 多語言支援

透過社群翻譯，Windows WPF GUI 有多種語言版本。請參閱 [WPF 自述檔案（英語）](TwitchDownloaderWPF/README.md)的 [本地化部分](TwitchDownloaderWPF/README.md#localization)。

### 主題

Windows WPF GUI 內建了淺色和深色主題，以及根據當前 Windows 主題進行實時更新的選項。它還支援使用者建立主題！更多詳情，請參閱 [WPF 自述檔案（英語）](TwitchDownloaderWPF/README.md)的 [主題部分](TwitchDownloaderWPF/README.md#theming)。

### 影片演示

<https://www.youtube.com/watch?v=0W3MhfhnYjk>
（舊版，概念相同）

## Linux?

請檢視 [Github](https://github.com/mohad12211/twitch-downloader-gui) 上的 twitch-downloader-gui 或 [AUR](https://aur.archlinux.org/packages/twitch-downloader-gui) 上的 CLI 的 Linux GUI 封裝程式。

## MacOS?

目前還沒有適用於 MacOS 的 GUI 版本 :(

# CLI

### [點選此處檢視完整的 CLI 文件](TwitchDownloaderCLI/README.md)

CLI 是跨平臺的，可以實現程式的主要功能。它可在 Windows、Linux 和 MacOS<sup>*</sup> 上執行。

<sup>*僅對 Intel Mac 進行了測試</sup>

透過 CLI，可以使用外部指令碼自動處理影片。例如，你可以在 Windows 上將以下程式碼複製貼上到 `.bat` 檔案中，下載點播及其聊天內容，然後渲染聊天內容，所有這些都只需一次輸入。

```bat
@echo off
set /p vodid="輸入點播 ID："
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json -E
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```

## Windows - 開始

1. 前往 [Release 頁面](https://github.com/lay295/TwitchDownloader/releases/) 並下載最新的 Windows 版本或 [從原始碼構建](#building-from-source)。
2. 提取 `TwitchDownloaderCLI.exe`。
3. 開啟可執行檔案放置的位置：

```命令
cd C:\folder\containing\TwitchDownloaderCLI
```

4. 如果沒有 FFmpeg，可以透過 [Chocolatey 程式包管理器](https://community.chocolatey.org/) 安裝，也可以從 [ffmpeg.org](https://ffmpeg.org/download.html) 或使用 TwitchDownloaderCLI 以獨立檔案的形式獲取：

```命令
TwitchDownloaderCLI.exe ffmpeg --download
```

5. 你現在可以開始使用 TwitchDownloaderCLI 了，例如：

```命令
TwitchDownloaderCLI.exe videodownload --id <點播 ID> -o out.mp4
```

更多命令示例請參閱 [CLI 自述檔案](TwitchDownloaderCLI/README.md#example-commands)。

## Linux – 開始

1. 有些發行版，如 Linux Alpine，缺少某些語言（阿拉伯語、波斯語、泰語等）的字型。如果是這種情況，請安裝額外的字體系列，如 [Noto Sans](https://fonts.google.com/noto/specimen/Noto+Sans)，或者檢視發行版的字型 Wiki 頁面，因為它可能有針對這種特定情況的安裝命令，如 [Linux Alpine](https://wiki.alpinelinux.org/wiki/Fonts) 字型頁面。
2. 確保 `fontconfig` 和 `libfontconfig1` 都已安裝。在 Ubuntu 上執行 `apt-get install fontconfig libfontconfig1`。
3. 前往 [Release 頁面](https://github.com/lay295/TwitchDownloader/releases/) 並下載最新的 Linux 版本抓取適用於 Arch Linux 的 [AUR 軟體包](https://aur.archlinux.org/packages/twitch-downloader-bin/)，或 [從原始碼構建](#building-from-source)。
4. 提取 `TwitchDownloaderCLI`。
5. 開啟二進位制可執行檔案放置的目錄：

```命令
cd directory/containing/TwitchDownloaderCLI
```

6. 授予二進位制可執行檔案權限：

```命令
sudo chmod +x TwitchDownloaderCLI
```

7. a) 如果沒有 FFmpeg，可以透過 [Chocolatey 程式包管理器](https://community.chocolatey.org/) 安裝，也可以從 [ffmpeg.org](https://ffmpeg.org/download.html) 或使用 TwitchDownloaderCLI 以獨立檔案的形式獲取：

```命令
./TwitchDownloaderCLI ffmpeg --download
```

7. b) 如果下載的是獨立檔案，還必須授予其可執行權限：

```命令
sudo chmod +x ffmpeg
```

8. 你現在可以開始使用 TwitchDownloaderCLI 了，例如：

```命令
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

更多命令示例請參閱 [CLI 自述檔案](TwitchDownloaderCLI/README.md#example-commands)。

## MacOS – 開始

1. 如果你的裝置使用的是 Apple Silicon M 系列處理器，請確保下載了 arm64 二進位制檔案，但如果你想在 Apple Silicon 上使用 x64 二進位制檔案，則必須在 Rosetta 2 下透過終端會話執行：

```命令
arch -x86_64 zsh
```

2. 前往 [Release 頁面](https://github.com/lay295/TwitchDownloader/releases/) 並下載最新的 MacOS 版本或 [從原始碼構建](#building-from-source)。
3. 提取 `TwitchDownloaderCLI`。
4. 開啟二進位制可執行檔案放置的目錄：

```命令
cd directory/containing/TwitchDownloaderCLI
```

5. 授予二進位制可執行檔案在終端中的權限：

```命令
chmod +x TwitchDownloaderCLI
```

6. a) 如果沒有 FFmpeg，可以透過 [Homebrew 程式包管理器](https://brew.sh/) 在整個系統中安裝，也可以從 [ffmpeg.org](https://ffmpeg.org/download.html) 或使用 TwitchDownloaderCLI 以獨立檔案的形式獲取：

```命令
./TwitchDownloaderCLI ffmpeg --download
```

6. b) 如果下載的是獨立檔案，還必須授予其可執行權限：

```命令
chmod +x ffmpeg
```

7. 你現在可以開始使用 TwitchDownloaderCLI 了，例如：

```命令
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

更多命令示例請參閱 [CLI 自述檔案](TwitchDownloaderCLI/README.md#example-commands)。

# 從原始碼構建

## 要求

- [.NET 6.0.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- 約 1GB 磁碟空間

## 構建說明

1. 克隆此倉庫：

```命令
git clone https://github.com/lay295/TwitchDownloader.git
```

2. 定位至 solution 資料夾：

```命令
cd TwitchDownloader
```

3. 還原 solution：

```命令
dotnet restore
```

- 非 Windows 裝置可能需要明確指定要還原的專案，即 `dotnet restore TwitchDownloaderCLI`

4. a) 構建 GUI：

```命令
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows
```

4. b) 構建 CLI：

```命令
dotnet publish TwitchDownloaderCLI -p:PublishProfile=<Profile>
```

- 可接受的配置檔案：`Windows`、`Linux`、`LinuxAlpine`、`LinuxArm`、`LinuxArm64`、`MacOS`、`MacOSArm64`

5. a) 定位至 GUI 構建資料夾：

```命令
cd TwitchDownloaderWPF/bin/Release/net6.0-windows/publish/win-x64
```

5. b) 定位至 CLI 構建資料夾：

```命令
cd TwitchDownloaderCLI/bin/Release/net6.0/publish
```

# 第三方貢獻

聊天渲染使用 [SkiaSharp](https://github.com/mono/SkiaSharp) 和 [HarfBuzzSharp](https://github.com/mono/SkiaSharp) © Microsoft 公司。

對聊天渲染進行編碼，並最終完成影片下載使用 [FFmpeg](https://ffmpeg.org/) © FFmpeg 開發者。

聊天渲染可能會使用 [Noto Color 表情符號](https://github.com/googlefonts/noto-emoji) © Google 及其貢獻者。

聊天渲染可能會使用 [Twemoji](https://github.com/twitter/twemoji) © Twitter 及其貢獻者。

內建的 FFmpeg 二進位制檔案取自 [gyan.dev](https://www.gyan.dev/ffmpeg/) © Gyan Doshi。

FFmpeg 二進位制檔案和執行時的下載使用 [Xabe.FFmpeg.Downloader](https://github.com/tomaszzmuda/Xabe.FFmpeg) © Xabe。

聊天 Html 匯出使用的 _Inter_ 字型由 [Google Fonts API](https://fonts.google.com/) 託管 © Google。

有關使用的外部庫的完整列表，請參閱 [THIRD-PARTY-LICENSES.txt（英文）](./TwitchDownloaderCore/Resources/THIRD-PARTY-LICENSES.txt)。

# 開源許可協議

[MIT](./LICENSE.txt)

TwitchDownloader 與 Twitch Interactive, Inc. 及其附屬公司沒有任何關聯。
