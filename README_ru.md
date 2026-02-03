<div align="center">
  <a href="https://github.com/lay295/TwitchDownloader">
    <img src="TwitchDownloaderWPF/Images/Logo.png" alt="Logo" width="80" height="80">
  </a>
  <h3 align="center">Twitch Downloader</h3>
  <div align="center">
    Twitch VOD/Клип/Чат скачиватель и Чат рендер
    <br />
    <br />
    <a href="https://github.com/lay295/TwitchDownloader/issues">Сообщить об ошибке</a>
  </div>
</div>

## Что эта программа может делать?

- Скачивать Twitch VODs (записи)
- Скачивать клипы с Twitch`а
- Скачивать чат для VODs и клипов, в [JSON со всей оригинальной информацией](https://github.com/lay295/TwitchDownloader/files/13495494/ExampleMoonMoonJsonFile.json), a также в браузерный HTML файл, или [обычный txt файл](https://github.com/lay295/TwitchDownloader/files/13495523/ExampleMoonMoonTextFile.txt)
- Обновить содержание сгенерированного JSON чат файла с опцией сохранить в другом формате
- Использовать сгенерированный JSON чат файл для рендера чата с смайликами Twitter Twemoji или Google Noto Color Emojis и BTTV, FFZ, 7TV статичныими и анимированными смайликами

### Пример рендера чата

<https://user-images.githubusercontent.com/1060681/197653099-c3fd12c2-f03a-4580-84e4-63ce3f36be8d.mp4>

# Интерфейс

## Windows WPF

![](https://i.imgur.com/bLegxGX.gif)

### [Полная документация по WPF здесь](TwitchDownloaderWPF/README.md)

### Функциональность

 Windows WPF GUI имеет все основные а также жизненно необходимые функции:

- Поставить в очередь несколько задач на скачивание/рендер для одновременного выполнения
- Создать список задач для скачивания из списка ссылок vod/клипов
- Поиск и загрузка нескольких VODs/клипов любого стримера не выходя из приложения

### Поддержка нескольких языков

Windows WPF GUI доступен в нескольких языках благодаря сообществу переводчиков. Посмотрите [Раздел локализации](TwitchDownloaderWPF/README.md#localization) из [WPF README](TwitchDownloaderWPF/README.md) для получения более подробной информации.

### Темы

Windows WPF GUI поставляется с светлой и тёмной темой вместе с возможностью подстраиванием под системную тему. Программа также поддерживает пользовательские темы! Посмотрите [Раздел тем](TwitchDownloaderWPF/README.md#theming) из [WPF README](TwitchDownloaderWPF/README.md) для получения более подробной информации.

### Видео демонстрация

<https://www.youtube.com/watch?v=0W3MhfhnYjk>
(старая версия, концепт тот же)

## Linux?

Посмотрите twitch-downloader-gui на [Гитхабе](https://github.com/mohad12211/twitch-downloader-gui) или на [AUR](https://aur.archlinux.org/packages/twitch-downloader-gui) для GUI оболочки для CLI под Linux.

## MacOS?

GUI не доступен для MacOS пока что :(

# CLI

### [Посмотрите полную документацию по CLI здесь](TwitchDownloaderCLI/README.md)

CLI кросс-платформенная и имеет основные функции программы. Она работает на Windows, Linux, и MacOs*.

<sup>*Протестированы были только Macs с процессором Intel</sup>

С помощью CLI, возможна автоматизация процесса с помощью внешних скриптов. Для примера ниже предоставлен `.bat` код который может быть открыт на Windows. Этот код скачивает VOD и его чат, затем рендерит его, всё от одной команды.

```bat
@echo off
set /p vodid="Напишите айди VOD: "
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json -E
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```

## Windows - Введение

1. Зайдите в [Releases](https://github.com/lay295/TwitchDownloader/releases/) и скачайте последнюю версию для Windows или [сделайте сборку сами](#делаем-свою-сборку).
2. Достаньте `TwitchDownloaderCLI.exe`.
3. Закиньте его в папку которую вы хотите:

```
cd C:\folder\containing\TwitchDownloaderCLI
```

4. Если у вас нету FFmpeg, вы можете установить его через [Chocolatey package manager](https://community.chocolatey.org/), или вы можете скачать его как standalone файл с [ffmpeg.org](https://ffmpeg.org/download.html) или используя TwitchDownloaderCLI:

```
TwitchDownloaderCLI.exe ffmpeg --download
```

5. Теперь вы можете начать использовать TwitchDownloaderCLI, например:

```
TwitchDownloaderCLI.exe videodownload --id <vod-id-here> -o out.mp4
```

Вы можете найти больше примеров комманд в [CLI README](TwitchDownloaderCLI/README.md#example-commands).

## Linux – Введение

1. У некоторых дистрибутивов, таких как like Linux Alpine, отсутствуют шрифты для некоторых языков (Арабский, Персидский, Тайский и др.). Если у вас отсутствуют шрифты вам нужно установить семейство шрифтов такие как [Noto](https://fonts.google.com/noto/specimen/Noto+Sans) или проверьте wiki страницу вашего дистрибутива про шрифты, там может быть есть команда установки для этого сценария, например такая есть у [Linux Alpine](https://wiki.alpinelinux.org/wiki/Fonts).
2. Убедитесь что `fontconfig` и `libfontconfig1` установлены. `apt-get install fontconfig libfontconfig1` напишите эту команда на Ubuntu чтобы установить.
3. Зайдите в [Releases](https://github.com/lay295/TwitchDownloader/releases/) и скачайте последнюю версию бинарного файла для Linux, получите [AUR Package](https://aur.archlinux.org/packages/twitch-downloader-bin/) для Arch Linux, или [сделайте сборку сами](#делаем-свою-сборку).
4. Достаньте `TwitchDownloaderCLI`.
5. Закиньте его в папку которую вы хотите:

```
cd directory/containing/TwitchDownloaderCLI
```

6. Дайте бинарному файлу права:

```
sudo chmod +x TwitchDownloaderCLI
```

7. a) Если у вас нет FFmpeg, вам надо установить его через пакетный менеджер вашего дистрибутива или через [ffmpeg.org](https://ffmpeg.org/download.html) как standalone файл, также вы можете скачать его через TwitchDownloaderCLI:

```
./TwitchDownloaderCLI ffmpeg --download
```

7. b) Если вы скачали его как standalone файл, вам также нужно дать ему права:

```
sudo chmod +x ffmpeg
```

8. Теперь вы можете начать использовать TwitchDownloaderCLI, например:

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

Вы можете найти больше примеров комманд в [CLI README](TwitchDownloaderCLI/README.md#example-commands).

## MacOS – Введение

1. Если у вас Apple Silicon M-series процессор, удостоверьтесь что вы скачали arm64 бинарный файл, если же вы хотите использовать x64 бинарный файл на Apple Silicon он должен быть запущен через сессию терминала под Rosetta 2:

```
arch -x86_64 zsh
```

2. Зайдите в [Releases](https://github.com/lay295/TwitchDownloader/releases/) и скачайте последнюю версию бинарного файла для MacOS или [сделайте сборку сами](#делаем-свою-сборку).
3. Достаньте TwitchDownloaderCLI.
4. Закиньте его в папку которую вы хотите:

```
cd directory/containing/TwitchDownloaderCLI
```

5. Дайте бинарному файлу права:

```
chmod +x TwitchDownloaderCLI
```

6. a) Если у вас нет FFmpeg, вам надо установить его через [Homebrew package manager](https://brew.sh/), или через ffmpeg.org как standalone файл, также вы можете скачать его через TwitchDownloaderCLI:

```
./TwitchDownloaderCLI ffmpeg --download
```

6. b) Если вы скачали его как standalone файл, вам также нужно дать ему права:

```
chmod +x ffmpeg
```

7. Теперь вы можете начать использовать TwitchDownloaderCLI, например:

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

Вы можете найти больше примеров комманд в [CLI README](TwitchDownloaderCLI/README.md#example-commands).

# Делаем свою сборку

## Требования

- [.NET 10.0.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Примерно 1GB свободного места

## Инструкции сборки

1. Клонируйте репозиторий:

```
git clone https://github.com/lay295/TwitchDownloader.git
```

2. Откройте папку клонированного репозитория:

```
cd TwitchDownloader
```

3. Восстановите решение:

```
dotnet restore
```

- Non-Windows устройствам может понадобится указать проект для восстановления тоесть `dotnet restore TwitchDownloaderCLI`

4. a) Собираем GUI версию:

```
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows
```

4. b) Собираем CLI версию:

```
dotnet publish TwitchDownloaderCLI -p:PublishProfile=<Профили>
```

- Существующие профили: `Windows`, `Linux`, `LinuxAlpine`, `LinuxArm`, `LinuxArm64`, `MacOS`, `MacOSArm64`

5. a) Зайдите в папку с GUI сборкой:

```
cd TwitchDownloaderWPF/bin/Release/net10.0-windows/publish/win-x64
```

5. b) Зайдите в папку с CLI сборкой:

```
cd TwitchDownloaderCLI/bin/Release/net10.0/publish
```

# Благодарности третьим лицам

Рендер чата рендерится с помощью [SkiaSharp](https://github.com/mono/SkiaSharp) и [HarfBuzzSharp](https://github.com/mono/SkiaSharp) © Microsoft Corporation.

Кодирование рендера чата и завершение загрузки видео выполнены с помощью [FFmpeg](https://ffmpeg.org/) © The FFmpeg developers.

Рендер чата может использовать [Noto Color Emoji](https://github.com/googlefonts/noto-emoji) © Google and contributors.

Рендер чата может использовать [Twemoji](https://github.com/twitter/twemoji) © Twitter and contributors.

Поставляемые FFmpeg бинарные файлы загружаются с  [gyan.dev](https://www.gyan.dev/ffmpeg/) © Gyan Doshi.

Бинарные файлы FFmpeg загружаемые во время работы приложения, скачиваются с помощью  [Xabe.FFmpeg.Downloader](https://github.com/tomaszzmuda/Xabe.FFmpeg) © Xabe.

Чат формата Html экспортируется используя шрифт _Inter_ размещённый на [Google Fonts API](https://fonts.google.com/) © Google.

Чтобы посмотреть полный список используемых внешних библиотек, посмотрите [THIRD-PARTY-LICENSES.txt](./TwitchDownloaderCore/Resources/THIRD-PARTY-LICENSES.txt).

# License

[MIT](./LICENSE.txt)

TwitchDownloader никак не связан с Twitch Interactive, Inc. или его аффилированным компаниям.
