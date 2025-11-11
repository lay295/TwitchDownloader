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
