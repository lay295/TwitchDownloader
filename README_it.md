<div align="center">
  <a href="https://github.com/lay295/TwitchDownloader">
    <img src="TwitchDownloaderWPF/Images/Logo.png" alt="Logo" width="80" height="80">
  </a>

  <h3 align="center">Twitch Downloader</h3>

  <div align="center">
    Twitch VOD/Clip/Chat Downloader and Chat Renderer
    <br />
    <br />
    <a href="https://github.com/lay295/TwitchDownloader/issues">Report Bug</a>
  </div>
</div>

Questo archivio Readme potrebbe non essere aggiornato, considera la visioone del [**Readme in Inglese**](README.md)

## Esempio di rendering della Chat

https://user-images.githubusercontent.com/1060681/197653099-c3fd12c2-f03a-4580-84e4-63ce3f36be8d.mp4


## Cosa può fare?

- Scaricare i VODs di Twitch
- Scaricare le Clips di Twitch
- Scaricare la chat per i VOD e le clip, sia in un formato [JSON with all the original information](https://github.com/lay295/TwitchDownloader/files/13495494/ExampleMoonMoonJsonFile.json), un file HTML, o un [file di testo](https://github.com/lay295/TwitchDownloader/files/13495523/ExampleMoonMoonTextFile.txt)
- Aggiornare il contenuto di un file di chat JSON generato in precedenza con un'opzione di salvataggio in un altro formato.
- Utilizzare un file di chat JSON generato in precedenza per renderizzare la chat con le emoji di Twitter Twemoji o Google Noto Color e le emotes statiche e animate di BTTV, FFZ, 7TV.

# GUI

## Windows WPF

![](https://i.imgur.com/bLegxGX.gif)

### [Guarda la documentazione di WPF qui](TwitchDownloaderWPF/README.md).

### Funzionalità

La GUI di Windows WPF implementa tutte le funzioni base del programma e alcune funzioni aggiuntive per la qualità delle stesse:
- Accodare più lavori di download/rendering da eseguire simultaneamente
- Creare un elenco di lavori di download da un elenco di link a vod/clip
- Cercate e scaricate più VOD/clip da qualsiasi streamer senza chiudere l'app.

### Supporto al multi linguaggio

La GUI di Windows WPF è disponibile in molteplici linguaggio grazie alle traduzioni della community. Guarda la [sezione localizzazione](TwitchDownloaderWPF/README.md#localization) o il [WPF README](TwitchDownloaderWPF/README.md) per più dettagli.

### Temi

L'interfaccia grafica WPF di Windows viene fornita con temi chiari e scuri e con l'opzione di aggiornamento live in base al tema corrente di Windows. Supporta anche temi creati dall'utente! Guarda la [sezione temi](TwitchDownloaderWPF/README.md#theming) di [WPF README](TwitchDownloaderWPF/README.md) per più dettagli.

### Dimostrazione Video

https://www.youtube.com/watch?v=0W3MhfhnYjk
(versione più vecchia, stesso concetto)

## Linux?

Controlla twitch-downloader-gui su [github](https://github.com/mohad12211/twitch-downloader-gui) o la [AUR](https://aur.archlinux.org/packages/twitch-downloader-gui) per un wrapper Linux GUI per la CLI.

## MacOS?

Non è ancora disponibile nessuna GUI per MacOS :(

# CLI

### [Guarda la documentazione completa del CLI qui](TwitchDownloaderCLI/README.md).

Il CLI è cross-platform ed implementa le funzioni principali del programma. Funzione su Windows, Linux, e MacOS<sup>*</sup>.

<sup>*Solo i Mac con Intel sono stati testati</sup>

Con il Cli, è possibile automatizzare l'elaborazione video utilizzando script esterni. Per esempio, puoi copiare il seguente codice in un file `.bat`  su Windows per scaricare un VOD e la sua chat, renderizzandola, tutto in un singolo input.
```bat
@echo off
set /p vodid="Enter VOD ID: "
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json -E
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```

## Windows - Come iniziare

1. Vai a [Releases](https://github.com/lay295/TwitchDownloader/releases/) e scarica l'ultima versione per Windows o [compilala dal sorgente](#building-from-source).
2. Estrai `TwitchDownloaderCLI.exe`.
3. Vai a adove hai estratto l'eseguibile:
```
cd C:\folder\containing\TwitchDownloaderCLI
```
4. Se non hai FFmpeg, puoi installarlo con [Chocolatey package manager](https://community.chocolatey.org/), o puoi averlo standalone da [ffmpeg.org](https://ffmpeg.org/download.html) o usando TwitchDownloaderCLI:
```
TwitchDownloaderCLI.exe ffmpeg --download
```
5. Puoi ora iniziare ad usare TwitchDownloaderCLI, per esempio:
```
TwitchDownloaderCLI.exe videodownload --id <vod-id-here> -o out.mp4
```
Puoi trovare più esempi di comandi in [CLI README](TwitchDownloaderCLI/README.md#example-commands).

## Linux – Come iniziare

1. Alcune distro, come Linux Alpine, mancano di font per alcune lingue (Arabo, Persiano, Thai, etc.) Se è il tuo caso, installa font addizionali come [Noto](https://fonts.google.com/noto/specimen/Noto+Sans) o controlla la wiki della tua distro sui font visto che potrebbe avere un comando specifico per l'installazione, come la pagina [Linux Alpine](https://wiki.alpinelinux.org/wiki/Fonts) per i font.
2. Assicurati che `fontconfig` e `libfontconfig1` siano installati. `apt-get install fontconfig libfontconfig1` su Ubuntu.
3. Vai a [Releases](https://github.com/lay295/TwitchDownloader/releases/) e scarica l'ultimo file binario per Linux, prendi il [pacchetto AUR](https://aur.archlinux.org/packages/twitch-downloader-bin/) per Arch Linux, o [compila dal sorgente](#building-from-source).
5. Estrai `TwitchDownloaderCLI`.
6. Naviga dove hai estratto il binario:
```
cd directory/containing/TwitchDownloaderCLI
```
6. Dai al binario i diritti di eseguibile:
```
sudo chmod +x TwitchDownloaderCLI
```
7. a) Se non hai FFmpeg, dovresti installarlo a livello di sistema tramite il gestore di pacchetti, tuttavia si può anche ottenere come un file standalone da [ffmpeg.org](https://ffmpeg.org/download.html) o usando TwitchDownloaderCLI:
```
./TwitchDownloaderCLI ffmpeg --download
```
7. b) Se scaricato come file standalone, devi eseguirlo con diritti di eseguibile con:
```
sudo chmod +x ffmpeg
```
8. Puoi ora iniziare ad usare TwitchDownloaderCLI, per esempio:
```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```
Puoi trovare più esempio in [CLI README](TwitchDownloaderCLI/README.md#example-commands).

## MacOS – Come iniziare
1. Se il tuo dispositivo ha un processore Apple Silicon M-series, assicurati di scaricare il binario per arm64, però se si desidera utilizzare il binario x64 su Apple Silicon deve essere eseguito tramite una sessione terminale in esecuzione sotto Rosetta 2:
```
arch -x86_64 zsh
```
2. Vai a [Releases](https://github.com/lay295/TwitchDownloader/releases/) e scarica l'ultimo binario per MacOS o [compilalo dal sorgente](#building-from-source).
3. Estrai `TwitchDownloaderCLI`.
4. Naviga nella cartella dove hai estratto il binario:
```
cd directory/containing/TwitchDownloaderCLI
```
5. Dai i diritti eseguibili binari nel terminale:
```
chmod +x TwitchDownloaderCLI
```
6. a) Se non si dispone di FFmpeg, è possibile installarlo a livello di sistema tramite [Homebrew package manager](https://brew.sh/), o puoi ottenerlo come un file standalone da [ffmpeg.org](https://ffmpeg.org/download.html) o usando TwitchDownloaderCLI:
```
./TwitchDownloaderCLI ffmpeg --download
```
6. b) Se scaricato come un file standalone, si deve anche dare diritti eseguibili con:
```
chmod +x ffmpeg
```
7. Puoi ora utilizzare TwitchDownloaderCLI, per esempio:
```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```
Puoi trovare altri esempi nel [CLI README](TwitchDownloaderCLI/README.md#example-commands).

# Compilare il sorgente

## Requisiti

- [.NET 6.0.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- Circa 1GB di spazio su disco

## Istruzioni

1. Clona la repository:
```
git clone https://github.com/lay295/TwitchDownloader.git
```
2. Naviga nella cartella:
```
cd TwitchDownloader
```
3. Ripristina:
```
dotnet restore
```
- Dispositivi diversi da Windows potrebbero necessitare di specificare esplicitamente un progetto da ripristinare, i.e. `dotnet restore TwitchDownloaderCLI`
4. a) Costruisci la GUI:
```
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows
```
4. b) Costruisci il CLI:
```
dotnet publish TwitchDownloaderCLI -p:PublishProfile=<Profile>
```
- Profili Applicabili: `Windows`, `Linux`, `LinuxAlpine`, `LinuxArm`, `LinuxArm64`, `MacOS`, `MacOSArm64`
5. a) Naviga nella cartella della GUI:
```
cd TwitchDownloaderWPF/bin/Release/net6.0-windows/publish/win-x64
```
5. b) Naviga nella cartella del CLI:
```
cd TwitchDownloaderCLI/bin/Release/net6.0/publish
```

# Crediti di Terze Parti

I rendering di chat sono resi con [SkiaSharp](https://github.com/mono/SkiaSharp) e [HarfBuzzSharp](https://github.com/mono/SkiaSharp) © Microsoft Corporation.

I rendering di chat sono codificati e i download video sono finalizzati con [FFmpeg](https://ffmpeg.org/) © The FFmpeg developers.

I rendering di chat potrebbero usare [Noto Color Emoji](https://github.com/googlefonts/noto-emoji) © Google e collaboratori.

I rendering di chat potrebbero usare [Twemoji](https://github.com/twitter/twemoji) © Twitter e collaboratori.

I binari FFmpeg in bundle sono recuperati da [gyan.dev](https://www.gyan.dev/ffmpeg/) © Gyan Doshi.

I binari FFmpeg recuperati vengono scaricati usando [Xabe.FFmpeg.Downloader](https://github.com/tomaszzmuda/Xabe.FFmpeg) © Xabe.

Le esportazioni di Chat Html utilizzano il carattere _Inter_ ospitato dal [Google Fonts API](https://fonts.google.com/) © Google.

Per un elenco completo delle librerie esterne utilizzate, vedere [THIRD-PARTY-LICENSES.txt](./TwitchDownloaderCore/Resources/THIRD-PARTY-LICENSES.txt).

# Licenza

[MIT](./LICENSE.txt)

TwitchDownloader non è in nessuna maniera assiociata con Twitch Interactive, Inc. o i suoi affiliati.
