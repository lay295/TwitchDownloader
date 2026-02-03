<div align="center">
  <a href="https://github.com/lay295/TwitchDownloader">
    <img src="TwitchDownloaderWPF/Images/Logo.png" alt="Logo" width="80" height="80">
  </a>
  <h3 align="center">Twitch Downloader</h3>
  <div align="center">
    Twitch VOD/Clip/Chat-Downloader und Chat-Renderer
    <br />
    <br />
    <a href="https://github.com/lay295/TwitchDownloader/issues">Fehler melden</a>
  </div>
</div>

**Diese README-Datei ist möglicherweise nicht auf dem neuesten Stand. Prüfe zur Sicherheit die [**englische README**](README.md).**

## Was kann das Programm?

- Twitch-VODs herunterladen
- Twitch-Clips herunterladen
- Chats für VODs und Clips herunterladen – entweder als [JSON mit allen Originalinformationen](https://github.com/lay295/TwitchDownloader/files/13495494/ExampleMoonMoonJsonFile.json), als HTML-Datei für den Browser oder als [reine Textdatei](https://github.com/lay295/TwitchDownloader/files/13495523/ExampleMoonMoonTextFile.txt)
- Den Inhalt einer zuvor erzeugten JSON-Chat-Datei aktualisieren und optional in einem anderen Format speichern
- Eine zuvor erzeugte JSON-Chat-Datei verwenden, um den Chat mit Twitter-Twemoji- oder Google-Noto-Color-Emojis sowie BTTV-, FFZ- und 7TV-Emotes (statisch und animiert) zu rendern

### Beispiel für das Chat-Rendering

<https://user-images.githubusercontent.com/1060681/197653099-c3fd12c2-f03a-4580-84e4-63ce3f36be8d.mp4>

# GUI

## Windows WPF

![](https://i.imgur.com/bLegxGX.gif)

### [Hier findest du die vollständige WPF-Dokumentation](TwitchDownloaderWPF/README.md)

### Funktionen

Die Windows-WPF-GUI implementiert alle Hauptfunktionen des Programms sowie einige Komfortfunktionen:

- Mehrere Download-/Rendering-Aufträge zur gleichzeitigen Ausführung in die Warteschlange einreihen
- Eine Liste von Download-Aufträgen aus einer Liste von VOD-/Clip-Links erstellen
- Mehrere VODs/Clips eines beliebigen Streamers direkt in der App suchen und herunterladen

### Mehrsprachigkeit

Die Windows-WPF-GUI ist dank Community-Übersetzungen in mehreren Sprachen verfügbar. Siehe den [Abschnitt zur Lokalisierung](TwitchDownloaderWPF/README.md#localization) im [WPF-README](TwitchDownloaderWPF/README.md) für weitere Details.

### Themes

Die Windows-WPF-GUI enthält sowohl helle als auch dunkle Themes und kann sich live am aktuellen Windows-Theme orientieren. Außerdem werden benutzerdefinierte Themes unterstützt! Weitere Informationen findest du im [Abschnitt zu Themes](TwitchDownloaderWPF/README.md#theming) des [WPF-README](TwitchDownloaderWPF/README.md).

### Video-Demonstration

<https://www.youtube.com/watch?v=0W3MhfhnYjk>
(ältere Version, gleicher Funktionsumfang)

## Linux?

Sieh dir twitch-downloader-gui auf [GitHub](https://github.com/mohad12211/twitch-downloader-gui) oder im [AUR](https://aur.archlinux.org/packages/twitch-downloader-gui) an – das ist eine Linux-GUI-Hülle für die CLI.

## macOS?

Für macOS ist aktuell keine GUI verfügbar :(

# CLI

### [Hier findest du die vollständige CLI-Dokumentation](TwitchDownloaderCLI/README.md)

Die CLI ist plattformübergreifend und implementiert die wichtigsten Funktionen des Programms. Sie läuft unter Windows, Linux und macOS<sup>*</sup>.

<sup>*Nur Intel-Macs wurden getestet</sup>

Mit der CLI lässt sich die Videobearbeitung per Skript automatisieren. Du kannst zum Beispiel den folgenden Code in eine `.bat`-Datei unter Windows kopieren, um einen VOD und dessen Chat herunterzuladen und anschließend den Chat zu rendern – alles mit einer einzigen Eingabe.

```bat
@echo off
set /p vodid="Enter VOD ID: "
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json -E
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4

```

## Windows – Erste Schritte

1. Gehe zu [Releases](https://github.com/lay295/TwitchDownloader/releases/) und lade die neueste Version für Windows herunter oder [erstelle sie aus dem Quellcode](#building-from-source).
2. Extrahiere `TwitchDownloaderCLI.exe`.
3. Navigiere zu dem Ordner, in dem du die ausführbare Datei extrahiert hast:

```
cd C:\folder\containing\TwitchDownloaderCLI
```

4. Falls du FFmpeg nicht hast, kannst du es über den [Chocolatey-Paketmanager](https://community.chocolatey.org/) installieren oder als eigenständige Datei von [ffmpeg.org](https://ffmpeg.org/download.html) herunterladen oder mit TwitchDownloaderCLI verwenden:

```
TwitchDownloaderCLI.exe ffmpeg --download
```

5. Du kannst TwitchDownloaderCLI nun verwenden, zum Beispiel:

```
TwitchDownloaderCLI.exe videodownload --id <vod-id-here> -o out.mp4
```

Weitere Beispielbefehle findest du in der [CLI README](TwitchDownloaderCLI/README.md#example-commands).

## Linux – Erste Schritte

1. Einige Distributionen, wie Linux Alpine, haben keine Schriftarten für manche Sprachen (Arabisch, Persisch, Thai, etc.). Falls das auf dich zutrifft, installiere zusätzliche Schriftartenfamilien wie [Noto](https://fonts.google.com/noto/specimen/Noto+Sans) oder schau auf der Wiki-Seite deiner Distribution nach Schriftarten, da sie möglicherweise einen Installationsbefehl für dieses spezifische Szenario hat, wie beispielsweise die [Linux Alpine](https://wiki.alpinelinux.org/wiki/Fonts) Schriftartenseite.
2. Stelle sicher, dass beide `fontconfig` und `libfontconfig1` installiert sind. `apt-get install fontconfig libfontconfig1` auf Ubuntu.
3. Gehe zu [Releases](https://github.com/lay295/TwitchDownloader/releases/) und lade die neueste Binärdatei für Linux herunter, hole das [AUR-Paket](https://aur.archlinux.org/packages/twitch-downloader-bin/) für Arch Linux oder [erstelle sie aus dem Quellcode](#building-from-source).
4. Extrahiere `TwitchDownloaderCLI`.
5. Navigiere zu dem Ordner, in dem du die Binärdatei extrahiert hast:

```
cd directory/containing/TwitchDownloaderCLI
```

6. Gib der Binärdatei Ausführungsrechte:

```
sudo chmod +x TwitchDownloaderCLI
```

7. a) Falls du FFmpeg nicht hast, solltest du es systemweit über den Paketmanager deiner Distribution installieren, du kannst es aber auch als eigenständige Datei von [ffmpeg.org](https://ffmpeg.org/download.html) herunterladen oder mit TwitchDownloaderCLI verwenden:

```
./TwitchDownloaderCLI ffmpeg --download
```

7. b) Falls du es als eigenständige Datei heruntergeladen hast, musst du ihm auch Ausführungsrechte geben mit:

```
sudo chmod +x ffmpeg
```

8. Du kannst TwitchDownloaderCLI nun verwenden, zum Beispiel:

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

Weitere Beispielbefehle findest du in der [CLI README](TwitchDownloaderCLI/README.md#example-commands).

## MacOS – Erste Schritte

1. Falls dein Gerät einen Apple Silicon M-Series-Prozessor hat, stelle sicher, dass du die arm64-Binärdatei herunterlädst. Falls du jedoch die x64-Binärdatei auf Apple Silicon verwenden möchtest, muss sie über eine Terminalsitzung ausgeführt werden, die unter Rosetta 2 läuft:

```
arch -x86_64 zsh
```

2. Gehe zu [Releases](https://github.com/lay295/TwitchDownloader/releases/) und lade die neueste Binärdatei für MacOS herunter oder [erstelle sie aus dem Quellcode](#building-from-source).
3. Extrahiere `TwitchDownloaderCLI`.
4. Navigiere zu dem Ordner, in dem du die Binärdatei extrahiert hast:

```
cd directory/containing/TwitchDownloaderCLI
```

5. Gib der Binärdatei Ausführungsrechte im Terminal:

```
chmod +x TwitchDownloaderCLI
```

6. a) Falls du FFmpeg nicht hast, kannst du es systemweit über den [Homebrew-Paketmanager](https://brew.sh/) installieren oder du kannst es als eigenständige Datei von [ffmpeg.org](https://ffmpeg.org/download.html) herunterladen oder mit TwitchDownloaderCLI verwenden:

```
./TwitchDownloaderCLI ffmpeg --download
```

6. b) Falls du es als eigenständige Datei heruntergeladen hast, musst du ihm auch Ausführungsrechte geben mit:

```
chmod +x ffmpeg
```

7. Du kannst TwitchDownloaderCLI nun verwenden, zum Beispiel:

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

Weitere Beispielbefehle findest du in der [CLI README](TwitchDownloaderCLI/README.md#example-commands).

## Aus dem Quellcode erstellen

### Anforderungen

- [.NET 10.0.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Etwa 1GB Speicherplatz

## Build-Anweisungen

1. Clone das Repository:

```
git clone https://github.com/lay295/TwitchDownloader.git
```

2. Navigiere zum Lösungsordner:

```
cd TwitchDownloader
```

3. Stelle die Lösung wieder her:

```
dotnet restore
```

- Nicht-Windows-Geräte müssen möglicherweise explizit ein Projekt angeben, das wiederhergestellt werden soll, z. B. `dotnet restore TwitchDownloaderCLI`

4. a) GUI erstellen:

```
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows
```

4. b) CLI erstellen:

```
dotnet publish TwitchDownloaderCLI -p:PublishProfile=<Profile>
```

- Anwendbare Profile: `Windows`, `Linux`, `LinuxAlpine`, `LinuxArm`, `LinuxArm64`, `MacOS`, `MacOSArm64`

5. a) Navigiere zum GUI-Build-Ordner:

```
cd TwitchDownloaderWPF/bin/Release/net10.0-windows/publish/win-x64
```

5. b) Navigiere zum CLI-Build-Ordner:

```
cd TwitchDownloaderCLI/bin/Release/net10.0/publish
```

## Danksagungen an Dritte

Chat-Renders werden mit [SkiaSharp](https://github.com/mono/SkiaSharp) und [HarfBuzzSharp](https://github.com/mono/SkiaSharp) © Microsoft Corporation gerendert.

Chat-Renders werden kodiert und Video-Downloads werden mit [FFmpeg](https://ffmpeg.org/) © Die FFmpeg-Entwickler abgeschlossen.

Chat-Renders können [Noto Color Emoji](https://github.com/googlefonts/noto-emoji) © Google und Mitwirkende verwenden.

Chat-Renders können [Twemoji](https://github.com/twitter/twemoji) © Twitter und Mitwirkende verwenden.

Gebündelte FFmpeg-Binärdateien werden von [gyan.dev](https://www.gyan.dev/ffmpeg/) © Gyan Doshi abgerufen.

FFmpeg-Binärdateien, die zur Laufzeit abgerufen werden, werden mit [Xabe.FFmpeg.Downloader](https://github.com/tomaszzmuda/Xabe.FFmpeg) © Xabe heruntergeladen.

Chat-HTML-Exporte verwenden die _Inter_-Schriftart, die von der [Google Fonts API](https://fonts.google.com/) © Google gehostet wird.

Eine vollständige Liste der verwendeten externen Bibliotheken findest du in [THIRD-PARTY-LICENSES.txt](./TwitchDownloaderCore/Resources/THIRD-PARTY-LICENSES.txt).

## Lizenz

[MIT](./LICENSE.txt)

TwitchDownloader ist in keiner Weise mit Twitch Interactive, Inc. oder seinen Tochtergesellschaften verbunden.
