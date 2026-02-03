<div align="center">
  <a href="https://github.com/lay295/TwitchDownloader">
    <img src="TwitchDownloaderWPF/Images/Logo.png" alt="Logo" width="80" height="80">
  </a>
  <h3 align="center">Twitch Downloader</h3>
  <div align="center">
    Téléchargeur de VOD/Clips/Chats Twitch et moteur de rendu de chat
    <br />
    <br />
    <a href="https://github.com/lay295/TwitchDownloader/issues">Signaler un bug</a>
  </div>
</div>

## Que peut faire cet outil ?

- Télécharger des VOD Twitch
- Télécharger des Clips Twitch
- Extraire le chat des VODs et des clips, au choix : au format JSON (avec toutes les informations d’origine), en HTML (affichable dans un navigateur) ou en texte brut
- Mettre à jour le contenu d’un fichier JSON de chat déjà généré et le sauvegarder éventuellement dans un autre format
- Utiliser un fichier de chat JSON existant pour restituer le chat avec les emojis Twitter Twemoji ou Google Noto Color, ainsi qu’avec les émotes BTTV, FFZ et 7TV (statiques et animées)

### Exemple de rendu de chat

<https://user-images.githubusercontent.com/1060681/197653099-c3fd12c2-f03a-4580-84e4-63ce3f36be8d.mp4>

# Interface graphique (GUI)

## Windows WPF

