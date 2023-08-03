<p align="center">
  <a href="https://github.com/lay295/TwitchDownloader">
    <img src="TwitchDownloaderWPF/Images/Logo.png" alt="Logo" width="80" height="80">
    
  </a>

  <h3 align="center">Twitch Downloader</h3>

  <p align="center">
    Descargador de Twitch VOD/Clip/Chat y Renderizador de Chat
    <br />
    <br />
    <a href="https://github.com/lay295/TwitchDownloader/issues">Reportar un error</a>
  </p>
</p>

**Este archivo Readme podría no estar actualizado, considere revisar el Readme en [**ingles**](README.md)**

## Ejemplo de Renderizado de Chat

https://user-images.githubusercontent.com/1060681/197653099-c3fd12c2-f03a-4580-84e4-63ce3f36be8d.mp4

## ¿Qué puede hacer?

- Descargar VODs de Twitch
- Descargar Clips de Twitch
- Descargar el chat de VODs y Clips, ya sea en un archivo JSON con toda la información original, en un archivo HTML del navegador o en un archivo de texto sin formato
- Actualizar el contenido de un archivo JSON de chat generado previamente con la opción de guardarlo en otro formato
- Usar un archivo JSON de chat generado previamente para renderizar el chat con emojis de Twitter Twemoji o Google Noto Color, y con emotes estáticos y animados de BTTV, FFZ y 7TV

# Interfaz Gráfica de Usuario (GUI)

## Windows WPF

