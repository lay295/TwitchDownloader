<p align="center">
  <a href="https://github.com/lay295/TwitchDownloader">
    <img src="TwitchDownloaderWPF/Images/Logo.png" alt="Logo" width="80" height="80">
  </a>

  <h3 align="center">Twitch İndirici</h3>

  <p align="center">
    Twitch VOD/Clip/Chat İndirici ve Chat Oynatıcı
    <br />
    <br />
    <a href="https://github.com/lay295/TwitchDownloader/issues">Hata Bildir</a>
  </p>
</p>

[**İspanyolca'da Oku**](README_es.md)  
[**İngilizce'de Oku**](README.md)

## Chat Oynatma Örneği

https://user-images.githubusercontent.com/1060681/197653099-c3fd12c2-f03a-4580-84e4-63ce3f36be8d.mp4

## Neler Yapabilir?

- Twitch VOD'larını İndir
- Twitch Kliplerini İndir
- VOD'lar ve Klipler için sohbeti, ya [tüm orijinal bilgileri içeren bir JSON olarak](https://pastebin.com/raw/YDgRe6X4), bir tarayıcı HTML dosyası olarak ya da [düz metin dosyası olarak](https://pastebin.com/raw/016azeQX) indirin.
- Daha önce oluşturulmuş bir JSON sohbet dosyasının içeriğini güncelleyin ve başka bir biçimde kaydetme seçeneğiyle kaydedin.
- Daha önce oluşturulmuş bir JSON sohbet dosyasını kullanarak sohbeti Twitter Twemoji veya Google Noto Color emojileri ve BTTV, FFZ, 7TV statik ve animasyonlu emojilerle oynatmak için kullanın.

# GUI

## Windows WPF

![](https://i.imgur.com/bLegxGX.gif)

### [Full WPF belgelerini buradan görüntüleyin](TwitchDownloaderWPF/README.md).

### İşlevsellik

Windows WPF GUI, programın tüm ana işlevlerini ve bazı ek yaşam kalitesi işlevlerini uygular:
- Aynı anda çalıştırılacak birden fazla indirme/oynatma işini sıraya alın.
- VOD/Klip bağlantıları listesinden indirme işlerinin bir listesini oluşturun.
- Uygulamayı terk etmeden herhangi bir yayıcıdan birden fazla VOD/klip arayın ve indirin.

### Çoklu Dil Desteği

Windows WPF GUI, topluluk çevirileri sayesinde birçok dilde kullanılabilir. Daha fazla ayrıntı için [WPF README](TwitchDownloaderWPF/README.md)'nin [Yerelleştirme bölümüne](TwitchDownloaderWPF/README.md#localization) bakın.

### Temalar

Windows WPF GUI, hem açık hem de karanlık temalar ile gelir ve mevcut Windows temasına göre canlı olarak güncelleme seçeneği sunar. Ayrıca kullanıcı tarafından oluşturulan temaları destekler! Daha fazla ayrıntı için [WPF README](TwitchDownloaderWPF/README.md)'nin [Tema bölümüne](TwitchDownloaderWPF/README.md#theming) bakın.

### Video Gösterimi

https://www.youtube.com/watch?v=0W3MhfhnYjk
(eski sürüm, aynı konsept)

## Linux?

***Nasıl cevireceğimi bilemedim terminal versionu var [githubda](https://github.com/mohad12211/twitch-downloader-gui) gidin ona bakın diyor kısaca birde [AUR'da](https://aur.archlinux.org/packages/twitch-downloader-gui) terminalin biraz süslü gui hali var ona bakabilirsniiz diyor.

## MacOS?

Malesef MacOS için henüz bir GUI mevcut değil :(

# CLI

### [Tüm CLI belgelerini buradan inceleyin](TwitchDownloaderCLI/README.md).

CLI, ana program işlevlerini uygulayan ve Windows, Linux ve MacOS<sup>*</sup> üzerinde çalışan çapraz platformlu bir araçtır.

<sup>*Sadece Intel Mac'ler test edilmiştir</sup>

CLI ile, harici komut dosyalarını kullanarak video işleme işlemini otomatikleştirmek mümkündür. Örneğin, Windows'ta bir `.bat` dosyasına aşağıdaki kodu kopyalayarak bir VOD'u ve onun sohbetini indirebilir ve ardından sohbeti renderlayabilirsiniz.
```bat
@echo off
set /p vodid="VOD Kimliğini Girin: "
TwitchDownloaderCLI.exe videodownload --id %vodid% --ffmpeg-path "ffmpeg.exe" -o %vodid%.mp4
TwitchDownloaderCLI.exe chatdownload --id %vodid% -o %vodid%_chat.json -E
TwitchDownloaderCLI.exe chatrender -i %vodid%_chat.json -h 1080 -w 422 --framerate 30 --update-rate 0 --font-size 18 -o %vodid%_chat.mp4
```

## Windows - Başlangıç

1. [Releases-Sürümler](https://github.com/lay295/TwitchDownloader/releases/) sayfasına gidin ve en son Windows sürümünü indirin veya [kaynaktan derleyin.](#building-from-source).
2. `TwitchDownloaderCLI.exe`'yi çıkartın.
3. Dosyayı çıkardığınız yerde terminal açın.
4. FFmpeg'e sahip değilseniz,[Chocolatey package manager](https://community.chocolatey.org/) aracılığı ile indirebilir veya bağımsız bir dosya olarak [ffmpeg.org](https://ffmpeg.org/download.html) adresinden alabilir veya TwitchDownloaderCLI kullanarak alabilirsiniz. Şu komutu kullanarak indirebilirsiniz:
```
TwitchDownloaderCLI.exe ffmpeg --download
```
5. Artık indirme işlemine başlayabilirsiniz, örneğin:
```
TwitchDownloaderCLI.exe videodownload --id <vod-id-here> -o out.mp4
```

## Linux – Başlangıç

1. Bazı dağıtımlar, Linux Alpine gibi, bazı diller için (Arapça, Farsça, Tayca vb.) yazı tiplerini eksik bulabilir. Bu durum sizin için geçerliyse, [Noto](https://fonts.google.com/noto/specimen/Noto+Sans) gibi ek yazı tipleri ailesi yükleyin veya dağıtımınızın yazı tipleri hakkındaki wiki sayfasını kontrol edin, çünkü bu özel senaryo için bir kurulum komutuna sahip olabilir, örneğin [Linux Alpine](https://wiki.alpinelinux.org/wiki/Fonts) yazı tipi sayfası gibi.
2. `fontconfig` ve `libfontconfig1`'in yüklü olduğundan emin olun. Ubuntu'da `apt-get install fontconfig libfontconfig1` kullanabilirsiniz.
3. [Sürümler](https://github.com/lay295/TwitchDownloader/releases/) sayfasına gidin ve Linux için en son ikili sürümü indirin, Arch Linux için [AUR Paketi](https://aur.archlinux.org/packages/twitch-downloader-bin/)ni alın veya [kaynaktan derleyin](#building-from-source).
4. `TwitchDownloaderCLI`'yi çıkarın.
5. Dosyayı çıkardığınız yere gidin ve terminalde çalıştırılabilir izinleri verin:
```
sudo chmod +x TwitchDownloaderCLI
```
6. a) Eğer FFmpeg'e sahip değilseniz, bunu dağıtım paket yöneticiniz aracılığıyla kurmalısınız. Ayrıca, [ffmpeg.org](https://ffmpeg.org/download.html) adresinden bağımsız bir dosya olarak veya TwitchDownloaderCLI kullanarak da edinebilirsiniz.
```
./TwitchDownloaderCLI ffmpeg --download
```
6. b) Bağımsız bir dosya olarak indirildiyse, ona çalıştırılabilir izinler vermelisiniz:
```
sudo chmod +x ffmpeg
```
7. Şimdi indiriciyi kullanmaya başlayabilirsiniz, örneğin:
```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```
## MacOS – Başlangıç
1. [Releases](https://github.com/lay295/TwitchDownloader/releases/) sayfasına gidin ve MacOS için en son sürümü indirin veya kaynaktan derleyin.
2. `TwitchDownloaderCLI` dosyasını çıkarın.
3. Dosyayı çıkardığınız yere terminalde çalıştırılabilir izinler verin.
```
chmod +x TwitchDownloaderCLI
```
4. a) Eğer FFmpeg'e sahip değilseniz, [Homebrew paket yöneticisi](https://brew.sh/) aracılığıyla kurabilirsiniz veya bağımsız bir dosya olarak [ffmpeg.org](https://ffmpeg.org/download.html) adresinden veya TwitchDownloaderCLI kullanarak edinebilirsiniz.
```
./TwitchDownloaderCLI ffmpeg --download
```
4. b) Bağımsız bir dosya olarak indirildiyse, ona çalıştırılabilir izinler vermelisiniz.
```
chmod +x ffmpeg
```
5. Şimdi indiriciyi kullanmaya başlayabilirsiniz, örneğin:
```
./TwitchDownloaderCLI videodownload --id <vod-id-here> -o out.mp4
```

# Kaynaktan derleme

## Gereksinimler

- [.NET 6.0.x SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

## Derleme Talimatları

1. Depoyu klonlayın:
```
git clone https://github.com/lay295/TwitchDownloader.git
```
2. Çözüm klasörüne gidin:
```
cd TwitchDownloader
```
3. Çözümü geri yükleyin:
```
dotnet restore
```
4. a) GUI'yi oluşturun:
```
dotnet publish TwitchDownloaderWPF -p:PublishProfile=Windows -p:DebugType=None -p:DebugSymbols=false
```
4. b) CLI'yi oluşturun:
```
dotnet publish TwitchDownloaderCLI -p:PublishProfile=<Profile> -p:DebugType=None -p:DebugSymbols=false
```
- Uygulanabilir Profiller: `Windows`, `Linux`, `LinuxAlpine`, `LinuxArm`, `LinuxArm64`, `MacOS`
5. a) GUI derleme klasörüne gidin:
```
cd TwitchDownloaderWPF/bin/Release/net6.0-windows/publish/win-x64
```
5. b) CLI derleme klasörüne gidin:
```
cd TwitchDownloaderCLI/bin/Release/net6.0/publish
```

# Lisans

[MIT](./LICENSE.txt)

# Üçüncü Taraf Kredileri

Sohbet Görüntülemeleri, [SkiaSharp ve HarfBuzzSharp](https://github.com/mono/SkiaSharp) tarafından oluşturulmuştur © Microsoft Corporation.

Sohbet Görüntülemeleri işlenmesi ve Video İndirmeleri [FFmpeg](https://ffmpeg.org/) ile sonlandırılır © FFmpeg geliştiricileri.

Sohbet Görüntülemeleri, [Noto Renkli Emoji](https://github.com/googlefonts/noto-emoji) tarafından kullanılabilir © Google ve katkıda bulunanlar.

Sohbet Görüntülemeleri, [Twemoji](https://github.com/twitter/twemoji) tarafından kullanılabilir © Twitter ve katkıda bulunanlar.

Paketlenmiş FFmpeg ikili dosyaları [gyan.dev](https://www.gyan.dev/ffmpeg/) adresinden alınmıştır © Gyan Doshi.

Alınan FFmpeg ikili dosyaları çalışma zamanında [Xabe.FFmpeg.Downloader](https://github.com/tomaszzmuda/Xabe.FFmpeg) kullanılarak indirilir © Xabe.

Sohbet HTML dışa aktarmaları, [Google Fonts API](https://fonts.google.com/) tarafından barındırılan _Inter_ yazı tipini kullanır © Google.

Kullanılan tüm harici kütüphanelerin tam listesi için [THIRD-PARTY-LICENSES.txt](./TwitchDownloaderCore/Resources/THIRD-PARTY-LICENSES.txt) dosyasına bakınız.
