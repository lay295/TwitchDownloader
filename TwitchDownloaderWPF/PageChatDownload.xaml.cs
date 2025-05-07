using Microsoft.Win32;
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
using TwitchDownloaderCore;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Utils;
using WpfAnimatedGif;

namespace TwitchDownloaderWPF
{
    public enum DownloadType { Clip, Video }
    /// <summary>
    /// Interaction logic for PageChatDownload.xaml
    /// </summary>
    public partial class PageChatDownload : Page
    {
        public DownloadType downloadType;
        public string downloadId;
        public string streamerId;
        public string clipper;
        public string clipperId;
        public DateTime currentVideoTime;
        public TimeSpan vodLength;
        public int viewCount;
        public string game;
        private CancellationTokenSource _cancellationTokenSource;

        public PageChatDownload()
        {
            InitializeComponent();
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            SetEnabled(false);
            SetEnabledTrimStart(false);
            SetEnabledTrimEnd(false);
            checkEmbed.IsChecked = Settings.Default.ChatEmbedEmotes;
            checkBttvEmbed.IsChecked = Settings.Default.BTTVEmotes;
            checkFfzEmbed.IsChecked = Settings.Default.FFZEmotes;
            checkStvEmbed.IsChecked = Settings.Default.STVEmotes;
            NumChatDownloadThreads.Value = Settings.Default.ChatDownloadThreads;
            _ = (ChatFormat)Settings.Default.ChatDownloadType switch
            {
                ChatFormat.Text => radioText.IsChecked = true,
                ChatFormat.Html => radioHTML.IsChecked = true,
                ChatFormat.Json => radioJson.IsChecked = true,
                _ => null,
            };
            _ = (ChatCompression)Settings.Default.ChatJsonCompression switch
            {
                ChatCompression.None => radioCompressionNone.IsChecked = true,
                ChatCompression.Gzip => radioCompressionGzip.IsChecked = true,
                _ => null,
            };
            _ = (TimestampFormat)Settings.Default.ChatTextTimestampStyle switch
            {
                TimestampFormat.Utc => radioTimestampUTC.IsChecked = true,
                TimestampFormat.Relative => radioTimestampRelative.IsChecked = true,
                TimestampFormat.None => radioTimestampNone.IsChecked = true,
                _ => null,
            };
        }

        private void SetEnabled(bool isEnabled)
        {
            CheckTrimStart.IsEnabled = isEnabled;
            CheckTrimEnd.IsEnabled = isEnabled;
            radioTimestampRelative.IsEnabled = isEnabled;
            radioTimestampUTC.IsEnabled = isEnabled;
            radioTimestampNone.IsEnabled = isEnabled;
            radioCompressionNone.IsEnabled = isEnabled;
            radioCompressionGzip.IsEnabled = isEnabled;
            checkEmbed.IsEnabled = isEnabled;
            checkBttvEmbed.IsEnabled = isEnabled;
            checkFfzEmbed.IsEnabled = isEnabled;
            checkStvEmbed.IsEnabled = isEnabled;
            SplitBtnDownload.IsEnabled = isEnabled;
            MenuItemEnqueue.IsEnabled = isEnabled;
            radioJson.IsEnabled = isEnabled;
            radioText.IsEnabled = isEnabled;
            radioHTML.IsEnabled = isEnabled;
        }

        private void SetEnabledTrimStart(bool isEnabled)
        {
            numStartHour.IsEnabled = isEnabled;
            numStartMinute.IsEnabled = isEnabled;
            numStartSecond.IsEnabled = isEnabled;
        }

        private void SetEnabledTrimEnd(bool isEnabled)
        {
            numEndHour.IsEnabled = isEnabled;
            numEndMinute.IsEnabled = isEnabled;
            numEndSecond.IsEnabled = isEnabled;
        }

        private async void btnGetInfo_Click(object sender, RoutedEventArgs e)
        {
            await GetVideoInfo();
        }