![](https://i.imgur.com/bLegxGX.gif)

### [Ver toda la documentación de WPF aquí](TwitchDownloaderWPF/README.md).

### Funcionalidad

La interfaz gráfica de usuario Windows WPF implementa todas las funciones principales del programa junto con algunas funciones adicionales para mejorar la comodidad de uso:
- Agregar múltiples tareas de descarga/renderizado a la cola para ejecutarlas simultáneamente
- Crear una lista de tareas de descarga a partir de una lista de enlaces de VODs/Clips
- Buscar y descargar múltiples VODs/Clips de cualquier streamer sin salir de la aplicación

### Soporte Multilingüe

La interfaz gráfica de usuario Windows WPF está disponible en varios idiomas gracias a las traducciones de la comunidad. Consulta la [sección de Localización](TwitchDownloaderWPF/README.md#localization) del [README de WPF](TwitchDownloaderWPF/README.md) para obtener más detalles.

### Temas

La interfaz gráfica de usuario Windows WPF incluye temas claros y oscuros, junto con una opción para actualizar automáticamente el tema según el tema actual de Windows. ¡También admite temas creados por los usuarios! Consulta la [sección de Temas](TwitchDownloaderWPF/README.md#theming) del [README de WPF](TwitchDownloaderWPF/README.md) para obtener más detalles.

### Demostración en Video

https://www.youtube.com/watch?v=0W3MhfhnYjk
(versión anterior, mismo concepto)

## ¿Linux?

Consulta twitch-downloader-gui en [github](https://github.com/mohad12211/twitch-downloader-gui) o en el [AUR](https://aur.archlinux.org/packages/twitch-downloader-gui) para obtener una interfaz gráfica de usuario (GUI) para Linux que envuelve la CLI.

## ¿MacOS?

Por ahora, no hay una GUI disponible para MacOS :(

# Interfaz de Línea de Comandos (CLI)

### [Ver toda la documentación de la CLI aquí](TwitchDownloaderCLI/README.md).

La interfaz de línea de comandos (CLI) es multiplataforma e implementa las funciones principales del programa. Funciona en Windows, Linux y MacOS<sup>*</sup>.

<sup>*Solo se han probado las Mac con procesador Intel</sup>

Con la CLI, es posible automatizar el procesamiento de videos mediante scripts externos. Por ejemplo, podrías copiar y pegar el siguiente código en un archivo `.bat` en Windows para descargar un VOD y su chat, y luego renderizar el chat, todo desde una única entrada.

```bat
@echo off
set /p vodid="Ingresa el ID del VOD: "
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json -E
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```

## Windows - Primeros Pasos

1. Ve a [Releases](https://github.com/lay295/TwitchDownloader/releases/) y descarga la última versión para Windows o [compila desde el código fuente](#building-from-source).
2. Extrae `TwitchDownloaderCLI.exe`.
3. Navega hasta el lugar donde extrajiste el archivo en la terminal.
4. Si no tienes FFmpeg, puedes instalarlo a través del [administrador de paquetes Chocolatey](https://community.chocolatey.org/), o puedes obtenerlo como un archivo independiente desde [ffmpeg.org](https://ffmpeg.org/download.html) o utilizando TwitchDownloaderCLI:
```
TwitchDownloaderCLI.exe ffmpeg --download
```
5. Ahora puedes empezar a usar el descargador, por ejemplo:
```
TwitchDownloaderCLI.exe videodownload --id <id-del-vod-aquí> -o out.mp4
```

## Linux – Primeros Pasos

1. Algunas distribuciones, como Linux Alpine, carecen de fuentes para algunos idiomas (árabe, persa, tailandés, etc.). Si este es tu caso, instala familias adicionales de fuentes como [Noto](https://fonts.google.com/noto/specimen/Noto+Sans) o consulta la página de la wiki de tu distribución sobre fuentes, ya que puede tener un comando de instalación para este escenario específico, como la página de fuentes de [Linux Alpine](https://wiki.alpinelinux.org/wiki/Fonts).
2. Asegúrate de que tanto `fontconfig` como `libfontconfig1` estén instalados. Por ejemplo, en Ubuntu, puedes instalarlos con el comando `apt-get install fontconfig libfontconfig1`.
3. Ve a [Releases](https://github.com/lay295/TwitchDownloader/releases/) y descarga el archivo binario más reciente para Linux, toma el [Paquete AUR](https://aur.archlinux.org/packages/twitch-downloader-bin/) para Arch Linux o [compila desde el código fuente](#

building-from-source).
4. Extrae `TwitchDownloaderCLI`.
5. Navega hasta el lugar donde extrajiste el archivo y dale permisos de ejecución en la terminal:
```
sudo chmod +x TwitchDownloaderCLI
```
6. a) Si no tienes FFmpeg, debes instalarlo a través del administrador de paquetes de tu distribución, aunque también puedes obtenerlo como un archivo independiente desde [ffmpeg.org](https://ffmpeg.org/download.html) o usando TwitchDownloaderCLI:
```
./TwitchDownloaderCLI ffmpeg --download
```
6. b) Si lo descargaste como un archivo independiente, también debes darle permisos de ejecución con:
```
sudo chmod +x ffmpeg
```
7. Ahora puedes empezar a usar el descargador, por ejemplo:
```
./TwitchDownloaderCLI videodownload --id <id-del-vod-aquí> -o out.mp4
```

## MacOS – Primeros Pasos

1. Ve a [Releases](https://github.com/lay295/TwitchDownloader/releases/) y descarga el archivo binario más reciente para MacOS o [compila desde el código fuente](#building-from-source).
2. Extrae `TwitchDownloaderCLI`.
3. Navega hasta el lugar donde extrajiste el archivo y dale permisos de ejecución en la terminal:
```
chmod +x TwitchDownloaderCLI
```
4. a) Si no tienes FFmpeg, puedes instalarlo a través del [administrador de paquetes Homebrew](https://brew.sh/), o puedes obtenerlo como un archivo independiente desde [ffmpeg.org](https://ffmpeg.org/download.html) o utilizando TwitchDownloaderCLI:
```
./TwitchDownloaderCLI ffmpeg --download
```
4. b) Si lo descargaste como un archivo independiente, también debes darle permisos de ejecución con:
```
chmod +x ffmpeg
```
5. Ahora puedes empezar a usar el descargador, por ejemplo:
```
./TwitchDownloaderCLI videodownload --id <id-del-vod-aquí> -o out.mp4
```

# Compilación desde el Código Fuente

## Requisitos

- [.NET 6.0.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

## Instrucciones de Compilación

1. Clona el repositorio:
```
git clone https://github.com/lay295/TwitchDownloader.git
```
2. Navega hasta la carpeta de la solución:
```
cd TwitchDownloader
```
3. Restaura la solución:
```
dotnet restore
```
4. a) Compila la GUI:
```
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows -p:DebugType=None -p:DebugSymbols=false
```
4. b) Compila la CLI:
```
dotnet publish TwitchDownloaderCLI -p:PublishProfile=<Perfil> -p:DebugType=None -p:DebugSymbols=false
```
- Perfiles aplicables: `Windows`, `Linux`, `LinuxAlpine`, `LinuxArm`, `LinuxArm64`, `MacOS`
5. a) Navega hasta la carpeta de la compilación de la GUI:
```
cd TwitchDownloaderWPF/bin/Release/net6.0-windows/publish/win-x64
```
5. b) Navega hasta la carpeta de la compilación de la CLI:
```
cd TwitchDownloaderCLI/bin/Release/net6.0/publish
```

# Licencia

[MIT](./LICENSE.txt)

# Créditos de Terceros

Los renderizados de chat se realizan con [SkiaSharp y HarfBuzzSharp](https://github.com/mono/SkiaSharp) © Microsoft Corporation.

Los renderizados de chat se codifican y las descargas de video se finalizan con [FFmpeg](https://ffmpeg.org/) © Los desarrolladores de FFmpeg.

Los renderizados de chat pueden utilizar [Noto Color Emoji](https://github.com/googlefonts/noto-emoji) © Google y colaboradores.

Los renderizados de chat pueden utilizar [Twemoji](https://github.com/twitter/twemoji) © Twitter y colaboradores.

Los binarios de FFmpeg incluidos se obtienen de [gyan.dev](https://www.gyan.dev/ffmpeg/) © Gyan Doshi.

Los binarios de FFmpeg descargados en tiempo de ejecución son descargados utilizando [Xabe.FFmpeg.Downloader](https://github.com/tomaszzmuda/Xabe.FFmpeg) © Xabe.

Las exportaciones de HTML de chat utilizan la tipografía _Inter_ alojada en la [API de Google Fonts](https://fonts.google.com/) © Google.

Para obtener una lista completa de las bibliotecas externas utilizadas, consulta [THIRD-PARTY-LICENSES.txt](./TwitchDownloaderCore/Resources/THIRD-PARTY-LICENSES.txt).
