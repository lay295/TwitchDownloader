using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using HandyControl.Controls;
using HandyControl.Data;
using Microsoft.Win32;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Translations;
using TwitchDownloaderWPF.Utils;
using WpfAnimatedGif;
using MessageBox = System.Windows.MessageBox;

namespace TwitchDownloaderWPF;

/// <summary>
///     Interaction logic for PageVodDownload.xaml
/// </summary>
public partial class PageVodDownload : Page {
    public readonly Dictionary<string, (string url, int bandwidth)> videoQualities = new();
    private CancellationTokenSource _cancellationTokenSource;
    public long currentVideoId;
    public DateTime currentVideoTime;
    public string game;
    public int viewCount;
    public TimeSpan vodLength;

    public PageVodDownload() { this.InitializeComponent(); }

    private void SetEnabled(bool isEnabled) {
        this.comboQuality.IsEnabled = isEnabled;
        this.checkStart.IsEnabled = isEnabled;
        this.checkEnd.IsEnabled = isEnabled;
        this.SplitBtnDownload.IsEnabled = isEnabled;
        this.MenuItemEnqueue.IsEnabled = isEnabled;
        this.RadioTrimSafe.IsEnabled = isEnabled;
        this.RadioTrimExact.IsEnabled = isEnabled;
        this.SetEnabledTrimStart(isEnabled & this.checkStart.IsChecked.GetValueOrDefault());
        this.SetEnabledTrimEnd(isEnabled & this.checkEnd.IsChecked.GetValueOrDefault());
    }

    private void SetEnabledTrimStart(bool isEnabled) {
        this.numStartHour.IsEnabled = isEnabled;
        this.numStartMinute.IsEnabled = isEnabled;
        this.numStartSecond.IsEnabled = isEnabled;
    }