        private async Task GetVideoInfo()
        {
            string id = ValidateUrl(textUrl.Text.Trim());
            if (string.IsNullOrWhiteSpace(id))
            {
                MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.UnableToParseLinkMessage, Translations.Strings.UnableToParseLink, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            btnGetInfo.IsEnabled = false;
            downloadId = id;
            downloadType = id.All(char.IsDigit) ? DownloadType.Video : DownloadType.Clip;

            try
            {
                if (downloadType == DownloadType.Video)
                {
                    GqlVideoResponse videoInfo = await TwitchHelper.GetVideoInfo(long.Parse(downloadId));

                    var thumbUrl = videoInfo.data.video.thumbnailURLs.FirstOrDefault();
                    if (!ThumbnailService.TryGetThumb(thumbUrl, out var image))
                    {
                        AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail);
                        _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
                    }
                    imgThumbnail.Source = image;

                    vodLength = TimeSpan.FromSeconds(videoInfo.data.video.lengthSeconds);
                    textTitle.Text = videoInfo.data.video.title;
                    textStreamer.Text = videoInfo.data.video.owner?.displayName ?? Translations.Strings.UnknownUser;
                    var videoTime = videoInfo.data.video.createdAt;
                    textCreatedAt.Text = Settings.Default.UTCVideoTime ? videoTime.ToString(CultureInfo.CurrentCulture) : videoTime.ToLocalTime().ToString(CultureInfo.CurrentCulture);
                    currentVideoTime = Settings.Default.UTCVideoTime ? videoTime : videoTime.ToLocalTime();
                    streamerId = videoInfo.data.video.owner?.id;
                    clipper = null;
                    clipperId = null;
                    viewCount = videoInfo.data.video.viewCount;
                    game = videoInfo.data.video.game?.displayName ?? Translations.Strings.UnknownGame;

                    var urlTimeCodeMatch = TwitchRegex.UrlTimeCode.Match(textUrl.Text);
                    if (urlTimeCodeMatch.Success)
                    {
                        var time = UrlTimeCode.Parse(urlTimeCodeMatch.ValueSpan);
                        CheckTrimStart.IsChecked = true;
                        numStartHour.Value = time.Hours;
                        numStartMinute.Value = time.Minutes;
                        numStartSecond.Value = time.Seconds;
                    }
                    else
                    {
                        numStartHour.Value = 0;
                        numStartMinute.Value = 0;
                        numStartSecond.Value = 0;
                    }

                    if (vodLength > TimeSpan.Zero)
                    {
                        numStartHour.Maximum = (int)vodLength.TotalHours;
                        numEndHour.Maximum = (int)vodLength.TotalHours;
                    }
                    else
                    {
                        numStartHour.Maximum = 48;
                        numEndHour.Maximum = 48;
                    }

                    numEndHour.Value = (int)vodLength.TotalHours;
                    numEndMinute.Value = vodLength.Minutes;
                    numEndSecond.Value = vodLength.Seconds;
                    labelLength.Text = vodLength.ToString("c");
                    SetEnabled(true);
                }
                else if (downloadType == DownloadType.Clip)
                {
                    string clipId = downloadId;
                    GqlClipResponse clipInfo = await TwitchHelper.GetClipInfo(clipId);

                    var thumbUrl = clipInfo.data.clip.thumbnailURL;
                    if (!ThumbnailService.TryGetThumb(thumbUrl, out var image))
                    {
                        AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail);
                        _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
                    }
                    imgThumbnail.Source = image;

                    vodLength = TimeSpan.FromSeconds(clipInfo.data.clip.durationSeconds);
                    textStreamer.Text = clipInfo.data.clip.broadcaster?.displayName ?? Translations.Strings.UnknownUser;
                    var clipCreatedAt = clipInfo.data.clip.createdAt;
                    textCreatedAt.Text = Settings.Default.UTCVideoTime ? clipCreatedAt.ToString(CultureInfo.CurrentCulture) : clipCreatedAt.ToLocalTime().ToString(CultureInfo.CurrentCulture);
                    currentVideoTime = Settings.Default.UTCVideoTime ? clipCreatedAt : clipCreatedAt.ToLocalTime();
                    textTitle.Text = clipInfo.data.clip.title;
                    streamerId = clipInfo.data.clip.broadcaster?.id;
                    clipper = clipInfo.data.clip.curator?.displayName ?? Translations.Strings.UnknownUser;
                    clipperId = clipInfo.data.clip.curator?.id;
                    labelLength.Text = vodLength.ToString("c");
                    SetEnabled(true);

                    numStartHour.Maximum = 0;
                    numStartHour.Value = 0;
                    numStartMinute.Maximum = vodLength.Minutes;
                    numStartMinute.Value = 0;
                    numStartSecond.Value = 0;

                    numEndHour.Maximum = 0;
                    numEndHour.Value = 0;
                    numEndMinute.Maximum = vodLength.Minutes;
                    numEndMinute.Value = vodLength.Minutes;
                    numEndSecond.Value = vodLength.Seconds;
                }

                btnGetInfo.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.UnableToGetInfoMessage, Translations.Strings.UnableToGetInfo, MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                btnGetInfo.IsEnabled = true;
                if (Settings.Default.VerboseErrors)
                {
                    MessageBox.Show(Application.Current.MainWindow!, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

        public static string ValidateUrl(string text)
        {
            var vodClipIdMatch = IdParse.MatchVideoOrClipId(text);
            return vodClipIdMatch is { Success: true }
                ? vodClipIdMatch.Value
                : null;
        }

        private void SetPercent(int percent)
        {
            Dispatcher.BeginInvoke(() =>
                statusProgressBar.Value = percent
            );
        }

        private void SetStatus(string message)
        {
            Dispatcher.BeginInvoke(() =>
                statusMessage.Text = message
            );
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke(() =>
                textLog.AppendText(message + Environment.NewLine)
            );
        }

        public ChatDownloadOptions GetOptions(string filename)
        {
            ChatDownloadOptions options = new ChatDownloadOptions();

            if (radioJson.IsChecked == true)
                options.DownloadFormat = ChatFormat.Json;
            else if (radioHTML.IsChecked == true)
                options.DownloadFormat = ChatFormat.Html;
            else if (radioText.IsChecked == true)
                options.DownloadFormat = ChatFormat.Text;

            // TODO: Support non-json chat compression
            if (radioCompressionNone.IsChecked == true || options.DownloadFormat != ChatFormat.Json)
                options.Compression = ChatCompression.None;
            else if (radioCompressionGzip.IsChecked == true)
                options.Compression = ChatCompression.Gzip;

            if (CheckTrimStart.IsChecked == true)
            {
                options.TrimBeginning = true;
                TimeSpan start = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                options.TrimBeginningTime = (int)start.TotalSeconds;
            }

            if (CheckTrimEnd.IsChecked == true)
            {
                options.TrimEnding = true;
                TimeSpan end = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
                options.TrimEndingTime = (int)end.TotalSeconds;
            }

            if (radioTimestampUTC.IsChecked == true)
                options.TimeFormat = TimestampFormat.Utc;
            else if (radioTimestampRelative.IsChecked == true)
                options.TimeFormat = TimestampFormat.Relative;
            else if (radioTimestampNone.IsChecked == true)
                options.TimeFormat = TimestampFormat.None;

            options.Id = downloadId;
            options.EmbedData = checkEmbed.IsChecked.GetValueOrDefault();
            options.BttvEmotes = checkBttvEmbed.IsChecked.GetValueOrDefault();
            options.FfzEmotes = checkFfzEmbed.IsChecked.GetValueOrDefault();
            options.StvEmotes = checkStvEmbed.IsChecked.GetValueOrDefault();
            options.Filename = filename;
            options.DownloadThreads = (int)NumChatDownloadThreads.Value;
            return options;
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
            statusImage.Visibility = Settings.Default.ReduceMotion ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
            statusImage.Visibility = Settings.Default.ReduceMotion ? Visibility.Collapsed : Visibility.Visible;
        }

        private void NumChatDownloadThreads_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (this.IsInitialized)
            {
                NumChatDownloadThreads.Value = Math.Clamp((int)NumChatDownloadThreads.Value, 1, 50);
                Settings.Default.ChatDownloadThreads = (int)NumChatDownloadThreads.Value;
                Settings.Default.Save();
            }
        }

        private void checkEmbed_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatEmbedEmotes = true;
                Settings.Default.Save();
                checkBttvEmbed.IsEnabled = true;
                checkFfzEmbed.IsEnabled = true;
                checkStvEmbed.IsEnabled = true;
            }
        }

