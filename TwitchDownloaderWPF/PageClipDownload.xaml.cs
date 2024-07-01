using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using HandyControl.Controls;
using Microsoft.Win32;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Translations;
using TwitchDownloaderWPF.Utils;
using WpfAnimatedGif;
using MessageBox = System.Windows.MessageBox;

namespace TwitchDownloaderWPF {
    /// <summary>
    ///     Interaction logic for PageClipDownload.xaml
    /// </summary>
    public partial class PageClipDownload : Page {
        private CancellationTokenSource _cancellationTokenSource;
        public string clipId = "";
        public TimeSpan clipLength;
        public DateTime currentVideoTime;
        public string game;
        public int viewCount;

        public PageClipDownload() { this.InitializeComponent(); }

        private async void btnGetInfo_Click(object sender, RoutedEventArgs e) { await this.GetClipInfo(); }

        private async Task GetClipInfo() {
            this.clipId = ValidateUrl(this.textUrl.Text.Trim());
            if (string.IsNullOrWhiteSpace(this.clipId)) {
                MessageBox.Show(
                    Application.Current.MainWindow!,
                    Strings.InvalidClipLinkIdMessage.Replace(@"\n", Environment.NewLine),
                    Strings.InvalidClipLinkId,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            try {
                this.btnGetInfo.IsEnabled = false;
                this.comboQuality.Items.Clear();
                var taskClipInfo = TwitchHelper.GetClipInfo(this.clipId);
                var taskLinks = TwitchHelper.GetClipLinks(this.clipId);
                await Task.WhenAll(taskClipInfo, taskLinks);

                var clipData = taskClipInfo.Result;

                var thumbUrl = clipData.data.clip.thumbnailURL;
                if (!ThumbnailService.TryGetThumb(thumbUrl, out var image)) {
                    this.AppendLog(Strings.ErrorLog + Strings.UnableToFindThumbnail);
                    _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
                }

                this.imgThumbnail.Source = image;

                this.clipLength = TimeSpan.FromSeconds(taskClipInfo.Result.data.clip.durationSeconds);
                this.textStreamer.Text = clipData.data.clip.broadcaster?.displayName ?? Strings.UnknownUser;
                var clipCreatedAt = clipData.data.clip.createdAt;
                this.textCreatedAt.Text = Settings.Default.UTCVideoTime
                    ? clipCreatedAt.ToString(CultureInfo.CurrentCulture)
                    : clipCreatedAt.ToLocalTime().ToString(CultureInfo.CurrentCulture);
                this.currentVideoTime = Settings.Default.UTCVideoTime ? clipCreatedAt : clipCreatedAt.ToLocalTime();
                this.textTitle.Text = clipData.data.clip.title;
                this.labelLength.Text = this.clipLength.ToString("c");
                this.viewCount = taskClipInfo.Result.data.clip.viewCount;
                this.game = taskClipInfo.Result.data.clip.game?.displayName ?? Strings.UnknownGame;

                foreach (var quality in taskLinks.Result.data.clip.videoQualities)
                    this.comboQuality.Items.Add(
                        new TwitchClip(quality.quality, Math.Round(quality.frameRate).ToString("F0"), quality.sourceURL)
                    );

                this.comboQuality.SelectedIndex = 0;
                this.SetEnabled(true);
            } catch (Exception ex) {
                MessageBox.Show(
                    Application.Current.MainWindow!,
                    Strings.UnableToGetClipInfo,
                    Strings.UnableToGetInfo,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                this.AppendLog(Strings.ErrorLog + ex);
                if (Settings.Default.VerboseErrors)
                    MessageBox.Show(
                        Application.Current.MainWindow!,
                        ex.ToString(),
                        Strings.VerboseErrorOutput,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
            }

            this.btnGetInfo.IsEnabled = true;
        }

        private void UpdateActionButtons(bool isDownloading) {
            if (isDownloading) {
                this.SplitBtnDownload.Visibility = Visibility.Collapsed;
                this.BtnCancel.Visibility = Visibility.Visible;
                return;
            }

            this.SplitBtnDownload.Visibility = Visibility.Visible;
            this.BtnCancel.Visibility = Visibility.Collapsed;
        }

        private static string ValidateUrl(string text) {
            var clipIdMatch = TwitchRegex.MatchClipId(text);
            return clipIdMatch is { Success: true }
                ? clipIdMatch.Value
                : null;
        }

        private void SetPercent(int percent) {
            this.Dispatcher.BeginInvoke(
                () => this.statusProgressBar.Value = percent
            );
        }

        private void SetStatus(string message) {
            this.Dispatcher.BeginInvoke(
                () => this.statusMessage.Text = message
            );
        }

        private void AppendLog(string message) {
            this.textLog.Dispatcher.BeginInvoke(
                () => this.textLog.AppendText(message + Environment.NewLine)
            );
        }

        private void Page_Initialized(object sender, EventArgs e) {
            this.SetEnabled(false);
            this.CheckMetadata.IsChecked = Settings.Default.EncodeClipMetadata;
        }

        private void SetEnabled(bool enabled) {
            this.comboQuality.IsEnabled = enabled;
            this.SplitBtnDownload.IsEnabled = enabled;
            this.CheckMetadata.IsEnabled = enabled;
        }

        public void SetImage(string imageUri, bool isGif) {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new(imageUri, UriKind.Relative);
            image.EndInit();
            if (isGif)
                ImageBehavior.SetAnimatedSource(this.statusImage, image);
            else {
                ImageBehavior.SetAnimatedSource(this.statusImage, null);
                this.statusImage.Source = image;
            }
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e) {
            Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e) {
            var settings = new WindowSettings {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            settings.ShowDialog();
            this.btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) {
            this.btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void SplitBtnDownload_Click(object sender, RoutedEventArgs e) {
            if (((SplitButton)sender).IsDropDownOpen)
                return;

            var saveFileDialog = new SaveFileDialog {
                Filter = "MP4 Files | *.mp4",
                FileName = FilenameService.GetFilename(
                        Settings.Default.TemplateClip,
                        this.textTitle.Text,
                        this.clipId,
                        this.currentVideoTime,
                        this.textStreamer.Text,
                        TimeSpan.Zero,
                        this.clipLength,
                        this.viewCount,
                        this.game
                    )
                    + ".mp4"
            };
            if (saveFileDialog.ShowDialog() != true)
                return;

            this.SetEnabled(false);

            var downloadOptions = this.GetOptions(saveFileDialog.FileName);
            this._cancellationTokenSource = new();

            var downloadProgress = new WpfTaskProgress(
                (LogLevel)Settings.Default.LogLevels,
                this.SetPercent,
                this.SetStatus,
                this.AppendLog
            );
            var currentDownload = new ClipDownloader(downloadOptions, downloadProgress);

            this.SetImage("Images/ppOverheat.gif", true);
            this.statusMessage.Text = Strings.StatusDownloading;
            this.UpdateActionButtons(true);
            try {
                await currentDownload.DownloadAsync(this._cancellationTokenSource.Token);
                downloadProgress.SetStatus(Strings.StatusDone);
                this.SetImage("Images/ppHop.gif", true);
            } catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException
                && this._cancellationTokenSource.IsCancellationRequested) {
                downloadProgress.SetStatus(Strings.StatusCanceled);
                this.SetImage("Images/ppHop.gif", true);
            } catch (Exception ex) {
                downloadProgress.SetStatus(Strings.StatusError);
                this.SetImage("Images/peepoSad.png", false);
                this.AppendLog(Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors)
                    MessageBox.Show(
                        Application.Current.MainWindow!,
                        ex.ToString(),
                        Strings.VerboseErrorOutput,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
            }

            this.btnGetInfo.IsEnabled = true;
            downloadProgress.ReportProgress(0);
            this._cancellationTokenSource.Dispose();
            this.UpdateActionButtons(false);
        }

        private ClipDownloadOptions GetOptions(string fileName) => new() {
            Filename = fileName,
            Id = this.clipId,
            Quality = this.comboQuality.Text,
            ThrottleKib = Settings.Default.DownloadThrottleEnabled
                ? Settings.Default.MaximumBandwidthKib
                : -1,
            TempFolder = Settings.Default.TempPath,
            EncodeMetadata = this.CheckMetadata.IsChecked!.Value,
            FfmpegPath = "ffmpeg"
        };

        private void BtnCancel_Click(object sender, RoutedEventArgs e) {
            this.statusMessage.Text = Strings.StatusCanceling;
            try {
                this._cancellationTokenSource.Cancel();
            } catch (ObjectDisposedException) { }
        }

        private void MenuItemEnqueue_Click(object sender, RoutedEventArgs e) {
            var queueOptions = new WindowQueueOptions(this) {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            queueOptions.ShowDialog();
        }

        private async void TextUrl_OnKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                await this.GetClipInfo();
                e.Handled = true;
            }
        }

        private void CheckMetadata_OnCheckStateChanged(object sender, RoutedEventArgs e) {
            if (this.IsInitialized) {
                Settings.Default.EncodeClipMetadata = this.CheckMetadata.IsChecked!.Value;
                Settings.Default.Save();
            }
        }
    }
}

public class TwitchClip {

    public TwitchClip(string Quality, string Framerate, string Url) {
        this.quality = Quality;
        this.framerate = Framerate;
        this.url = Url;
    }

    public string quality { get; set; }
    public string framerate { get; set; }
    public string url { get; set; }

    override
        public string ToString()
        //Only show framerate if it's not 30fps
        => $"{this.quality}p{(this.framerate == "30" ? "" : this.framerate)}";
}
