using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using HandyControl.Controls;
using Microsoft.Win32;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Translations;
using TwitchDownloaderWPF.Utils;
using WpfAnimatedGif;
using MessageBox = System.Windows.MessageBox;

namespace TwitchDownloaderWPF;

/// <summary>
///     Interaction logic for PageChatUpdate.xaml
/// </summary>
public partial class PageChatUpdate : Page {
    private CancellationTokenSource _cancellationTokenSource;
    public ChatRoot ChatJsonInfo;
    public string Game;

    public string InputFile;
    public DateTime VideoCreatedAt;
    public string VideoId;
    public TimeSpan VideoLength;
    public int ViewCount;

    public PageChatUpdate() { this.InitializeComponent(); }

    private async void btnBrowse_Click(object sender, RoutedEventArgs e) {
        var openFileDialog = new OpenFileDialog {
            Filter = "JSON Files | *.json;*.json.gz"
        };
        if (openFileDialog.ShowDialog() != true)
            return;

        this.textJson.Text = openFileDialog.FileName;
        this.InputFile = openFileDialog.FileName;
        this.ChatJsonInfo = null;
        this.imgThumbnail.Source = null;
        this.SetEnabled(false);

        if (Path.GetExtension(this.InputFile)!.ToLower() is not ".json" and not ".gz") {
            this.textJson.Text = "";
            this.InputFile = "";
            return;
        }

        try {
            this.ChatJsonInfo = await ChatJson.DeserializeAsync(
                this.InputFile,
                true,
                true,
                false,
                CancellationToken.None
            );
            GC.Collect();
        } catch (Exception ex) {
            this.AppendLog(Strings.ErrorLog + ex.Message);
            if (Settings.Default.VerboseErrors)
                MessageBox.Show(
                    Application.Current.MainWindow!,
                    ex.ToString(),
                    Strings.VerboseErrorOutput,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

            return;
        }

        this.SetEnabled(true);

        var videoCreatedAt = this.ChatJsonInfo.video.created_at == default
            ? this.ChatJsonInfo.comments[0].created_at
            - TimeSpan.FromSeconds(this.ChatJsonInfo.comments[0].content_offset_seconds)
            : this.ChatJsonInfo.video.created_at;
        this.textCreatedAt.Text = Settings.Default.UTCVideoTime
            ? videoCreatedAt.ToString(CultureInfo.CurrentCulture)
            : videoCreatedAt.ToLocalTime().ToString(CultureInfo.CurrentCulture);
        this.VideoCreatedAt = Settings.Default.UTCVideoTime ? videoCreatedAt : videoCreatedAt.ToLocalTime();

        this.textStreamer.Text = this.ChatJsonInfo.streamer.name;
        this.textTitle.Text = this.ChatJsonInfo.video.title ?? Strings.Unknown;

        var chatStart = TimeSpan.FromSeconds(this.ChatJsonInfo.video.start);
        this.numStartHour.Value = (int)chatStart.TotalHours;
        this.numStartMinute.Value = chatStart.Minutes;
        this.numStartSecond.Value = chatStart.Seconds;

        var chatEnd = TimeSpan.FromSeconds(this.ChatJsonInfo.video.end);
        this.numEndHour.Value = (int)chatEnd.TotalHours;
        this.numEndMinute.Value = chatEnd.Minutes;
        this.numEndSecond.Value = chatEnd.Seconds;

        this.VideoLength = TimeSpan.FromSeconds(
            double.IsNegative(this.ChatJsonInfo.video.length) ? 0.0 : this.ChatJsonInfo.video.length
        );
        this.labelLength.Text = this.VideoLength.Seconds > 0
            ? this.VideoLength.ToString("c")
            : Strings.UnknownVideoLength;

        this.VideoId = this.ChatJsonInfo.video.id ?? this.ChatJsonInfo.comments.FirstOrDefault()?.content_id ?? "-1";
        this.ViewCount = this.ChatJsonInfo.video.viewCount;
        this.Game = this.ChatJsonInfo.video.game
            ?? this.ChatJsonInfo.video.chapters.FirstOrDefault()?.gameDisplayName ?? Strings.UnknownGame;

        try {
            if (this.VideoId.All(char.IsDigit)) {
                var videoInfo = await TwitchHelper.GetVideoInfo(long.Parse(this.VideoId));
                if (videoInfo.data.video == null) {
                    this.AppendLog(
                        Strings.ErrorLog + Strings.UnableToFindThumbnail + ": " + Strings.VodExpiredOrIdCorrupt
                    );
                    _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out var image);
                    this.imgThumbnail.Source = image;

                    this.numStartHour.Maximum = 48;
                    this.numEndHour.Maximum = 48;
                } else {
                    this.VideoLength = TimeSpan.FromSeconds(videoInfo.data.video.lengthSeconds);
                    this.labelLength.Text = this.VideoLength.ToString("c");
                    this.numStartHour.Maximum = (int)this.VideoLength.TotalHours;
                    this.numEndHour.Maximum = (int)this.VideoLength.TotalHours;
                    this.ViewCount = videoInfo.data.video.viewCount;
                    this.Game = videoInfo.data.video.game?.displayName;

                    var thumbUrl = videoInfo.data.video.thumbnailURLs.FirstOrDefault();
                    if (!ThumbnailService.TryGetThumb(thumbUrl, out var image)) {
                        this.AppendLog(Strings.ErrorLog + Strings.UnableToFindThumbnail);
                        _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
                    }

                    this.imgThumbnail.Source = image;
                }
            } else {
                if (this.VideoId != "-1") {
                    this.numStartHour.Maximum = 0;
                    this.numEndHour.Maximum = 0;
                }

                var videoInfo = await TwitchHelper.GetClipInfo(this.VideoId);
                if (videoInfo.data.clip.video == null) {
                    this.AppendLog(
                        Strings.ErrorLog + Strings.UnableToFindThumbnail + ": " + Strings.VodExpiredOrIdCorrupt
                    );
                    _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out var image);
                    this.imgThumbnail.Source = image;
                } else {
                    this.VideoLength = TimeSpan.FromSeconds(videoInfo.data.clip.durationSeconds);
                    this.labelLength.Text = this.VideoLength.ToString("c");
                    this.ViewCount = videoInfo.data.clip.viewCount;
                    this.Game = videoInfo.data.clip.game?.displayName;

                    var thumbUrl = videoInfo.data.clip.thumbnailURL;
                    if (!ThumbnailService.TryGetThumb(thumbUrl, out var image)) {
                        this.AppendLog(Strings.ErrorLog + Strings.UnableToFindThumbnail);
                        _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
                    }

                    this.imgThumbnail.Source = image;
                }
            }
        } catch (Exception ex) {
            MessageBox.Show(
                Application.Current.MainWindow!,
                Strings.UnableToGetInfoMessage,
                Strings.UnableToGetInfo,
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
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
    }

    private void UpdateActionButtons(bool isUpdating) {
        if (isUpdating) {
            this.SplitBtnUpdate.Visibility = Visibility.Collapsed;
            this.BtnCancel.Visibility = Visibility.Visible;
            return;
        }

        this.SplitBtnUpdate.Visibility = Visibility.Visible;
        this.BtnCancel.Visibility = Visibility.Collapsed;
    }

    private void Page_Initialized(object sender, EventArgs e) {
        this.SetEnabled(false);
        this.SetEnabledTrimStart(false);
        this.SetEnabledTrimEnd(false);
        this.checkEmbedMissing.IsChecked = Settings.Default.ChatEmbedMissing;
        this.checkReplaceEmbeds.IsChecked = Settings.Default.ChatReplaceEmbeds;
        this.checkBttvEmbed.IsChecked = Settings.Default.BTTVEmotes;
        this.checkFfzEmbed.IsChecked = Settings.Default.FFZEmotes;
        this.checkStvEmbed.IsChecked = Settings.Default.STVEmotes;
        _ = (ChatFormat)Settings.Default.ChatDownloadType switch {
            ChatFormat.Text => this.radioText.IsChecked = true,
            ChatFormat.Html => this.radioHTML.IsChecked = true,
            _ => this.radioJson.IsChecked = true
        };
    }

    private void SetEnabled(bool isEnabled) {
        this.checkStart.IsEnabled = isEnabled;
        this.checkEnd.IsEnabled = isEnabled;
        this.checkEmbedMissing.IsEnabled = isEnabled;
        this.checkReplaceEmbeds.IsEnabled = isEnabled;
        this.SplitBtnUpdate.IsEnabled = isEnabled;
        this.MenuItemEnqueue.IsEnabled = isEnabled;
        this.radioTimestampRelative.IsEnabled = isEnabled;
        this.radioTimestampUTC.IsEnabled = isEnabled;
        this.radioTimestampNone.IsEnabled = isEnabled;
        this.radioCompressionNone.IsEnabled = isEnabled;
        this.radioCompressionGzip.IsEnabled = isEnabled;
        this.radioJson.IsEnabled = isEnabled;
        this.radioText.IsEnabled = isEnabled;
        this.radioHTML.IsEnabled = isEnabled;

        if (isEnabled)
            return;

        this.checkBttvEmbed.IsEnabled = isEnabled;
        this.checkFfzEmbed.IsEnabled = isEnabled;
        this.checkStvEmbed.IsEnabled = isEnabled;
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

    public ChatUpdateOptions GetOptions(string outputFile) {
        var options = new ChatUpdateOptions {
            EmbedMissing = this.checkEmbedMissing.IsChecked.GetValueOrDefault(),
            ReplaceEmbeds = this.checkReplaceEmbeds.IsChecked.GetValueOrDefault(),
            BttvEmotes = this.checkBttvEmbed.IsChecked.GetValueOrDefault(),
            FfzEmotes = this.checkFfzEmbed.IsChecked.GetValueOrDefault(),
            StvEmotes = this.checkStvEmbed.IsChecked.GetValueOrDefault(),
            InputFile = this.textJson.Text,
            OutputFile = outputFile,
            TrimBeginningTime = -1,
            TrimEndingTime = -1
        };

        if (this.radioJson.IsChecked.GetValueOrDefault())
            options.OutputFormat = ChatFormat.Json;
        else if (this.radioHTML.IsChecked.GetValueOrDefault())
            options.OutputFormat = ChatFormat.Html;
        else if (this.radioText.IsChecked.GetValueOrDefault())
            options.OutputFormat = ChatFormat.Text;

        if (this.radioCompressionNone.IsChecked == true)
            options.Compression = ChatCompression.None;
        else if (this.radioCompressionGzip.IsChecked == true)
            options.Compression = ChatCompression.Gzip;

        if (this.checkStart.IsChecked == true) {
            options.TrimBeginning = true;
            var start = new TimeSpan(
                (int)this.numStartHour.Value,
                (int)this.numStartMinute.Value,
                (int)this.numStartSecond.Value
            );
            options.TrimBeginningTime = (int)Math.Round(start.TotalSeconds);
        }

        if (this.checkEnd.IsChecked == true) {
            options.TrimEnding = true;
            var end = new TimeSpan(
                (int)this.numEndHour.Value,
                (int)this.numEndMinute.Value,
                (int)this.numEndSecond.Value
            );
            options.TrimEndingTime = (int)Math.Round(end.TotalSeconds);
        }

        if (this.radioTimestampUTC.IsChecked.GetValueOrDefault())
            options.TextTimestampFormat = TimestampFormat.Utc;
        else if (this.radioTimestampRelative.IsChecked.GetValueOrDefault())
            options.TextTimestampFormat = TimestampFormat.Relative;
        else if (this.radioTimestampNone.IsChecked.GetValueOrDefault())
            options.TextTimestampFormat = TimestampFormat.None;

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

    private void checkEmbedMissing_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.ChatEmbedMissing = true;
            Settings.Default.ChatReplaceEmbeds = false;
            Settings.Default.Save();
            this.checkReplaceEmbeds.IsChecked = false;
            this.checkBttvEmbed.IsEnabled = true;
            this.checkFfzEmbed.IsEnabled = true;
            this.checkStvEmbed.IsEnabled = true;
        }
    }

    private void checkEmbedMissing_Unchecked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.ChatEmbedMissing = false;
            Settings.Default.Save();
            this.checkBttvEmbed.IsEnabled = false;
            this.checkFfzEmbed.IsEnabled = false;
            this.checkStvEmbed.IsEnabled = false;
        }
    }

    private void checkReplaceEmbeds_Checked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.ChatEmbedMissing = false;
            Settings.Default.ChatReplaceEmbeds = true;
            Settings.Default.Save();
            this.checkEmbedMissing.IsChecked = false;
            this.checkBttvEmbed.IsEnabled = true;
            this.checkFfzEmbed.IsEnabled = true;
            this.checkStvEmbed.IsEnabled = true;
        }
    }

    private void checkReplaceEmbeds_Unchecked(object sender, RoutedEventArgs e) {
        if (this.IsInitialized) {
            Settings.Default.ChatReplaceEmbeds = false;
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

    private async void SplitBtnUpdate_Click(object sender, RoutedEventArgs e) {
        if (((SplitButton)sender).IsDropDownOpen)
            return;

        var saveFileDialog = new SaveFileDialog {
            FileName = FilenameService.GetFilename(
                Settings.Default.TemplateChat,
                this.textTitle.Text,
                this.ChatJsonInfo.video.id ?? this.ChatJsonInfo.comments.FirstOrDefault()?.content_id ?? "-1",
                this.VideoCreatedAt,
                this.textStreamer.Text,
                this.checkStart.IsChecked == true
                    ? new((int)this.numStartHour.Value, (int)this.numStartMinute.Value, (int)this.numStartSecond.Value)
                    : TimeSpan.FromSeconds(
                        double.IsNegative(this.ChatJsonInfo.video.start) ? 0.0 : this.ChatJsonInfo.video.start
                    ),
                this.checkEnd.IsChecked == true
                    ? new((int)this.numEndHour.Value, (int)this.numEndMinute.Value, (int)this.numEndSecond.Value)
                    : this.VideoLength,
                this.ViewCount,
                this.Game
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
            var updateOptions = this.GetOptions(saveFileDialog.FileName);

            var updateProgress = new WpfTaskProgress(
                (LogLevel)Settings.Default.LogLevels,
                this.SetPercent,
                this.SetStatus,
                this.AppendLog
            );
            var currentUpdate = new ChatUpdater(updateOptions, updateProgress);
            try {
                await currentUpdate.ParseJsonAsync(CancellationToken.None);
            } catch (Exception ex) {
                this.AppendLog(Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors)
                    MessageBox.Show(
                        Application.Current.MainWindow!,
                        ex.ToString(),
                        Strings.VerboseErrorOutput,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                return;
            }

            this.btnBrowse.IsEnabled = false;
            this.SetEnabled(false);

            this.SetImage("Images/ppOverheat.gif", true);
            this.statusMessage.Text = Strings.StatusUpdating;
            this._cancellationTokenSource = new();
            this.UpdateActionButtons(true);

            try {
                await currentUpdate.UpdateAsync(this._cancellationTokenSource.Token);
                this.textJson.Text = "";
                updateProgress.SetStatus(Strings.StatusDone);
                this.SetImage("Images/ppHop.gif", true);
            } catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException
                && this._cancellationTokenSource.IsCancellationRequested) {
                updateProgress.SetStatus(Strings.StatusCanceled);
                this.SetImage("Images/ppHop.gif", true);
            } catch (Exception ex) {
                updateProgress.SetStatus(Strings.StatusError);
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

            this.btnBrowse.IsEnabled = true;
            updateProgress.ReportProgress(0);
            this._cancellationTokenSource.Dispose();
            this.UpdateActionButtons(false);

            currentUpdate = null;
            GC.Collect();
        } catch (Exception ex) {
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
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) {
        this.statusMessage.Text = Strings.StatusCanceling;
        try {
            this._cancellationTokenSource.Cancel();
        } catch (ObjectDisposedException) { }
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
            this.textTrim.Margin = new(0, 10, 0, 0);

            Settings.Default.ChatDownloadType = (int)ChatFormat.Text;
            Settings.Default.Save();
        }
    }

    private void checkStart_OnCheckStateChanged(object sender, RoutedEventArgs e) {
        this.SetEnabledTrimStart(this.checkStart.IsChecked.GetValueOrDefault());
    }

    private void checkEnd_OnCheckStateChanged(object sender, RoutedEventArgs e) {
        this.SetEnabledTrimEnd(this.checkEnd.IsChecked.GetValueOrDefault());
    }

    private void MenuItemEnqueue_Click(object sender, RoutedEventArgs e) {
        var queueOptions = new WindowQueueOptions(this) {
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        queueOptions.ShowDialog();
    }
}