    private void SetEnabledTrimEnd(bool isEnabled) {
        this.numEndHour.IsEnabled = isEnabled;
        this.numEndMinute.IsEnabled = isEnabled;
        this.numEndSecond.IsEnabled = isEnabled;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private async void btnGetInfo_Click(object sender, RoutedEventArgs e) { await this.GetVideoInfo(); }

    private async Task GetVideoInfo() {
        var videoId = ValidateUrl(this.textUrl.Text.Trim());
        if (videoId <= 0) {
            MessageBox.Show(
                Application.Current.MainWindow!,
                Strings.InvalidVideoLinkIdMessage.Replace(@"\n", Environment.NewLine),
                Strings.InvalidVideoLinkId,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            return;
        }

        this.currentVideoId = videoId;
        try {
            var taskVideoInfo = TwitchHelper.GetVideoInfo(videoId);
            var taskAccessToken = TwitchHelper.GetVideoToken(videoId, this.TextOauth.Text);
            await Task.WhenAll(taskVideoInfo, taskAccessToken);

            if (taskAccessToken.Result.data.videoPlaybackAccessToken is null)
                throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");

            var thumbUrl = taskVideoInfo.Result.data.video.thumbnailURLs.FirstOrDefault();
            if (!ThumbnailService.TryGetThumb(thumbUrl, out var image)) {
                this.AppendLog(Strings.ErrorLog + Strings.UnableToFindThumbnail);
                _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
            }

            this.imgThumbnail.Source = image;

            this.comboQuality.Items.Clear();
            this.videoQualities.Clear();

            var playlistString = await TwitchHelper.GetVideoPlaylist(
                videoId,
                taskAccessToken.Result.data.videoPlaybackAccessToken.value,
                taskAccessToken.Result.data.videoPlaybackAccessToken.signature
            );
            if (playlistString.Contains("vod_manifest_restricted")
                || playlistString.Contains("unauthorized_entitlements"))
                throw new NullReferenceException(Strings.InsufficientAccessMayNeedOauth);

            var videoPlaylist = M3U8.Parse(playlistString);
            videoPlaylist.SortStreamsByQuality();

            //Add video qualities to combo quality
            foreach (var stream in videoPlaylist.Streams) {
                var userFriendlyName = stream.GetResolutionFramerateString();
                if (!this.videoQualities.ContainsKey(userFriendlyName)) {
                    this.videoQualities.Add(userFriendlyName, (stream.Path, stream.StreamInfo.Bandwidth));
                    this.comboQuality.Items.Add(userFriendlyName);
                }
            }

            this.comboQuality.SelectedIndex = 0;

            this.vodLength = TimeSpan.FromSeconds(taskVideoInfo.Result.data.video.lengthSeconds);
            this.textStreamer.Text = taskVideoInfo.Result.data.video.owner.displayName;
            this.textTitle.Text = taskVideoInfo.Result.data.video.title;
            var videoCreatedAt = taskVideoInfo.Result.data.video.createdAt;
            this.textCreatedAt.Text = Settings.Default.UTCVideoTime
                ? videoCreatedAt.ToString(CultureInfo.CurrentCulture)
                : videoCreatedAt.ToLocalTime().ToString(CultureInfo.CurrentCulture);
            this.currentVideoTime = Settings.Default.UTCVideoTime ? videoCreatedAt : videoCreatedAt.ToLocalTime();
            var urlTimeCodeMatch = TwitchRegex.UrlTimeCode.Match(this.textUrl.Text);
            if (urlTimeCodeMatch.Success) {
                var time = UrlTimeCode.Parse(urlTimeCodeMatch.ValueSpan);
                this.checkStart.IsChecked = true;
                this.numStartHour.Value = time.Hours;
                this.numStartMinute.Value = time.Minutes;
                this.numStartSecond.Value = time.Seconds;
            } else {
                this.numStartHour.Value = 0;
                this.numStartMinute.Value = 0;
                this.numStartSecond.Value = 0;
            }

            this.numStartHour.Maximum = (int)this.vodLength.TotalHours;

            this.numEndHour.Value = (int)this.vodLength.TotalHours;
            this.numEndHour.Maximum = (int)this.vodLength.TotalHours;
            this.numEndMinute.Value = this.vodLength.Minutes;
            this.numEndSecond.Value = this.vodLength.Seconds;
            this.labelLength.Text = this.vodLength.ToString("c");
            this.viewCount = taskVideoInfo.Result.data.video.viewCount;
            this.game = taskVideoInfo.Result.data.video.game?.displayName ?? Strings.UnknownGame;

            this.UpdateVideoSizeEstimates();

            this.SetEnabled(true);
        } catch (Exception ex) {
            this.btnGetInfo.IsEnabled = true;
            this.AppendLog(Strings.ErrorLog + ex.Message);
            MessageBox.Show(
                Application.Current.MainWindow!,
                Strings.UnableToGetVideoInfo,
                Strings.UnableToGetInfo,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            if (Settings.Default.VerboseErrors)
                MessageBox.Show(
                    Application.Current.MainWindow!,
                    ex.ToString(),
                    Strings.VerboseErrorOutput,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
        }
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

    public VideoDownloadOptions GetOptions(string filename, string folder) {
        var options = new VideoDownloadOptions {
            DownloadThreads = (int)this.numDownloadThreads.Value,
            ThrottleKib = Settings.Default.DownloadThrottleEnabled
                ? Settings.Default.MaximumBandwidthKib
                : -1,
            Filename = filename
                ?? Path.Combine(
                    folder,
                    FilenameService.GetFilename(
                        Settings.Default.TemplateVod,
                        this.textTitle.Text,
                        this.currentVideoId.ToString(),
                        this.currentVideoTime,
                        this.textStreamer.Text,
                        this.checkStart.IsChecked == true
                            ? new(
                                (int)this.numStartHour.Value,
                                (int)this.numStartMinute.Value,
                                (int)this.numStartSecond.Value
                            )
                            : TimeSpan.Zero,
                        this.checkEnd.IsChecked == true
                            ? new(
                                (int)this.numEndHour.Value,
                                (int)this.numEndMinute.Value,
                                (int)this.numEndSecond.Value
                            )
                            : this.vodLength,
                        this.viewCount,
                        this.game
                    )
                    + (this.comboQuality.Text.Contains("Audio", StringComparison.OrdinalIgnoreCase) ? ".m4a" : ".mp4")
                ),
            Oauth = this.TextOauth.Text,
            Quality = GetQualityWithoutSize(this.comboQuality.Text),
            Id = this.currentVideoId,
            TrimBeginning = this.checkStart.IsChecked.GetValueOrDefault(),
            TrimBeginningTime = new(
                (int)this.numStartHour.Value,
                (int)this.numStartMinute.Value,
                (int)this.numStartSecond.Value
            ),
            TrimEnding = this.checkEnd.IsChecked.GetValueOrDefault(),
            TrimEndingTime
                = new((int)this.numEndHour.Value, (int)this.numEndMinute.Value, (int)this.numEndSecond.Value),
            FfmpegPath = "ffmpeg",
            TempFolder = Settings.Default.TempPath
        };

        if (this.RadioTrimSafe.IsChecked == true)
            options.TrimMode = VideoTrimMode.Safe;
        else if (this.RadioTrimExact.IsChecked == true)
            options.TrimMode = VideoTrimMode.Exact;

        return options;
    }

    private void UpdateVideoSizeEstimates() {
        var selectedIndex = this.comboQuality.SelectedIndex;

        var trimStart = this.checkStart.IsChecked == true
            ? new((int)this.numStartHour.Value, (int)this.numStartMinute.Value, (int)this.numStartSecond.Value)
            : TimeSpan.Zero;
        var trimEnd = this.checkEnd.IsChecked == true
            ? new((int)this.numEndHour.Value, (int)this.numEndMinute.Value, (int)this.numEndSecond.Value)
            : this.vodLength;

        for (var i = 0; i < this.comboQuality.Items.Count; i++) {
            var qualityWithSize = (string)this.comboQuality.Items[i];
            var quality = GetQualityWithoutSize(qualityWithSize);
            var bandwidth = this.videoQualities[quality].bandwidth;

            var sizeInBytes = VideoSizeEstimator.EstimateVideoSize(bandwidth, trimStart, trimEnd);
            if (sizeInBytes == 0)
                this.comboQuality.Items[i] = quality;
            else {
                var newVideoSize = VideoSizeEstimator.StringifyByteCount(sizeInBytes);
                this.comboQuality.Items[i] = $"{quality} - {newVideoSize}";
            }
        }

        this.comboQuality.SelectedIndex = selectedIndex;
    }

    private static string GetQualityWithoutSize(string qualityWithSize) {
        var qualityIndex = qualityWithSize.LastIndexOf(" - ", StringComparison.Ordinal);
        return qualityIndex == -1
            ? qualityWithSize
            : qualityWithSize[..qualityIndex];
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

    private static long ValidateUrl(string text) {
        var vodIdMatch = TwitchRegex.MatchVideoId(text);
        if (vodIdMatch is { Success: true } && long.TryParse(vodIdMatch.ValueSpan, out var vodId))
            return vodId;

        return -1;
    }

    public bool ValidateInputs() {
        if (this.checkStart.IsChecked.GetValueOrDefault()) {
            var beginTime = new TimeSpan(
                (int)this.numStartHour.Value,
                (int)this.numStartMinute.Value,
                (int)this.numStartSecond.Value
            );
            if (beginTime.TotalSeconds >= this.vodLength.TotalSeconds)
                return false;

            if (this.checkEnd.IsChecked.GetValueOrDefault()) {
                var endTime = new TimeSpan(
                    (int)this.numEndHour.Value,
                    (int)this.numEndMinute.Value,
                    (int)this.numEndSecond.Value
                );
                if (endTime.TotalSeconds < beginTime.TotalSeconds)
                    return false;
            }
        }

        return true;
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
        this.SetEnabledTrimStart(false);
        this.SetEnabledTrimEnd(false);
        WebRequest.DefaultWebProxy = null;
        this.numDownloadThreads.Value = Settings.Default.VodDownloadThreads;
        this.TextOauth.Text = Settings.Default.OAuth;
        _ = (VideoTrimMode)Settings.Default.VodTrimMode switch {
            VideoTrimMode.Exact => this.RadioTrimExact.IsChecked = true,
            _ => this.RadioTrimSafe.IsChecked = true
        };
    }

    private void numDownloadThreads_ValueChanged(object sender, FunctionEventArgs<double> e) {
        if (this.IsInitialized && this.numDownloadThreads.IsEnabled) {
            Settings.Default.VodDownloadThreads = (int)this.numDownloadThreads.Value;
            Settings.Default.Save();
        }
    }

    private void TextOauth_TextChanged(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.OAuth = this.TextOauth.Text;
            Settings.Default.Save();
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

    private void checkStart_OnCheckStateChanged(object sender, RoutedEventArgs e) {
        this.SetEnabledTrimStart(this.checkStart.IsChecked.GetValueOrDefault());

        this.UpdateVideoSizeEstimates();
    }

    private void checkEnd_OnCheckStateChanged(object sender, RoutedEventArgs e) {
        this.SetEnabledTrimEnd(this.checkEnd.IsChecked.GetValueOrDefault());

        this.UpdateVideoSizeEstimates();
    }

    private async void SplitBtnDownloader_Click(object sender, RoutedEventArgs e) {
        if (((SplitButton)sender).IsDropDownOpen)
            return;

        if (!this.ValidateInputs()) {
            this.AppendLog(Strings.ErrorLog + Strings.InvalidTrimInputs);
            return;
        }

        var saveFileDialog = new SaveFileDialog {
            Filter = this.comboQuality.Text.Contains("Audio", StringComparison.OrdinalIgnoreCase)
                ? "M4A Files | *.m4a"
                : "MP4 Files | *.mp4",
            FileName = FilenameService.GetFilename(
                    Settings.Default.TemplateVod,
                    this.textTitle.Text,
                    this.currentVideoId.ToString(),
                    this.currentVideoTime,
                    this.textStreamer.Text,
                    this.checkStart.IsChecked == true
                        ? new(
                            (int)this.numStartHour.Value,
                            (int)this.numStartMinute.Value,
                            (int)this.numStartSecond.Value
                        )
                        : TimeSpan.Zero,
                    this.checkEnd.IsChecked == true
                        ? new((int)this.numEndHour.Value, (int)this.numEndMinute.Value, (int)this.numEndSecond.Value)
                        : this.vodLength,
                    this.viewCount,
                    this.game
                )
                + (this.comboQuality.Text.Contains("Audio", StringComparison.OrdinalIgnoreCase) ? ".m4a" : ".mp4")
        };
        if (saveFileDialog.ShowDialog() == false)
            return;

        this.SetEnabled(false);
        this.btnGetInfo.IsEnabled = false;

        var options = this.GetOptions(saveFileDialog.FileName, null);
        options.CacheCleanerCallback = this.HandleCacheCleanerCallback;

        var downloadProgress = new WpfTaskProgress(
            (LogLevel)Settings.Default.LogLevels,
            this.SetPercent,
            this.SetStatus,
            this.AppendLog
        );
        var currentDownload = new VideoDownloader(options, downloadProgress);
        this._cancellationTokenSource = new();

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

        currentDownload = null;
        GC.Collect();
    }

    private DirectoryInfo[] HandleCacheCleanerCallback(DirectoryInfo[] directories) {
        return this.Dispatcher.Invoke(
            () => {
                var window = new WindowOldVideoCacheManager(directories) {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.ShowDialog();

                return window.GetItemsToDelete();
            }
        );
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) {
        this.statusMessage.Text = Strings.StatusCanceling;
        try {
            this._cancellationTokenSource.Cancel();
        } catch (ObjectDisposedException) { }
    }

    private void MenuItemEnqueue_Click(object sender, RoutedEventArgs e) {
        if (!this.SplitBtnDownload.IsDropDownOpen)
            return;

        if (this.ValidateInputs()) {
            var queueOptions = new WindowQueueOptions(this) {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            queueOptions.ShowDialog();
        } else
            this.AppendLog(Strings.ErrorLog + Strings.InvalidTrimInputs);
    }

    private void numEndHour_ValueChanged(object sender, FunctionEventArgs<double> e) {
        this.UpdateVideoSizeEstimates();
    }

    private void numEndMinute_ValueChanged(object sender, FunctionEventArgs<double> e) {
        this.UpdateVideoSizeEstimates();
    }

    private void numEndSecond_ValueChanged(object sender, FunctionEventArgs<double> e) {
        this.UpdateVideoSizeEstimates();
    }

    private void numStartHour_ValueChanged(object sender, FunctionEventArgs<double> e) {
        this.UpdateVideoSizeEstimates();
    }

    private void numStartMinute_ValueChanged(object sender, FunctionEventArgs<double> e) {
        this.UpdateVideoSizeEstimates();
    }

    private void numStartSecond_ValueChanged(object sender, FunctionEventArgs<double> e) {
        this.UpdateVideoSizeEstimates();
    }

    private async void TextUrl_OnKeyDown(object sender, KeyEventArgs e) {
        if (e.Key == Key.Enter) {
            await this.GetVideoInfo();
            e.Handled = true;
        }
    }

    private void RadioTrimSafe_OnCheckedStateChanged(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.VodTrimMode = (int)VideoTrimMode.Safe;
            Settings.Default.Save();
        }
    }

    private void RadioTrimExact_OnCheckedStateChanged(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.VodTrimMode = (int)VideoTrimMode.Exact;
            Settings.Default.Save();
        }
    }
}
