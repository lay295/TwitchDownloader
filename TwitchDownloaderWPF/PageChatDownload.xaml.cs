using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using HandyControl.Controls;
using HandyControl.Data;
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

namespace TwitchDownloaderWPF;

public enum DownloadType {
    Clip,
    Video
}

/// <summary>
///     Interaction logic for PageChatDownload.xaml
/// </summary>
public partial class PageChatDownload : Page {
    private CancellationTokenSource _cancellationTokenSource;
    public DateTime currentVideoTime;
    public string downloadId;

    public DownloadType downloadType;
    public string game;
    public int streamerId;
    public int viewCount;
    public TimeSpan vodLength;

    public PageChatDownload() { this.InitializeComponent(); }

    private void Page_Initialized(object sender, EventArgs e) {
        this.SetEnabled(false, false);
        this.SetEnabledTrimStart(false);
        this.SetEnabledTrimEnd(false);
        this.checkEmbed.IsChecked = Settings.Default.ChatEmbedEmotes;
        this.checkBttvEmbed.IsChecked = Settings.Default.BTTVEmotes;
        this.checkFfzEmbed.IsChecked = Settings.Default.FFZEmotes;
        this.checkStvEmbed.IsChecked = Settings.Default.STVEmotes;
        this.NumChatDownloadThreads.Value = Settings.Default.ChatDownloadThreads;
        _ = (ChatFormat)Settings.Default.ChatDownloadType switch {
            ChatFormat.Text => this.radioText.IsChecked = true,
            ChatFormat.Html => this.radioHTML.IsChecked = true,
            _ => this.radioJson.IsChecked = true
        };
    }