        private void checkEmbed_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatEmbedEmotes = false;
                Settings.Default.Save();
                checkBttvEmbed.IsEnabled = false;
                checkFfzEmbed.IsEnabled = false;
                checkStvEmbed.IsEnabled = false;
            }
        }

        private void checkBttvEmbed_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.BTTVEmotes = true;
                Settings.Default.Save();
            }
        }

        private void checkBttvEmbed_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.BTTVEmotes = false;
                Settings.Default.Save();
            }
        }

        private void checkFfzEmbed_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.FFZEmotes = true;
                Settings.Default.Save();
            }
        }

        private void checkFfzEmbed_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.FFZEmotes = false;
                Settings.Default.Save();
            }
        }

        private void checkStvEmbed_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.STVEmotes = true;
                Settings.Default.Save();
            }
        }

        private void checkStvEmbed_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.STVEmotes = false;
                Settings.Default.Save();
            }
        }

        private void radioJson_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                timeText.Visibility = Visibility.Collapsed;
                timeOptions.Visibility = Visibility.Collapsed;
                stackEmbedText.Visibility = Visibility.Visible;
                stackEmbedChecks.Visibility = Visibility.Visible;
                compressionText.Visibility = Visibility.Visible;
                compressionOptions.Visibility = Visibility.Visible;
                textTrim.Margin = new Thickness(0, 10, 0, 33);

                Settings.Default.ChatDownloadType = (int)ChatFormat.Json;
                Settings.Default.Save();
            }
        }

        private void radioHTML_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                timeText.Visibility = Visibility.Collapsed;
                timeOptions.Visibility = Visibility.Collapsed;
                stackEmbedText.Visibility = Visibility.Visible;
                stackEmbedChecks.Visibility = Visibility.Visible;
                compressionText.Visibility = Visibility.Collapsed;
                compressionOptions.Visibility = Visibility.Collapsed;
                textTrim.Margin = new Thickness(0, 17, 0, 33);

                Settings.Default.ChatDownloadType = (int)ChatFormat.Html;
                Settings.Default.Save();
            }
        }

        private void radioText_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                timeText.Visibility = Visibility.Visible;
                timeOptions.Visibility = Visibility.Visible;
                stackEmbedText.Visibility = Visibility.Collapsed;
                stackEmbedChecks.Visibility = Visibility.Collapsed;
                compressionText.Visibility = Visibility.Collapsed;
                compressionOptions.Visibility = Visibility.Collapsed;
                textTrim.Margin = new Thickness(0, 10, 0, 36);

                Settings.Default.ChatDownloadType = (int)ChatFormat.Text;
                Settings.Default.Save();
            }
        }

        private async void SplitBtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (((HandyControl.Controls.SplitButton)sender).IsDropDownOpen)
            {
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                FileName = FilenameService.GetFilename(Settings.Default.TemplateChat, textTitle.Text, downloadId, currentVideoTime, textStreamer.Text, streamerId,
                    CheckTrimStart.IsChecked == true ? new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value) : TimeSpan.Zero,
                    CheckTrimEnd.IsChecked == true ? new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value) : vodLength,
                    vodLength, viewCount, game, clipper, clipperId)
            };

            if (radioJson.IsChecked == true)
            {
                if (radioCompressionNone.IsChecked == true)
                {
                    saveFileDialog.Filter = "JSON Files | *.json";
                    saveFileDialog.FileName += ".json";
                }
                else if (radioCompressionGzip.IsChecked == true)
                {
                    saveFileDialog.Filter = "GZip JSON Files | *.json.gz";
                    saveFileDialog.FileName += ".json.gz";
                }
            }
            else if (radioHTML.IsChecked == true)
            {
                saveFileDialog.Filter = "HTML Files | *.html";
                saveFileDialog.FileName += ".html";
            }
            else if (radioText.IsChecked == true)
            {
                saveFileDialog.Filter = "TXT Files | *.txt";
                saveFileDialog.FileName += ".txt";
            }

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ChatDownloadOptions downloadOptions = GetOptions(saveFileDialog.FileName);

                var downloadProgress = new WpfTaskProgress((LogLevel)Settings.Default.LogLevels, SetPercent, SetStatus, AppendLog);
                var currentDownload = new ChatDownloader(downloadOptions, downloadProgress);

                btnGetInfo.IsEnabled = false;
                SetEnabled(false);

                SetImage("Images/ppOverheat.gif", true);
                statusMessage.Text = Translations.Strings.StatusDownloading;
                _cancellationTokenSource = new CancellationTokenSource();
                UpdateActionButtons(true);

                try
                {
                    await currentDownload.DownloadAsync(_cancellationTokenSource.Token);
                    downloadProgress.SetStatus(Translations.Strings.StatusDone);
                    SetImage("Images/ppHop.gif", true);
                }
                catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && _cancellationTokenSource.IsCancellationRequested)
                {
                    downloadProgress.SetStatus(Translations.Strings.StatusCanceled);
                    SetImage("Images/ppHop.gif", true);
                }
                catch (Exception ex)
                {
                    downloadProgress.SetStatus(Translations.Strings.StatusError);
                    SetImage("Images/peepoSad.png", false);
                    AppendLog(Translations.Strings.ErrorLog + ex.Message);
                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(Application.Current.MainWindow!, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                btnGetInfo.IsEnabled = true;
                downloadProgress.ReportProgress(0);
                _cancellationTokenSource.Dispose();
                UpdateActionButtons(false);

                GC.Collect();
            }
            catch (Exception ex)
            {
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            statusMessage.Text = Translations.Strings.StatusCanceling;
            SetImage("Images/ppStretch.gif", true);
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException) { }
        }

        private void CheckTrimStart_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledTrimStart(CheckTrimStart.IsChecked.GetValueOrDefault());
        }

        private void CheckTrimEnd_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledTrimEnd(CheckTrimEnd.IsChecked.GetValueOrDefault());
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
                await GetVideoInfo();
                e.Handled = true;
            }
        }

        private void RadioCompressionNone_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.ChatJsonCompression = (int)ChatCompression.None;
            Settings.Default.Save();
        }

        private void RadioCompressionGzip_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.ChatJsonCompression = (int)ChatCompression.Gzip;
            Settings.Default.Save();
        }

        private void RadioTimestampUTC_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.ChatTextTimestampStyle = (int)TimestampFormat.Utc;
            Settings.Default.Save();
        }

        private void RadioTimestampRelative_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.ChatTextTimestampStyle = (int)TimestampFormat.Relative;
            Settings.Default.Save();
        }

        private void RadioTimestampNone_OnCheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized)
                return;

            Settings.Default.ChatTextTimestampStyle = (int)TimestampFormat.None;
            Settings.Default.Save();
        }
    }
}