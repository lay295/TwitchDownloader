using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Twitch;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using WpfAnimatedGif;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageClipDownload.xaml
    /// </summary>
    public partial class PageClipDownload : Page
    {
        public string clipId = "";
        public VideoPlatform platform;
        public DateTime currentVideoTime;
        public TimeSpan clipLength;
        public int viewCount;
        public string game;
        private CancellationTokenSource _cancellationTokenSource;

        public PageClipDownload()
        {
            InitializeComponent();
        }

        private async void btnGetInfo_Click(object sender, RoutedEventArgs e)
        {
            await GetClipInfo();
        }

        private async Task GetClipInfo()
        {
            bool parseSuccess = IdParse.TryParseClip(textUrl.Text.Trim(), out VideoPlatform videoPlatform, out string videoId);

            if (!parseSuccess || string.IsNullOrWhiteSpace(videoId))
            {
                MessageBox.Show(Translations.Strings.InvalidClipLinkIdMessage.Replace(@"\n", Environment.NewLine), Translations.Strings.InvalidClipLinkId, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            clipId = videoId;
            platform = videoPlatform;

            try
            {
                btnGetInfo.IsEnabled = false;
                comboQuality.Items.Clear();
                IVideoInfo clipInfo = await PlatformHelper.GetClipInfo(videoPlatform, clipId);

                var thumbUrl = clipInfo.ThumbnailUrl;
                if (!ThumbnailService.TryGetThumb(thumbUrl, out var image))
                {
                    AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail);
                    _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
                }
                imgThumbnail.Source = image;

                clipLength = TimeSpan.FromSeconds(clipInfo.Duration);
                textStreamer.Text = clipInfo.StreamerName;
                var clipCreatedAt = clipInfo.CreatedAt;
                textCreatedAt.Text = Settings.Default.UTCVideoTime ? clipCreatedAt.ToString(CultureInfo.CurrentCulture) : clipCreatedAt.ToLocalTime().ToString(CultureInfo.CurrentCulture);
                currentVideoTime = Settings.Default.UTCVideoTime ? clipCreatedAt : clipCreatedAt.ToLocalTime();
                textTitle.Text = clipInfo.Title;
                labelLength.Text = clipLength.ToString("c");
                viewCount = clipInfo.ViewCount;
                game = clipInfo.Game ?? "Unknown";

                if (videoPlatform == VideoPlatform.Twitch)
                {
                    var twitchClipInfo = await TwitchHelper.GetClipLinks(clipId);
                    foreach (var quality in twitchClipInfo[0].data.clip.videoQualities)
                    {
                        comboQuality.Items.Add(new TwitchClip(quality.quality, Math.Round(quality.frameRate).ToString("F0"), quality.sourceURL));
                    }
                }
                else
                {
                    comboQuality.Items.Add(new TwitchClip("Source", "", ""));
                }

                comboQuality.SelectedIndex = 0;
                SetEnabled(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Translations.Strings.UnableToGetClipInfo, Translations.Strings.UnableToGetInfo, MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog(Translations.Strings.ErrorLog + ex);
                if (Settings.Default.VerboseErrors)
                {
                    MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            btnGetInfo.IsEnabled = true;
        }

        private void UpdateActionButtons(bool isDownloading)
        {
            if (isDownloading)
            {
                SplitBtnDownload.Visibility = Visibility.Collapsed;
                BtnCancel.Visibility = Visibility.Visible;
                return;
            }
            SplitBtnDownload.Visibility = Visibility.Visible;
            BtnCancel.Visibility = Visibility.Collapsed;
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke(() =>
                textLog.AppendText(message + Environment.NewLine)
            );
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            SetEnabled(false);
            CheckMetadata.IsChecked = Settings.Default.EncodeClipMetadata;
        }

        private void SetEnabled(bool enabled)
        {
            comboQuality.IsEnabled = enabled;
            SplitBtnDownload.IsEnabled = enabled;
            CheckMetadata.IsEnabled = enabled;
        }

        private void OnProgressChanged(ProgressReport progress)
        {
            switch (progress.ReportType)
            {
                case ReportType.Percent:
                    statusProgressBar.Value = (int)progress.Data;
                    break;
                case ReportType.NewLineStatus or ReportType.SameLineStatus:
                    statusMessage.Text = (string)progress.Data;
                    break;
                case ReportType.Log:
                    AppendLog((string)progress.Data);
                    break;
            }
        }

        public void SetImage(string imageUri, bool isGif)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(imageUri, UriKind.Relative);
            image.EndInit();
            if (isGif)
            {
                ImageBehavior.SetAnimatedSource(statusImage, image);
            }
            else
            {
                ImageBehavior.SetAnimatedSource(statusImage, null);
                statusImage.Source = image;
            }
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new WindowSettings
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            settings.ShowDialog();
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void SplitBtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (((HandyControl.Controls.SplitButton)sender).IsDropDownOpen)
            {
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "MP4 Files | *.mp4",
                FileName = FilenameService.GetFilename(Settings.Default.TemplateClip, textTitle.Text, clipId, currentVideoTime, textStreamer.Text, TimeSpan.Zero, clipLength, viewCount.ToString(), game)
            };
            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            SetEnabled(false);

            ClipDownloadOptions downloadOptions = GetOptions(saveFileDialog.FileName);
            _cancellationTokenSource = new CancellationTokenSource();

            SetImage("Images/ppOverheat.gif", true);
            statusMessage.Text = Translations.Strings.StatusDownloading;
            UpdateActionButtons(true);
            try
            {
                var downloadProgress = new Progress<ProgressReport>(OnProgressChanged);
                ClipDownloaderFactory clipDownloaderFactory = new ClipDownloaderFactory(downloadProgress);
                await clipDownloaderFactory.Create(downloadOptions)
                    .DownloadAsync(_cancellationTokenSource.Token);

                statusMessage.Text = Translations.Strings.StatusDone;
                SetImage("Images/ppHop.gif", true);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && _cancellationTokenSource.IsCancellationRequested)
            {
                statusMessage.Text = Translations.Strings.StatusCanceled;
                SetImage("Images/ppHop.gif", true);
            }
            catch (Exception ex)
            {
                statusMessage.Text = Translations.Strings.StatusError;
                SetImage("Images/peepoSad.png", false);
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors)
                {
                    MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            btnGetInfo.IsEnabled = true;
            statusProgressBar.Value = 0;
            _cancellationTokenSource.Dispose();
            UpdateActionButtons(false);
        }

        private ClipDownloadOptions GetOptions(string fileName)
        {
            return new ClipDownloadOptions
            {
                Filename = fileName,
                Id = clipId,
                Quality = comboQuality.Text,
                ThrottleKib = Settings.Default.DownloadThrottleEnabled
                    ? Settings.Default.MaximumBandwidthKib
                    : -1,
                TempFolder = Settings.Default.TempPath,
                EncodeMetadata = CheckMetadata.IsChecked!.Value,
                FfmpegPath = "ffmpeg",
                VideoPlatform = platform,
            };
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            statusMessage.Text = Translations.Strings.StatusCanceling;
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException) { }
        }

        private void MenuItemEnqueue_Click(object sender, RoutedEventArgs e)
        {
            var queueOptions = new WindowQueueOptions(this)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            queueOptions.ShowDialog();
        }

        private async void TextUrl_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await GetClipInfo();
                e.Handled = true;
            }
        }

        private void CheckMetadata_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            if (IsInitialized)
            {
                Settings.Default.EncodeClipMetadata = CheckMetadata.IsChecked!.Value;
                Settings.Default.Save();
            }
        }
    }
}

public class TwitchClip
{
    public string quality { get; set; }
    public string framerate { get; set; }
    public string url { get; set; }

    public TwitchClip(string Quality, string Framerate, string Url)
    {
        quality = Quality;
        framerate = Framerate;
        url = Url;
    }

    override
        public string ToString()
    {
        //Only show framerate if it's not 30fps
        if (quality == "Source") return quality;
        return $"{quality}p{(framerate == "30" ? "" : framerate)}";
    }
}