    private void SetEnabled(bool isEnabled, bool isClip) {
        this.CheckTrimStart.IsEnabled = isEnabled & !isClip;
        this.CheckTrimEnd.IsEnabled = isEnabled & !isClip;
        this.radioTimestampRelative.IsEnabled = isEnabled;
        this.radioTimestampUTC.IsEnabled = isEnabled;
        this.radioTimestampNone.IsEnabled = isEnabled;
        this.radioCompressionNone.IsEnabled = isEnabled;
        this.radioCompressionGzip.IsEnabled = isEnabled;
        this.checkEmbed.IsEnabled = isEnabled;
        this.checkBttvEmbed.IsEnabled = isEnabled;
        this.checkFfzEmbed.IsEnabled = isEnabled;
        this.checkStvEmbed.IsEnabled = isEnabled;
        this.SplitBtnDownload.IsEnabled = isEnabled;
        this.MenuItemEnqueue.IsEnabled = isEnabled;
        this.radioJson.IsEnabled = isEnabled;
        this.radioText.IsEnabled = isEnabled;
        this.radioHTML.IsEnabled = isEnabled;
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

    private async void btnGetInfo_Click(object sender, RoutedEventArgs e) { await this.GetVideoInfo(); }

    private async Task GetVideoInfo() {
        var id = ValidateUrl(this.textUrl.Text.Trim());
        if (string.IsNullOrWhiteSpace(id)) {
            MessageBox.Show(
                Application.Current.MainWindow!,
                Strings.UnableToParseLinkMessage,
                Strings.UnableToParseLink,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            return;
        }

        this.btnGetInfo.IsEnabled = false;
        this.downloadId = id;
        this.downloadType = id.All(char.IsDigit) ? DownloadType.Video : DownloadType.Clip;

        try {
            if (this.downloadType == DownloadType.Video) {
                var videoInfo = await TwitchHelper.GetVideoInfo(long.Parse(this.downloadId));

                var thumbUrl = videoInfo.data.video.thumbnailURLs.FirstOrDefault();
                if (!ThumbnailService.TryGetThumb(thumbUrl, out var image)) {
                    this.AppendLog(Strings.ErrorLog + Strings.UnableToFindThumbnail);
                    _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
                }

                this.imgThumbnail.Source = image;

                this.vodLength = TimeSpan.FromSeconds(videoInfo.data.video.lengthSeconds);
                this.textTitle.Text = videoInfo.data.video.title;
                this.textStreamer.Text = videoInfo.data.video.owner.displayName;
                var videoTime = videoInfo.data.video.createdAt;
                this.textCreatedAt.Text = Settings.Default.UTCVideoTime
                    ? videoTime.ToString(CultureInfo.CurrentCulture)
                    : videoTime.ToLocalTime().ToString(CultureInfo.CurrentCulture);
                this.currentVideoTime = Settings.Default.UTCVideoTime ? videoTime : videoTime.ToLocalTime();
                this.streamerId = int.Parse(videoInfo.data.video.owner.id);
                this.viewCount = videoInfo.data.video.viewCount;
                this.game = videoInfo.data.video.game?.displayName ?? Strings.UnknownGame;
                var urlTimeCodeMatch = TwitchRegex.UrlTimeCode.Match(this.textUrl.Text);
                if (urlTimeCodeMatch.Success) {
                    var time = UrlTimeCode.Parse(urlTimeCodeMatch.ValueSpan);
                    this.CheckTrimStart.IsChecked = true;
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
                this.SetEnabled(true, false);
            } else if (this.downloadType == DownloadType.Clip) {
                var clipId = this.downloadId;
                var clipInfo = await TwitchHelper.GetClipInfo(clipId);

                var thumbUrl = clipInfo.data.clip.thumbnailURL;
                if (!ThumbnailService.TryGetThumb(thumbUrl, out var image)) {
                    this.AppendLog(Strings.ErrorLog + Strings.UnableToFindThumbnail);
                    _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
                }

                this.imgThumbnail.Source = image;

                var clipLength = TimeSpan.FromSeconds(clipInfo.data.clip.durationSeconds);
                this.textStreamer.Text = clipInfo.data.clip.broadcaster?.displayName ?? Strings.UnknownUser;
                var clipCreatedAt = clipInfo.data.clip.createdAt;
                this.textCreatedAt.Text = Settings.Default.UTCVideoTime
                    ? clipCreatedAt.ToString(CultureInfo.CurrentCulture)
                    : clipCreatedAt.ToLocalTime().ToString(CultureInfo.CurrentCulture);
                this.currentVideoTime = Settings.Default.UTCVideoTime ? clipCreatedAt : clipCreatedAt.ToLocalTime();
                this.textTitle.Text = clipInfo.data.clip.title;
                this.streamerId = int.Parse(clipInfo.data.clip.broadcaster?.id ?? "-1");
                this.labelLength.Text = clipLength.ToString("c");
                this.SetEnabled(true, true);
                this.SetEnabledTrimStart(false);
                this.SetEnabledTrimEnd(false);
            }

            this.btnGetInfo.IsEnabled = true;
        } catch (Exception ex) {
            MessageBox.Show(
                Application.Current.MainWindow!,
                Strings.UnableToGetInfoMessage,
                Strings.UnableToGetInfo,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            this.AppendLog(Strings.ErrorLog + ex.Message);
            this.btnGetInfo.IsEnabled = true;
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

    public static string ValidateUrl(string text) {
        var vodClipIdMatch = TwitchRegex.MatchVideoOrClipId(text);
        return vodClipIdMatch is { Success: true }
            ? vodClipIdMatch.Value
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

    public ChatDownloadOptions GetOptions(string filename) {
        var options = new ChatDownloadOptions();

        if (this.radioJson.IsChecked == true)
            options.DownloadFormat = ChatFormat.Json;
        else if (this.radioHTML.IsChecked == true)
            options.DownloadFormat = ChatFormat.Html;
        else if (this.radioText.IsChecked == true)
            options.DownloadFormat = ChatFormat.Text;

        if (this.radioCompressionNone.IsChecked == true)
            options.Compression = ChatCompression.None;
        else if (this.radioCompressionGzip.IsChecked == true)
            options.Compression = ChatCompression.Gzip;

        options.EmbedData = this.checkEmbed.IsChecked.GetValueOrDefault();
        options.BttvEmotes = this.checkBttvEmbed.IsChecked.GetValueOrDefault();
        options.FfzEmotes = this.checkFfzEmbed.IsChecked.GetValueOrDefault();
        options.StvEmotes = this.checkStvEmbed.IsChecked.GetValueOrDefault();
        options.Filename = filename;
        options.DownloadThreads = (int)this.NumChatDownloadThreads.Value;
        return options;
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

    private void NumChatDownloadThreads_ValueChanged(object sender, FunctionEventArgs<double> e) {
        if (this.IsInitialized) {
            this.NumChatDownloadThreads.Value = Math.Clamp((int)this.NumChatDownloadThreads.Value, 1, 50);
            Settings.Default.ChatDownloadThreads = (int)this.NumChatDownloadThreads.Value;
            Settings.Default.Save();
        }
    }

    private void checkEmbed_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.ChatEmbedEmotes = true;
            Settings.Default.Save();
            this.checkBttvEmbed.IsEnabled = true;
            this.checkFfzEmbed.IsEnabled = true;
            this.checkStvEmbed.IsEnabled = true;
        }
    }

    private void checkEmbed_Unchecked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.ChatEmbedEmotes = false;
            Settings.Default.Save();
            this.checkBttvEmbed.IsEnabled = false;
            this.checkFfzEmbed.IsEnabled = false;
            this.checkStvEmbed.IsEnabled = false;
        }
    }

    private void checkBttvEmbed_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.BTTVEmotes = true;
            Settings.Default.Save();
        }
    }

    private void checkBttvEmbed_Unchecked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.BTTVEmotes = false;
            Settings.Default.Save();
        }
    }