![](https://i.imgur.com/bLegxGX.gif)

### [Documentation complète du WPF ici](TwitchDownloaderWPF/README.md)

### Fonctions principales
L’interface graphique Windows WPF intègre toutes les principales fonctions du programme, ainsi que plusieurs fonctionnalités pratiques supplémentaires :

- Mettre en file d’attente plusieurs tâches de téléchargement/rendu pour les exécuter simultanément
- Créer une liste de téléchargements à partir d’une liste de liens de VODs ou de clips
- Rechercher et télécharger plusieurs VODs ou clips d’un streamer sans quitter l’application

### Support multilingue

L’interface WPF est traduite dans plusieurs langues grâce à la communauté. Voir la section [Localisation](TwitchDownloaderWPF/README.md#localization) du README WPF pour plus d’informations.

### Thèmes

L’interface propose thèmes clair et sombre, une option de synchronisation automatique avec le thème Windows et la prise en charge de thèmes personnalisés. Voir la section [Thématisation](TwitchDownloaderWPF/README.md#theming) du README WPF pour les détails.

### Démonstration vidéo

<https://www.youtube.com/watch?v=0W3MhfhnYjk>
(version plus ancienne, principe identique)

## Linux ?

Consultez twitch-downloader-gui sur [github](https://github.com/mohad12211/twitch-downloader-gui) ou dans l'AUR ([Arch Linux](https://aur.archlinux.org/packages/twitch-downloader-gui)) pour une interface graphique Linux basée sur la version en ligne de commande.

## MacOS ?

Aucune interface graphique n’est encore disponible pour macOS :(

# Interface en ligne de commande (CLI)

### [Documentation complète du CLI ici](TwitchDownloaderCLI/README.md)

La CLI est multiplateforme et implémente les principales fonctionnalités du programme. Elle fonctionne sur Windows, Linux et macOS<sup>*</sup>.

<sup>*Seuls les Mac Intel ont été testés</sup>

Avec la CLI, il est possible d’automatiser le traitement des vidéos à l’aide de scripts externes.
Par exemple, vous pouvez copier-coller le code suivant dans un fichier `.bat` sous Windows pour télécharger une VOD, son chat, uis générer le rendu du chat le tout à partir d’une seule saisie :

```bat
@echo off
set /p vodid="Enter VOD ID: "
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json -E
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```

## Windows — Démarrage

1. Rendez-vous sur la page [Releases](https://github.com/lay295/TwitchDownloader/releases/) et téléchargez la dernière version pour Windows ou [compilez depuis la source](#building-from-source).
2. Extrayez `TwitchDownloaderCLI.exe`.
3. Ouvrez le dossier où vous avez extrait l’exécutable :

```
cd C:\dossier\contenant\TwitchDownloaderCLI
```

4. Si vous n’avez pas FFmpeg, installez-le via [Chocolatey](https://community.chocolatey.org/), ou télécharger le comme fichier autonome depuis [ffmpeg.org](https://ffmpeg.org/download.html), ou encore via TwitchDownloaderCLI :

```
TwitchDownloaderCLI.exe ffmpeg --download
```

5. Vous pouvez maintenant commencer à utiliser TwitchDownloaderCLI, par exemple :

```
TwitchDownloaderCLI.exe videodownload --id <vod-id-here> -o out.mp4
```

Vous trouverez plus d’exemples de commandes dans le [README de la CLI](TwitchDownloaderCLI/README.md#example-commands).

## Linux — Démarrage

1. Certaines distributions (par ex. Alpine) n’incluent pas toutes les polices nécessaires pour certaines langues (arabe, persan, thaï, etc.). Si besoin, installez des familles de polices supplémentaires comme [Noto](https://fonts.google.com/noto/specimen/Noto+Sans) ou consultez la page wiki de votre distribution.
2. Assurez-vous que `fontconfig` et `libfontconfig1` sont installés. Sur Ubuntu : `apt-get install fontconfig libfontconfig1`.
3. Téléchargez le binaire Linux depuis la page [Releases](https://github.com/lay295/TwitchDownloader/releases/), utilisez le paquet AUR pour Arch ou [compilez depuis la source](#building-from-source).
4. Extrayez `TwitchDownloaderCLI`.
5. Ouvrez le dossier où vous avez extrait le binaire :

```
cd directory/containing/TwitchDownloaderCLI
```

6. Rendez le binaire exécutable :

```
sudo chmod +x TwitchDownloaderCLI
```

7. a) Si vous n’avez pas FFmpeg, installez-le via le gestionnaire de paquets de votre distribution ou téléchargez le binaire autonome depuis [ffmpeg.org](https://ffmpeg.org/download.html), ou utilisez TwitchDownloaderCLI :

```
./TwitchDownloaderCLI ffmpeg --download
```

7. b) Si vous avez téléchargé FFmpeg en binaire autonome, donnez-lui aussi les droits d’exécution :

```
sudo chmod +x ffmpeg
```

8. Vous pouvez maintenant utiliser TwitchDownloaderCLI, par exemple :

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

Vous trouverez plus d’exemples de commandes dans le [README de la CLI](TwitchDownloaderCLI/README.md#example-commands).

## MacOS — Démarrage

1.  Si votre appareil possède un processeur **Apple Silicon (M1, M2, etc.)**, assurez-vous de télécharger le binaire **arm64**. Si vous souhaitez utiliser le binaire **x64** sur Apple Silicon, il doit être exécuté via une session terminal sous **Rosetta 2** :


```
arch -x86_64 zsh
```

2. Téléchargez le binaire macOS depuis [Releases](https://github.com/lay295/TwitchDownloader/releases/) ou [compilez depuis la source](#building-from-source).
3. Extrayez `TwitchDownloaderCLI`.
4. Ouvrez le dossier où vous avez extrait le binaire :

```
cd dossier/contenant/TwitchDownloaderCLI
```

5. Rendez le binaire exécutable :

```
chmod +x TwitchDownloaderCLI
```

6. a)  Si vous n’avez pas **FFmpeg**, vous pouvez l’installer via le [gestionnaire de paquets Homebrew](https://brew.sh/), le télécharger depuis [ffmpeg.org](https://ffmpeg.org/download.html), ou le récupérer via TwitchDownloaderCLI :


```
./TwitchDownloaderCLI ffmpeg --download
```

6. b)  Si vous l’avez téléchargé séparément, donnez-lui aussi les droits d’exécution :

```
chmod +x ffmpeg
```

7. Vous pouvez maintenant utiliser TwitchDownloaderCLI, par exemple :

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

Des exemples supplémentaires sont disponibles dans le [README de la CLI](TwitchDownloaderCLI/README.md#example-commands).


# Compilation depuis la source

## Prérequis

- SDK [.NET 10.0.x](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Environ 1 Go d’espace disque

## Instructions de compilation

1. Clonez le dépôt :

```
git clone https://github.com/lay295/TwitchDownloader.git
```

2. Placez-vous dans le dossier de la solution :

```
cd TwitchDownloader
```

3. Restaurez la solution :

```
dotnet restore
```

- Sur certains systèmes non-Windows, il peut être nécessaire de préciser le projet à restaurer, par ex. `dotnet restore TwitchDownloaderCLI`

4. a) Compiler l’interface GUI :

```
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows
```

4. b) Compiler la CLI :

```
dotnet publish TwitchDownloaderCLI -p:PublishProfile=<Profile>
```

- Profils disponibles : `Windows`, `Linux`, `LinuxAlpine`, `LinuxArm`, `LinuxArm64`, `MacOS`, `MacOSArm64`

5. a) Accédez au dossier de build du GUI :

```
cd TwitchDownloaderWPF/bin/Release/net10.0-windows/publish/win-x64
```

5. b) Accédez au dossier de build de la CLI :

```
cd TwitchDownloaderCLI/bin/Release/net10.0/publish
```

# Crédits tiers

Les rendus de chat utilisent [SkiaSharp](https://github.com/mono/SkiaSharp) et [HarfBuzzSharp](https://github.com/mono/SkiaSharp) © Microsoft Corporation.

Les rendus de chat sont encodés et les vidéos finalisées avec [FFmpeg](https://ffmpeg.org/) © Les développeurs de FFmpeg.

Les rendus de chat peuvent inclure [Noto Color Emoji](https://github.com/googlefonts/noto-emoji) © Google et contributeurs.

Les rendus de chat peuvent inclure [Twemoji](https://github.com/twitter/twemoji) © Twitter et contributeurs.

Les binaires FFmpeg inclus sont fournis par [gyan.dev](https://www.gyan.dev/ffmpeg/) © Gyan Doshi.

Les téléchargements de binaires FFmpeg à l’exécution utilisent [Xabe.FFmpeg.Downloader](https://github.com/tomaszzmuda/Xabe.FFmpeg) © Xabe.

Les exports HTML du chat utilisent la police _Inter_ hébergée par l’API [Google Fonts](https://fonts.google.com/) © Google.

Pour la liste complète des bibliothèques externes utilisées, voir [THIRD-PARTY-LICENSES.txt](./TwitchDownloaderCore/Resources/THIRD-PARTY-LICENSES.txt).

# Licence

[MIT](./LICENSE.txt)

TwitchDownloader n’est en aucun cas affilié à Twitch Interactive, Inc. ni à ses sociétés partenaires.
