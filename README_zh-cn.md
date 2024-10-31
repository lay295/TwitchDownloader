<div align="center">
    <a href="https://github.com/lay295/TwitchDownloader">
        <img src="TwitchDownloaderWPF/Images/Logo.png" alt="Logo" width="80" height="80">
    </a>
    <h3 align="center">Twitch Downloader</h3>
    <div align="center">
        Twitch 点播 / 剪辑 / 聊天下载及聊天渲染
        <br />
        <br />
        <a href="https://github.com/lay295/TwitchDownloader/issues">
            反馈漏洞
        </a>
    </div>
</div>

## 聊天渲染示例

<https://user-images.githubusercontent.com/1060681/197653099-c3fd12c2-f03a-4580-84e4-63ce3f36be8d.mp4>

## 它能做什么？

- 下载 Twitch 点播
- 下载 Twitch 剪辑
- 以 [包含所有原始信息的 JSON](https://github.com/lay295/TwitchDownloader/files/13495494/ExampleMoonMoonJsonFile.json)、浏览器 HTML 文件或 [纯文本文件](https://github.com/lay295/TwitchDownloader/files/13495523/ExampleMoonMoonTextFile.txt) 的形式下载点播和剪辑的聊天内容
- 更新之前生成的 JSON 聊天文件的内容，并提供另一种格式的保存选项
- 使用之前生成的 JSON 聊天文件，用 Twitter Twemoji 或 Google Noto Color 表情符号以及 BTTV、FFZ、7TV 静态和动画表情来呈现聊天内容

# GUI

## Windows 呈现基础 (WPF)

![WindowsWPF](https://i.imgur.com/bLegxGX.gif)

### [点击此处查看完整的 WPF 文档（英语）](TwitchDownloaderWPF/README.md)

### 功能介绍

Windows WPF GUI 实现了程序的所有主要功能以及一些额外的提升效率的功能：

- 排队同时运行多个下载 / 渲染任务
- 从点播 / 剪辑链接列表中创建下载任务列表
- 搜索并下载来自任何流媒体的多个点播 / 剪辑，无需离开应用程序

### 多语言支持

通过社区翻译，Windows WPF GUI 有多种语言版本。请参阅 [WPF 自述文件（英语）](TwitchDownloaderWPF/README.md)的 [本地化部分](TwitchDownloaderWPF/README.md#localization)。

### 主题

Windows WPF GUI 内置了浅色和深色主题，以及根据当前 Windows 主题进行实时更新的选项。它还支持用户创建主题！更多详情，请参阅 [WPF 自述文件（英语）](TwitchDownloaderWPF/README.md)的 [主题部分](TwitchDownloaderWPF/README.md#theming)。

### 视频演示

<https://www.youtube.com/watch?v=0W3MhfhnYjk>
（旧版，概念相同）

## Linux?

请查看 [Github](https://github.com/mohad12211/twitch-downloader-gui) 上的 twitch-downloader-gui 或 [AUR](https://aur.archlinux.org/packages/twitch-downloader-gui) 上的 CLI 的 Linux GUI 封装程序。

## MacOS?

目前还没有适用于 MacOS 的 GUI 版本 :(

# CLI

### [点击此处查看完整的 CLI 文档](TwitchDownloaderCLI/README.md)

CLI 是跨平台的，可以实现程序的主要功能。它可在 Windows、Linux 和 MacOS<sup>*</sup> 上运行。

<sup>*仅对 Intel Mac 进行了测试</sup>

通过 CLI，可以使用外部脚本自动处理视频。例如，你可以在 Windows 上将以下代码复制粘贴到 `.bat` 文件中，下载点播及其聊天内容，然后渲染聊天内容，所有这些都只需一次输入。

```bat
@echo off
set /p vodid="输入点播 ID："
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json -E
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```

## Windows - 开始

1. 前往 [发行版页面](https://github.com/lay295/TwitchDownloader/releases/) 并下载最新的 Windows 版本或 [从源代码构建](#building-from-source)。
2. 提取 `TwitchDownloaderCLI.exe`。
3. 打开可执行文件放置的位置：

```命令
cd C:\folder\containing\TwitchDownloaderCLI
```

4. 如果没有 FFmpeg，可以通过 [Chocolatey 程序包管理器](https://community.chocolatey.org/) 安装，也可以从 [ffmpeg.org](https://ffmpeg.org/download.html) 或使用 TwitchDownloaderCLI 以独立文件的形式获取：

```命令
TwitchDownloaderCLI.exe ffmpeg --download
```

5. 你现在可以开始使用 TwitchDownloaderCLI 了，例如：

```命令
TwitchDownloaderCLI.exe videodownload --id <点播 ID> -o out.mp4
```

更多命令示例请参阅 [CLI 自述文件](TwitchDownloaderCLI/README.md#example-commands)。

## Linux – 开始

1. 有些发行版，如 Linux Alpine，缺少某些语言（阿拉伯语、波斯语、泰语等）的字体。如果是这种情况，请安装额外的字体系列，如 [Noto Sans](https://fonts.google.com/noto/specimen/Noto+Sans)，或者查看发行版的字体 Wiki 页面，因为它可能有针对这种特定情况的安装命令，如 [Linux Alpine](https://wiki.alpinelinux.org/wiki/Fonts) 字体页面。
2. 确保 `fontconfig` 和 `libfontconfig1` 都已安装。在 Ubuntu 上运行 `apt-get install fontconfig libfontconfig1`。
3. 前往 [发行版页面](https://github.com/lay295/TwitchDownloader/releases/) 并下载最新的 Linux 版本抓取适用于 Arch Linux 的 [AUR 软件包](https://aur.archlinux.org/packages/twitch-downloader-bin/)，或 [从源代码构建](#building-from-source)。
4. 提取 `TwitchDownloaderCLI`。
5. 打开二进制可执行文件放置的目录：

```命令
cd directory/containing/TwitchDownloaderCLI
```

6. 授予二进制可执行文件权限：

```命令
sudo chmod +x TwitchDownloaderCLI
```

7. a) 如果没有 FFmpeg，可以通过 [Chocolatey 程序包管理器](https://community.chocolatey.org/) 安装，也可以从 [ffmpeg.org](https://ffmpeg.org/download.html) 或使用 TwitchDownloaderCLI 以独立文件的形式获取：

```命令
./TwitchDownloaderCLI ffmpeg --download
```

7. b) 如果下载的是独立文件，还必须授予其可执行权限：

```命令
sudo chmod +x ffmpeg
```

8. 你现在可以开始使用 TwitchDownloaderCLI 了，例如：

```命令
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

更多命令示例请参阅 [CLI 自述文件](TwitchDownloaderCLI/README.md#example-commands)。

## MacOS – 开始

1. 如果你的设备使用的是 Apple Silicon M 系列处理器，请确保下载了 arm64 二进制文件，但如果你想在 Apple Silicon 上使用 x64 二进制文件，则必须在 Rosetta 2 下通过终端会话运行：

```命令
arch -x86_64 zsh
```

2. 前往 [发行版页面](https://github.com/lay295/TwitchDownloader/releases/) 并下载最新的 MacOS 版本或 [从源代码构建](#building-from-source)。
3. 提取 `TwitchDownloaderCLI`。
4. 打开二进制可执行文件放置的目录：

```命令
cd directory/containing/TwitchDownloaderCLI
```

5. 授予二进制可执行文件在终端中的权限：

```命令
chmod +x TwitchDownloaderCLI
```

6. a) 如果没有 FFmpeg，可以通过 [Homebrew 程序包管理器](https://brew.sh/) 在整个系统中安装，也可以从 [ffmpeg.org](https://ffmpeg.org/download.html) 或使用 TwitchDownloaderCLI 以独立文件的形式获取：

```命令
./TwitchDownloaderCLI ffmpeg --download
```

6. b) 如果下载的是独立文件，还必须授予其可执行权限：

```命令
chmod +x ffmpeg
```

7. 你现在可以开始使用 TwitchDownloaderCLI 了，例如：

```命令
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

更多命令示例请参阅 [CLI 自述文件](TwitchDownloaderCLI/README.md#example-commands)。

# 从源代码构建

## 要求

- [.NET 6.0.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- 约 1GB 磁盘空间

## 构建说明

1. 克隆此仓库：

```命令
git clone https://github.com/lay295/TwitchDownloader.git
```

2. 定位至 solution 文件夹：

```命令
cd TwitchDownloader
```

3. 还原 solution：

```命令
dotnet restore
```

- 非 Windows 设备可能需要明确指定要还原的项目，即 `dotnet restore TwitchDownloaderCLI`

4. a) 构建 GUI：

```命令
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows
```

4. b) 构建 CLI：

```命令
dotnet publish TwitchDownloaderCLI -p:PublishProfile=<Profile>
```

- 可接受的配置文件：`Windows`、`Linux`、`LinuxAlpine`、`LinuxArm`、`LinuxArm64`、`MacOS`、`MacOSArm64`

5. a) 定位至 GUI 构建文件夹：

```命令
cd TwitchDownloaderWPF/bin/Release/net6.0-windows/publish/win-x64
```

5. b) 定位至 CLI 构建文件夹：

```命令
cd TwitchDownloaderCLI/bin/Release/net6.0/publish
```

# 第三方贡献

聊天渲染使用 [SkiaSharp](https://github.com/mono/SkiaSharp) 和 [HarfBuzzSharp](https://github.com/mono/SkiaSharp) © Microsoft 公司。

对聊天渲染进行编码，并最终完成视频下载使用 [FFmpeg](https://ffmpeg.org/) © FFmpeg 开发者。

聊天渲染可能会使用 [Noto Color 表情符号](https://github.com/googlefonts/noto-emoji) © Google 及其贡献者。

聊天渲染可能会使用 [Twemoji](https://github.com/twitter/twemoji) © Twitter 及其贡献者。

内置的 FFmpeg 二进制文件取自 [gyan.dev](https://www.gyan.dev/ffmpeg/) © Gyan Doshi。

FFmpeg 二进制文件和运行时的下载使用 [Xabe.FFmpeg.Downloader](https://github.com/tomaszzmuda/Xabe.FFmpeg) © Xabe。

聊天 Html 导出使用的 _Inter_ 字体由 [Google Fonts API](https://fonts.google.com/) 托管 © Google。

有关使用的外部库的完整列表，请参阅 [THIRD-PARTY-LICENSES.txt（英文）](./TwitchDownloaderCore/Resources/THIRD-PARTY-LICENSES.txt)。

# 开源许可协议

[MIT](./LICENSE.txt)

TwitchDownloader 与 Twitch Interactive, Inc. 及其附属公司没有任何关联。
