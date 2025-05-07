using Microsoft.Win32;
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
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Utils;
using WpfAnimatedGif;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageChatUpdate.xaml
    /// </summary>
    public partial class PageChatUpdate : Page
    {
        public string InputFile;
        public ChatRoot ChatJsonInfo;
        public string VideoId;
        public string StreamerId;
        public string ClipperName;
        public string ClipperId;
        public DateTime VideoCreatedAt;
        public TimeSpan VideoLength;
        public int ViewCount;
        public string Game;
        private CancellationTokenSource _cancellationTokenSource;

        public PageChatUpdate()
        {
            InitializeComponent();
        }

        private async void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "JSON Files | *.json;*.json.gz"
            };
            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            textJson.Text = openFileDialog.FileName;
            InputFile = openFileDialog.FileName;
            ChatJsonInfo = null;
            imgThumbnail.Source = null;
            SetEnabled(false);

            if (Path.GetExtension(InputFile)!.ToLower() is not ".json" and not ".gz")
            {
                textJson.Text = "";
                InputFile = "";
                return;
            }

            try
            {
                ChatJsonInfo = await ChatJson.DeserializeAsync(InputFile, true, true, false, CancellationToken.None);
                GC.Collect();
            }
            catch (Exception ex)
            {
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors)
                {
                    MessageBox.Show(Application.Current.MainWindow!, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return;
            }

            SetEnabled(true);

            var videoCreatedAt = ChatJsonInfo.video.created_at == default
                ? ChatJsonInfo.comments[0].created_at - TimeSpan.FromSeconds(ChatJsonInfo.comments[0].content_offset_seconds)
                : ChatJsonInfo.video.created_at;
            textCreatedAt.Text = Settings.Default.UTCVideoTime ? videoCreatedAt.ToString(CultureInfo.CurrentCulture) : videoCreatedAt.ToLocalTime().ToString(CultureInfo.CurrentCulture);
            VideoCreatedAt = Settings.Default.UTCVideoTime ? videoCreatedAt : videoCreatedAt.ToLocalTime();

            textStreamer.Text = ChatJsonInfo.streamer.name;
            textTitle.Text = ChatJsonInfo.video.title ?? Translations.Strings.Unknown;

            TimeSpan chatStart = TimeSpan.FromSeconds(ChatJsonInfo.video.start);
            numStartHour.Value = (int)chatStart.TotalHours;
            numStartMinute.Value = chatStart.Minutes;
            numStartSecond.Value = chatStart.Seconds;

            TimeSpan chatEnd = TimeSpan.FromSeconds(ChatJsonInfo.video.end);
            numEndHour.Value = (int)chatEnd.TotalHours;
            numEndMinute.Value = chatEnd.Minutes;
            numEndSecond.Value = chatEnd.Seconds;

            VideoLength = TimeSpan.FromSeconds(double.IsNegative(ChatJsonInfo.video.length) ? 0.0 : ChatJsonInfo.video.length);
            labelLength.Text = VideoLength.Seconds > 0
                ? VideoLength.ToString("c")
                : Translations.Strings.UnknownVideoLength;

            VideoId = ChatJsonInfo.video.id ?? ChatJsonInfo.comments.FirstOrDefault()?.content_id ?? "-1";
            StreamerId = ChatJsonInfo.streamer.id.ToString(CultureInfo.InvariantCulture);
            ClipperName = ChatJsonInfo.clipper?.name;
            ClipperId = ChatJsonInfo.clipper?.id.ToString(CultureInfo.InvariantCulture);
            ViewCount = ChatJsonInfo.video.viewCount;
            Game = ChatJsonInfo.video.game ?? ChatJsonInfo.video.chapters.FirstOrDefault()?.gameDisplayName ?? Translations.Strings.UnknownGame;

            try
            {
                if (VideoId.All(char.IsDigit))
                {
                    GqlVideoResponse videoInfo = await TwitchHelper.GetVideoInfo(long.Parse(VideoId));
                    if (videoInfo.data.video == null)
                    {
                        AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail + ": " + Translations.Strings.VodExpiredOrIdCorrupt);
                        _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out var image);
                        imgThumbnail.Source = image;

                        numStartHour.Maximum = 48;
                        numEndHour.Maximum = 48;
                    }
                    else
                    {
                        VideoLength = TimeSpan.FromSeconds(videoInfo.data.video.lengthSeconds);
                        labelLength.Text = VideoLength.ToString("c");
                        if (VideoLength > TimeSpan.Zero)
                        {
                            numStartHour.Maximum = (int)VideoLength.TotalHours;
                            numEndHour.Maximum = (int)VideoLength.TotalHours;
                        }
                        else
                        {
                            numStartHour.Maximum = 48;
                            numEndHour.Maximum = 48;
                        }

                        ViewCount = videoInfo.data.video.viewCount;
                        Game = videoInfo.data.video.game?.displayName;

                        var thumbUrl = videoInfo.data.video.thumbnailURLs.FirstOrDefault();
                        if (!ThumbnailService.TryGetThumb(thumbUrl, out var image))
                        {
                            AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail);
                            _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
                        }

                        imgThumbnail.Source = image;
                    }
                }
                else
                {
                    if (VideoId != "-1")
                    {
                        numStartHour.Maximum = 0;
                        numEndHour.Maximum = 0;
                    }

                    GqlClipResponse clipInfo = await TwitchHelper.GetClipInfo(VideoId);
                    if (clipInfo.data.clip.video == null)
                    {
                        AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail + ": " + Translations.Strings.VodExpiredOrIdCorrupt);
                        _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out var image);
                        imgThumbnail.Source = image;
                    }
                    else
                    {
                        VideoLength = TimeSpan.FromSeconds(clipInfo.data.clip.durationSeconds);
                        labelLength.Text = VideoLength.ToString("c");
                        ViewCount = clipInfo.data.clip.viewCount;
                        Game = clipInfo.data.clip.game?.displayName;
                        ClipperName ??= clipInfo.data.clip.curator?.displayName ?? Translations.Strings.UnknownUser;
                        ClipperId ??= clipInfo.data.clip.curator?.id;

                        var thumbUrl = clipInfo.data.clip.thumbnailURL;
                        if (!ThumbnailService.TryGetThumb(thumbUrl, out var image))
                        {
                            AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail);
                            _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
                        }

                        imgThumbnail.Source = image;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.UnableToGetInfoMessage, Translations.Strings.UnableToGetInfo, MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors)
                {
                    MessageBox.Show(Application.Current.MainWindow!, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateActionButtons(bool isUpdating)
        {
            if (isUpdating)
            {
                SplitBtnUpdate.Visibility = Visibility.Collapsed;
                BtnCancel.Visibility = Visibility.Visible;
                return;
            }
            SplitBtnUpdate.Visibility = Visibility.Visible;
            BtnCancel.Visibility = Visibility.Collapsed;
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            SetEnabled(false);
            SetEnabledTrimStart(false);
            SetEnabledTrimEnd(false);
            checkEmbedMissing.IsChecked = Settings.Default.ChatEmbedMissing;
            checkReplaceEmbeds.IsChecked = Settings.Default.ChatReplaceEmbeds;
            checkBttvEmbed.IsChecked = Settings.Default.BTTVEmotes;
            checkFfzEmbed.IsChecked = Settings.Default.FFZEmotes;
            checkStvEmbed.IsChecked = Settings.Default.STVEmotes;
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
            checkStart.IsEnabled = isEnabled;
            checkEnd.IsEnabled = isEnabled;
            checkEmbedMissing.IsEnabled = isEnabled;
            checkReplaceEmbeds.IsEnabled = isEnabled;
            SplitBtnUpdate.IsEnabled = isEnabled;
            MenuItemEnqueue.IsEnabled = isEnabled;
            radioTimestampRelative.IsEnabled = isEnabled;
            radioTimestampUTC.IsEnabled = isEnabled;
            radioTimestampNone.IsEnabled = isEnabled;
            radioCompressionNone.IsEnabled = isEnabled;
            radioCompressionGzip.IsEnabled = isEnabled;
            radioJson.IsEnabled = isEnabled;
            radioText.IsEnabled = isEnabled;
            radioHTML.IsEnabled = isEnabled;

            if (isEnabled)
                return;

            checkBttvEmbed.IsEnabled = isEnabled;
            checkFfzEmbed.IsEnabled = isEnabled;
            checkStvEmbed.IsEnabled = isEnabled;
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

        public ChatUpdateOptions GetOptions(string outputFile)
        {
            ChatUpdateOptions options = new ChatUpdateOptions()
            {
                EmbedMissing = checkEmbedMissing.IsChecked.GetValueOrDefault(),
                ReplaceEmbeds = checkReplaceEmbeds.IsChecked.GetValueOrDefault(),
                BttvEmotes = checkBttvEmbed.IsChecked.GetValueOrDefault(),
                FfzEmotes = checkFfzEmbed.IsChecked.GetValueOrDefault(),
                StvEmotes = checkStvEmbed.IsChecked.GetValueOrDefault(),
                InputFile = textJson.Text,
                OutputFile = outputFile,
                TrimBeginningTime = -1,
                TrimEndingTime = -1
            };

            if (radioJson.IsChecked.GetValueOrDefault())
                options.OutputFormat = ChatFormat.Json;
            else if (radioHTML.IsChecked.GetValueOrDefault())
                options.OutputFormat = ChatFormat.Html;
            else if (radioText.IsChecked.GetValueOrDefault())
                options.OutputFormat = ChatFormat.Text;

            // TODO: Support non-json chat compression
            if (radioCompressionNone.IsChecked == true || options.OutputFormat != ChatFormat.Json)
                options.Compression = ChatCompression.None;
            else if (radioCompressionGzip.IsChecked == true)
                options.Compression = ChatCompression.Gzip;

            if (checkStart.IsChecked == true)
            {
                options.TrimBeginning = true;
                TimeSpan start = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                options.TrimBeginningTime = (int)Math.Round(start.TotalSeconds);
            }
            if (checkEnd.IsChecked == true)
            {
                options.TrimEnding = true;
                TimeSpan end = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
                options.TrimEndingTime = (int)Math.Round(end.TotalSeconds);
            }

            if (radioTimestampUTC.IsChecked.GetValueOrDefault())
                options.TextTimestampFormat = TimestampFormat.Utc;
            else if (radioTimestampRelative.IsChecked.GetValueOrDefault())
                options.TextTimestampFormat = TimestampFormat.Relative;
            else if (radioTimestampNone.IsChecked.GetValueOrDefault())
                options.TextTimestampFormat = TimestampFormat.None;

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

        private void checkEmbedMissing_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatEmbedMissing = true;
                Settings.Default.ChatReplaceEmbeds = false;
                Settings.Default.Save();
                checkReplaceEmbeds.IsChecked = false;
                checkBttvEmbed.IsEnabled = true;
                checkFfzEmbed.IsEnabled = true;
                checkStvEmbed.IsEnabled = true;
            }
        }

        private void checkEmbedMissing_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatEmbedMissing = false;
                Settings.Default.Save();
                checkBttvEmbed.IsEnabled = false;
                checkFfzEmbed.IsEnabled = false;
                checkStvEmbed.IsEnabled = false;
            }
        }

        private void checkReplaceEmbeds_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatEmbedMissing = false;
                Settings.Default.ChatReplaceEmbeds = true;
                Settings.Default.Save();
                checkEmbedMissing.IsChecked = false;
                checkBttvEmbed.IsEnabled = true;
                checkFfzEmbed.IsEnabled = true;
                checkStvEmbed.IsEnabled = true;
            }
        }

        private void checkReplaceEmbeds_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.ChatReplaceEmbeds = false;
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

        private async void SplitBtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (((HandyControl.Controls.SplitButton)sender).IsDropDownOpen)
            {
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                FileName = FilenameService.GetFilename(Settings.Default.TemplateChat, textTitle.Text,
                    ChatJsonInfo.video.id ?? ChatJsonInfo.comments.FirstOrDefault()?.content_id ?? "-1", VideoCreatedAt, textStreamer.Text, StreamerId,
                    checkStart.IsChecked == true ? new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value) : TimeSpan.FromSeconds(double.IsNegative(ChatJsonInfo.video.start) ? 0.0 : ChatJsonInfo.video.start),
                    checkEnd.IsChecked == true ? new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value) : VideoLength,
                    VideoLength, ViewCount, Game, ClipperName, ClipperId)
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
                ChatUpdateOptions updateOptions = GetOptions(saveFileDialog.FileName);

                var updateProgress = new WpfTaskProgress((LogLevel)Settings.Default.LogLevels, SetPercent, SetStatus, AppendLog);
                var currentUpdate = new ChatUpdater(updateOptions, updateProgress);
                try
                {
                    await currentUpdate.ParseJsonAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    AppendLog(Translations.Strings.ErrorLog + ex.Message);
                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(Application.Current.MainWindow!, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return;
                }

                btnBrowse.IsEnabled = false;
                SetEnabled(false);

                SetImage("Images/ppOverheat.gif", true);
                statusMessage.Text = Translations.Strings.StatusUpdating;
                _cancellationTokenSource = new CancellationTokenSource();
                UpdateActionButtons(true);

                try
                {
                    await currentUpdate.UpdateAsync(_cancellationTokenSource.Token);
                    textJson.Text = "";
                    updateProgress.SetStatus(Translations.Strings.StatusDone);
                    SetImage("Images/ppHop.gif", true);
                }
                catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException && _cancellationTokenSource.IsCancellationRequested)
                {
                    updateProgress.SetStatus(Translations.Strings.StatusCanceled);
                    SetImage("Images/ppHop.gif", true);
                }
                catch (Exception ex)
                {
                    updateProgress.SetStatus(Translations.Strings.StatusError);
                    SetImage("Images/peepoSad.png", false);
                    AppendLog(Translations.Strings.ErrorLog + ex.Message);
                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(Application.Current.MainWindow!, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                btnBrowse.IsEnabled = true;
                updateProgress.ReportProgress(0);
                _cancellationTokenSource.Dispose();
                UpdateActionButtons(false);

                GC.Collect();
            }
            catch (Exception ex)
            {
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors)
                {
                    MessageBox.Show(Application.Current.MainWindow!, ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
                textTrim.Margin = new Thickness(0, 10, 0, 0);

                Settings.Default.ChatDownloadType = (int)ChatFormat.Text;
                Settings.Default.Save();
            }
        }

        private void checkStart_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledTrimStart(checkStart.IsChecked.GetValueOrDefault());
        }

        private void checkEnd_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledTrimEnd(checkEnd.IsChecked.GetValueOrDefault());
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