    private void checkFfzEmbed_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.FFZEmotes = true;
            Settings.Default.Save();
        }
    }

    private void checkFfzEmbed_Unchecked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.FFZEmotes = false;
            Settings.Default.Save();
        }
    }

    private void checkStvEmbed_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.STVEmotes = true;
            Settings.Default.Save();
        }
    }

    private void checkStvEmbed_Unchecked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.STVEmotes = false;
            Settings.Default.Save();
        }
    }

    private void radioJson_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            this.timeText.Visibility = Visibility.Collapsed;
            this.timeOptions.Visibility = Visibility.Collapsed;
            this.stackEmbedText.Visibility = Visibility.Visible;
            this.stackEmbedChecks.Visibility = Visibility.Visible;
            this.compressionText.Visibility = Visibility.Visible;
            this.compressionOptions.Visibility = Visibility.Visible;
            this.textTrim.Margin = new(0, 10, 0, 33);

            Settings.Default.ChatDownloadType = (int)ChatFormat.Json;
            Settings.Default.Save();
        }
    }

    private void radioHTML_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            this.timeText.Visibility = Visibility.Collapsed;
            this.timeOptions.Visibility = Visibility.Collapsed;
            this.stackEmbedText.Visibility = Visibility.Visible;
            this.stackEmbedChecks.Visibility = Visibility.Visible;
            this.compressionText.Visibility = Visibility.Collapsed;
            this.compressionOptions.Visibility = Visibility.Collapsed;
            this.textTrim.Margin = new(0, 17, 0, 33);

            Settings.Default.ChatDownloadType = (int)ChatFormat.Html;
            Settings.Default.Save();
        }
    }

    private void radioText_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            this.timeText.Visibility = Visibility.Visible;
            this.timeOptions.Visibility = Visibility.Visible;
            this.stackEmbedText.Visibility = Visibility.Collapsed;
            this.stackEmbedChecks.Visibility = Visibility.Collapsed;
            this.compressionText.Visibility = Visibility.Collapsed;
            this.compressionOptions.Visibility = Visibility.Collapsed;
            this.textTrim.Margin = new(0, 10, 0, 36);

            Settings.Default.ChatDownloadType = (int)ChatFormat.Text;
            Settings.Default.Save();
        }
    }

    private async void SplitBtnDownload_Click(object sender, RoutedEventArgs e) {
        if (((SplitButton)sender).IsDropDownOpen)
            return;

        var saveFileDialog = new SaveFileDialog {
            FileName = FilenameService.GetFilename(
                Settings.Default.TemplateChat,
                this.textTitle.Text,
                this.downloadId,
                this.currentVideoTime,
                this.textStreamer.Text,
                this.CheckTrimStart.IsChecked == true
                    ? new((int)this.numStartHour.Value, (int)this.numStartMinute.Value, (int)this.numStartSecond.Value)
                    : TimeSpan.Zero,
                this.CheckTrimEnd.IsChecked == true
                    ? new((int)this.numEndHour.Value, (int)this.numEndMinute.Value, (int)this.numEndSecond.Value)
                    : this.vodLength,
                this.viewCount,
                this.game
            )
        };

        if (this.radioJson.IsChecked == true) {
            if (this.radioCompressionNone.IsChecked == true) {
                saveFileDialog.Filter = "JSON Files | *.json";
                saveFileDialog.FileName += ".json";
            } else if (this.radioCompressionGzip.IsChecked == true) {
                saveFileDialog.Filter = "GZip JSON Files | *.json.gz";
                saveFileDialog.FileName += ".json.gz";
            }
        } else if (this.radioHTML.IsChecked == true) {
            saveFileDialog.Filter = "HTML Files | *.html";
            saveFileDialog.FileName += ".html";
        } else if (this.radioText.IsChecked == true) {
            saveFileDialog.Filter = "TXT Files | *.txt";
            saveFileDialog.FileName += ".txt";
        }

        if (saveFileDialog.ShowDialog() != true)
            return;

        try {
            var downloadOptions = this.GetOptions(saveFileDialog.FileName);
            if (this.downloadType == DownloadType.Video) {
                if (this.CheckTrimStart.IsChecked == true) {
                    downloadOptions.TrimBeginning = true;
                    var start = new TimeSpan(
                        (int)this.numStartHour.Value,
                        (int)this.numStartMinute.Value,
                        (int)this.numStartSecond.Value
                    );
                    downloadOptions.TrimBeginningTime = (int)start.TotalSeconds;
                }

                if (this.CheckTrimEnd.IsChecked == true) {
                    downloadOptions.TrimEnding = true;
                    var end = new TimeSpan(
                        (int)this.numEndHour.Value,
                        (int)this.numEndMinute.Value,
                        (int)this.numEndSecond.Value
                    );
                    downloadOptions.TrimEndingTime = (int)end.TotalSeconds;
                }

                downloadOptions.Id = this.downloadId;
            } else
                downloadOptions.Id = this.downloadId;

            if (this.radioTimestampUTC.IsChecked == true)
                downloadOptions.TimeFormat = TimestampFormat.Utc;
            else if (this.radioTimestampRelative.IsChecked == true)
                downloadOptions.TimeFormat = TimestampFormat.Relative;
            else if (this.radioTimestampNone.IsChecked == true)
                downloadOptions.TimeFormat = TimestampFormat.None;

            var downloadProgress = new WpfTaskProgress(
                (LogLevel)Settings.Default.LogLevels,
                this.SetPercent,
                this.SetStatus,
                this.AppendLog
            );
            var currentDownload = new ChatDownloader(downloadOptions, downloadProgress);

            this.btnGetInfo.IsEnabled = false;
            this.SetEnabled(false, false);

            this.SetImage("Images/ppOverheat.gif", true);
            this.statusMessage.Text = Strings.StatusDownloading;
            this._cancellationTokenSource = new();
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
        } catch (Exception ex) {
            this.AppendLog(Strings.ErrorLog + ex.Message);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) {
        this.statusMessage.Text = Strings.StatusCanceling;
        try {
            this._cancellationTokenSource.Cancel();
        } catch (ObjectDisposedException) { }
    }

    private void CheckTrimStart_OnCheckStateChanged(object sender, RoutedEventArgs e) {
        this.SetEnabledTrimStart(this.CheckTrimStart.IsChecked.GetValueOrDefault());
    }

    private void CheckTrimEnd_OnCheckStateChanged(object sender, RoutedEventArgs e) {
        this.SetEnabledTrimEnd(this.CheckTrimEnd.IsChecked.GetValueOrDefault());
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
            await this.GetVideoInfo();
            e.Handled = true;
        }
    }
}
