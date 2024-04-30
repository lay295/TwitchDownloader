<div align="center">
  <a href="https://github.com/lay295/TwitchDownloader">
    <img src="TwitchDownloaderWPF/Images/Logo.png" alt="Logo" width="80" height="80">
  </a>
  <h3 align="center">Twitch Downloader</h3>
  <div align="center">
 Baixador de VOD/Clip/Chat da Twitch e Renderizador de Chat
    <br />
    <br />
    <a href="https://github.com/lay295/TwitchDownloader/issues">Report Bug</a>
  </div>
</div>

**Este arquivo Readme pode estar desatualizado, considere lê-lo em [**inglês**](README.md)**

## Exemplo do Chat Renderizado

<https://user-images.githubusercontent.com/1060681/197653099-c3fd12c2-f03a-4580-84e4-63ce3f36be8d.mp4>

## O quê ele pode fazer?

- Baixar VODs da Twitch
- Baixar Clips da Twitch
- Baixar o chat para VODs e Clips, seja em um arquivo [JSON com todas as informações originais](https://github.com/lay295/TwitchDownloader/files/13495494/ExampleMoonMoonJsonFile.json), um arquivo de browser HTML, ou um [arquivo de texto normal](https://github.com/lay295/TwitchDownloader/files/13495523/ExampleMoonMoonTextFile.txt)
- Atualizar os conteúdos de um arquivo do chat em JSON gerado préviamente com uma opção para salvar em outro formato
- Usar um arquivo do chat em JSON gerado préviamente para renderizar o chat com o Twitter Twemoji ou Google Noto Color emojis e emotes BTTV, FFZ, 7TV estáticos e animados

# GUI

## Windows WPF

![](https://i.imgur.com/bLegxGX.gif)

### [Veja a documentação completa do WPF aqui](TwitchDownloaderWPF/README.md)

### Funcionalidade

O GUI do Windows WPF implementa todas as principais funcionalidades do programa acompanhado com umas funcionalidades extras de conveniência:

- Programe múltiplos trabalhos de baixar/renderizar para que funcionem simultâneamente
- Criar uma lista de arquivos para baixar por uma lista de links de vods/clips
- Buscar e baixar múltiplos VODs/clips de qualquer streamer sem sair do aplicativo

### Suporte à Múltiplas línguas

O GUI do Windows WPF está disponível em múltiplas linguagens graças à traduções da comunidade. Veja a [Sessão de localização](TwitchDownloaderWPF/README.md#localization) do [WPF README](TwitchDownloaderWPF/README.md) para mais detalhes.

### Tema

O GUI do Windows WPF vêm com tanto o tema claro como o escuro, junto de uma opção para atualizar automaticamente de acordo com o tema do Windows. Ele também suporta temas criados pelo usuário! Veja a [Sessão de Tema](TwitchDownloaderWPF/README.md#theming) dos [WPF README](TwitchDownloaderWPF/README.md) para mais detalhes.

### Demonstração em Vídeo

<https://www.youtube.com/watch?v=0W3MhfhnYjk>
(versão mais antiga, mesmo conceito)

## Linux?

Veja twitch-downloader-gui no [github](https://github.com/mohad12211/twitch-downloader-gui) ou no [AUR](https://aur.archlinux.org/packages/twitch-downloader-gui) para um GUI wrapper para o CLI Linux .

## MacOS?

Nenhum GUI disponível pro MacOS até o momento :(

# CLI

### [Veja a documentação completa do CLI aqui](TwitchDownloaderCLI/README.md)

O CLI é cross-platform e implementa as principais funcionalidades do programa. Funciona no  Windows, Linux, e MacOS<sup>*</sup>.

<sup>*Somente Macs com Intel foram testadas</sup>

Com o CLI, é possível automatizar processamento de vídeo processing com scripts externos. Por exemplo, você poderia copiar e colar o código a seguir num arquivo `.bat` no Windows para baixar um VOD e seu chat, e depois renderizar o chat, tudo de um único valor.

```bat
@echo off
set /p vodid="Insira a ID da VOD: "
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json -E
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```

## Windows - Instalação

1. Vá para [Releases](https://github.com/lay295/TwitchDownloader/releases/) e baixe a versão mais recente para Windows ou [construa do código-fonte](#building-from-source).
2. Extraia o `TwitchDownloaderCLI.exe`.
3. Navegue para onde você extraiu o executável:

```
cd C:\folder\containing\TwitchDownloaderCLI
```

4. Se você não tem o FFmpeg, você pode instalar-lo via o [gerenciador de pacotes Chocolatey](https://community.chocolatey.org/), todavia também podes obter-lo como um arquivo separado no [ffmpeg.org](https://ffmpeg.org/download.html) ou usando o TwitchDownloaderCLI:

```
TwitchDownloaderCLI.exe ffmpeg --download
```

5. Você pode começar a usar o TwitchDownloaderCLI, por exemplo:

```
TwitchDownloaderCLI.exe videodownload --id <vod-id-here> -o out.mp4
```

Você pode encontrar mais comandos no [CLI README](TwitchDownloaderCLI/README.md#example-commands).

## Linux – Instalação

1. Algumas distros, como o Alpine Linux, não têm fontes para algumas linguagens (Árabe, Persa, Tailandês, etc.) Se este for seu caso, instale fontes familiares adicionais como [Noto](https://fonts.google.com/noto/specimen/Noto+Sans) ou leia a página da wiki de sua distro em fontes, tendo que deve haver um comando de instalação para essa situação específica, como a página de fontes do [Alpine Linux](https://wiki.alpinelinux.org/wiki/Fonts).
2. Verifique que ambos `fontconfig` e `libfontconfig1` estão instalados. `apt-get install fontconfig libfontconfig1` no Ubuntu.
3. Vá para [Releases](https://github.com/lay295/TwitchDownloader/releases/) e baixe o binário mais recente pro Linux, pegue o [Pacote AUR](https://aur.archlinux.org/packages/twitch-downloader-bin/) pro Arch Linux, ou [construa do código-fonte](#building-from-source).
4. Extraia o `TwitchDownloaderCLI`.
5. Navegue para onde você extraiu o binário:

```
cd directory/containing/TwitchDownloaderCLI
```

6. Dê ao binário direitos de execução:

```
sudo chmod +x TwitchDownloaderCLI
```

7. a) Se você não tem o FFmpeg, você deve instalar-lo no sistema todo pelo gerenciador de pacotes da sua distro, todavia também podes obter-lo como um arquivo separado no [ffmpeg.org](https://ffmpeg.org/download.html) ou usando o TwitchDownloaderCLI:

```
./TwitchDownloaderCLI ffmpeg --download
```

7. b) Se baixado como um arquivo separado, também deve dar-lo direitos de execução com:

```
sudo chmod +x ffmpeg
```

8. Você pode começar usando o TwitchDownloaderCLI, por exemplo:

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

Você pode encontrar mais comandos no [CLI README](TwitchDownloaderCLI/README.md#example-commands).

## MacOS – Instalação

1. Se seu dispositivo tem um processador série-M Apple Silicon, certifique-se que baixaste o binário do arm64, todavia se você prefere usar o binário em x64 no Apple Silicon deve ser rodado por uma sessão do terminal rodando dentro do Rosetta 2:

```
arch -x86_64 zsh
```

2. Vá para [Releases](https://github.com/lay295/TwitchDownloader/releases/) e baixe a versão mais recente para MacOS ou [construa do código-fonte](#building-from-source).
3. Extraia o `TwitchDownloaderCLI`.
4. Navegue para onde você extraiu o binário:

```
cd directory/containing/TwitchDownloaderCLI
```

5. Dê ao binário direitos de execução:

```
chmod +x TwitchDownloaderCLI
```

6. a) Se você não tem o FFmpeg, você deve instalar-lo no sistema todo pelo [gerenciador de pacotes Homebrew](https://brew.sh/), todavia também podes obter-lo como um arquivo separado no [ffmpeg.org](https://ffmpeg.org/download.html) ou usando o TwitchDownloaderCLI:

```
./TwitchDownloaderCLI ffmpeg --download
```

6. b) Se baixado como um arquivo separado, também deve dar-lo direitos de execução com:

```
chmod +x ffmpeg
```

7. Você pode começar a usar o TwitchDownloaderCLI, por exemplo:

```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

Você pode encontrar mais comandos no [CLI README](TwitchDownloaderCLI/README.md#example-commands).

# Construíndo do código-fonte

## Requerimentos

- [.NET 6.0.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- Mais ou menos 1GB de espaço de disco.

## Instruções para construção

1. Clone o repositório:

```
git clone https://github.com/lay295/TwitchDownloader.git
```

2. Navegue até a pasta da solução:

```
cd TwitchDownloader
```

3. Restaure a solução:

```
dotnet restore
```

- Dispositivos que não usam Windows devem especificar explicitamente um projeto para restaurar, i.e. `dotnet restore TwitchDownloaderCLI`

4. a) Construa o GUI:

```
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows
```

- Perfis Aplicáveis: `Windows`, `Linux`, `LinuxAlpine`, `LinuxArm`, `LinuxArm64`, `MacOS`, `MacOSArm64`

5. a) Navegue para a pasta do GUI:

```
cd TwitchDownloaderWPF/bin/Release/net6.0-windows/publish/win-x64
```

5. b) Navegue para a pasta do CLI:

```
cd TwitchDownloaderCLI/bin/Release/net6.0/publish
```

# Créditos de terçeiros

Renderizações do Chat são renderizadas com [SkiaSharp](https://github.com/mono/SkiaSharp) e [HarfBuzzSharp](https://github.com/mono/SkiaSharp) © Microsoft Corporation.

Renderizações do Chat são codificados e Downloads do Vídeo são finalizados com [FFmpeg](https://ffmpeg.org/) © The FFmpeg developers.

Renderizações do Chat devem usar [Noto Color Emoji](https://github.com/googlefonts/noto-emoji) © Google e contributors.

Renderizações do Chat devem usar [Twemoji](https://github.com/twitter/twemoji) © Twitter e contributors.

Binários pré-instalados do FFmpeg são pegos do [gyan.dev](https://www.gyan.dev/ffmpeg/) © Gyan Doshi.

Binários do FFmpeg pegos são runtime são baixados usando [Xabe.FFmpeg.Downloader](https://github.com/tomaszzmuda/Xabe.FFmpeg) © Xabe.

Exportações do Chat em Html utilizam o typeface _Inter_ hosteados pelo [Google Fonts API](https://fonts.google.com/) © Google.

Para uma lista completa de bibliotecas externas, veja [THIRD-PARTY-LICENSES.txt](./TwitchDownloaderCore/Resources/THIRD-PARTY-LICENSES.txt).

# Licença

[MIT](./LICENSE.txt)

TwitchDownloader não é de modo algum associado com a Twitch Interactive, Inc. ou suas filiais.
