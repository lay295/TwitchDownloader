﻿using Microsoft.Win32;
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
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
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
        public DateTime VideoCreatedAt;
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
            SetEnabled(true);

            if (Path.GetExtension(InputFile)!.ToLower() is not ".json" and not ".gz")
            {
                return;
            }

            ChatJsonInfo = await ChatJson.DeserializeAsync(InputFile, true, false, CancellationToken.None);
            ChatJsonInfo.comments.RemoveRange(1, ChatJsonInfo.comments.Count - 2);
            GC.Collect();
            textStreamer.Text = ChatJsonInfo.streamer.name;
            var videoCreatedAt = ChatJsonInfo.video.created_at;
            textCreatedAt.Text = Settings.Default.UTCVideoTime ? videoCreatedAt.ToString(CultureInfo.CurrentCulture) : videoCreatedAt.ToLocalTime().ToString(CultureInfo.CurrentCulture);
            VideoCreatedAt = Settings.Default.UTCVideoTime ? videoCreatedAt : videoCreatedAt.ToLocalTime();
            textTitle.Text = ChatJsonInfo.video.title ?? Translations.Strings.Unknown;

            TimeSpan chatStart = TimeSpan.FromSeconds(ChatJsonInfo.video.start);
            numStartHour.Value = (int)chatStart.TotalHours;
            numStartMinute.Value = chatStart.Minutes;
            numStartSecond.Value = chatStart.Seconds;

            TimeSpan chatEnd = TimeSpan.FromSeconds(ChatJsonInfo.video.end);
            numEndHour.Value = (int)chatEnd.TotalHours;
            numEndMinute.Value = chatEnd.Minutes;
            numEndSecond.Value = chatEnd.Seconds;

            TimeSpan videoLength = TimeSpan.FromSeconds(double.IsNegative(ChatJsonInfo.video.length) ? 0.0 : ChatJsonInfo.video.length);
            labelLength.Text = videoLength.Seconds > 0
                ? videoLength.ToString("c")
                : Translations.Strings.Unknown;

            VideoId = ChatJsonInfo.video.id ?? ChatJsonInfo.comments.FirstOrDefault()?.content_id ?? "-1";

            if (VideoId.All(char.IsDigit))
            {
                GqlVideoResponse videoInfo = await TwitchHelper.GetVideoInfo(int.Parse(VideoId));
                if (videoInfo.data.video == null)
                {
                    AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail + ": " + Translations.Strings.VodExpiredOrIdCorrupt);
                    var (success, image) = await ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL);
                    if (success)
                    {
                        imgThumbnail.Source = image;
                    }
                    numStartHour.Maximum = 48;
                    numEndHour.Maximum = 48;
                }
                else
                {
                    videoLength = TimeSpan.FromSeconds(videoInfo.data.video.lengthSeconds);
                    labelLength.Text = videoLength.ToString("c");
                    numStartHour.Maximum = (int)videoLength.TotalHours;
                    numEndHour.Maximum = (int)videoLength.TotalHours;

                    try
                    {
                        string thumbUrl = videoInfo.data.video.thumbnailURLs.FirstOrDefault();
                        imgThumbnail.Source = await ThumbnailService.GetThumb(thumbUrl);
                    }
                    catch
                    {
                        AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail);
                        var (success, image) = await ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL);
                        if (success)
                        {
                            imgThumbnail.Source = image;
                        }
                    }
                }
            }
            else
            {
                numStartHour.Maximum = 0;
                numEndHour.Maximum = 0;
                GqlClipResponse videoInfo = await TwitchHelper.GetClipInfo(VideoId);
                if (videoInfo.data.clip.video == null)
                {
                    AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail + ": " + Translations.Strings.VodExpiredOrIdCorrupt);
                    var (success, image) = await ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL);
                    if (success)
                    {
                        imgThumbnail.Source = image;
                    }
                }
                else
                {
                    videoLength = TimeSpan.FromSeconds(videoInfo.data.clip.durationSeconds);
                    labelLength.Text = videoLength.ToString("c");

                    try
                    {
                        string thumbUrl = videoInfo.data.clip.thumbnailURL;
                        imgThumbnail.Source = await ThumbnailService.GetThumb(thumbUrl);
                    }
                    catch
                    {
                        AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail);
                        var (success, image) = await ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL);
                        if (success)
                        {
                            imgThumbnail.Source = image;
                        }
                    }
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
            SetEnabledCropStart(false);
            SetEnabledCropEnd(false);
            checkEmbedMissing.IsChecked = Settings.Default.ChatEmbedMissing;
            checkReplaceEmbeds.IsChecked = Settings.Default.ChatReplaceEmbeds;
            checkBttvEmbed.IsChecked = Settings.Default.BTTVEmotes;
            checkFfzEmbed.IsChecked = Settings.Default.FFZEmotes;
            checkStvEmbed.IsChecked = Settings.Default.STVEmotes;
            _ = (ChatFormat)Settings.Default.ChatDownloadType switch
            {
                ChatFormat.Text => radioText.IsChecked = true,
                ChatFormat.Html => radioHTML.IsChecked = true,
                _ => radioJson.IsChecked = true
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

        private void SetEnabledCropStart(bool isEnabled)
        {
            numStartHour.IsEnabled = isEnabled;
            numStartMinute.IsEnabled = isEnabled;
            numStartSecond.IsEnabled = isEnabled;
        }

        private void SetEnabledCropEnd(bool isEnabled)
        {
            numEndHour.IsEnabled = isEnabled;
            numEndMinute.IsEnabled = isEnabled;
            numEndSecond.IsEnabled = isEnabled;
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
                EmbedMissing = (bool)checkEmbedMissing.IsChecked,
                ReplaceEmbeds = (bool)checkReplaceEmbeds.IsChecked,
                BttvEmotes = (bool)checkBttvEmbed.IsChecked,
                FfzEmotes = (bool)checkFfzEmbed.IsChecked,
                StvEmotes = (bool)checkStvEmbed.IsChecked,
                InputFile = textJson.Text,
                OutputFile = outputFile,
                CropBeginningTime = -1,
                CropEndingTime = -1
            };

            if ((bool)radioJson.IsChecked)
                options.OutputFormat = ChatFormat.Json;
            else if ((bool)radioHTML.IsChecked)
                options.OutputFormat = ChatFormat.Html;
            else if ((bool)radioText.IsChecked)
                options.OutputFormat = ChatFormat.Text;

            if (radioCompressionNone.IsChecked == true)
                options.Compression = ChatCompression.None;
            else if (radioCompressionGzip.IsChecked == true)
                options.Compression = ChatCompression.Gzip;

            if (checkStart.IsChecked == true)
            {
                options.CropBeginning = true;
                TimeSpan start = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                options.CropBeginningTime = (int)Math.Round(start.TotalSeconds);
            }
            if (checkEnd.IsChecked == true)
            {
                options.CropEnding = true;
                TimeSpan end = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
                options.CropEndingTime = (int)Math.Round(end.TotalSeconds);
            }

            if ((bool)radioTimestampUTC.IsChecked)
                options.TextTimestampFormat = TimestampFormat.Utc;
            else if ((bool)radioTimestampRelative.IsChecked)
                options.TextTimestampFormat = TimestampFormat.Relative;
            else if ((bool)radioTimestampNone.IsChecked)
                options.TextTimestampFormat = TimestampFormat.None;

            return options;
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
            WindowSettings settings = new WindowSettings();
            settings.ShowDialog();
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
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

            SaveFileDialog saveFileDialog = new SaveFileDialog();

            if (radioJson.IsChecked == true)
            {
                if (radioCompressionNone.IsChecked == true)
                    saveFileDialog.Filter = "JSON Files | *.json";
                else if (radioCompressionGzip.IsChecked == true)
                    saveFileDialog.Filter = "GZip JSON Files | *.json.gz";
            }
            else if (radioHTML.IsChecked == true)
                saveFileDialog.Filter = "HTML Files | *.html;*.htm";
            else if (radioText.IsChecked == true)
                saveFileDialog.Filter = "TXT Files | *.txt";

            saveFileDialog.FileName = MainWindow.GetFilename(Settings.Default.TemplateChat, textTitle.Text, ChatJsonInfo.video.id ?? ChatJsonInfo.comments.FirstOrDefault()?.content_id ?? "-1", VideoCreatedAt, textStreamer.Text);

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ChatUpdateOptions updateOptions = GetOptions(saveFileDialog.FileName);

                ChatUpdater currentUpdate = new ChatUpdater(updateOptions);
                try
                {
                    await currentUpdate.ParseJsonAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    AppendLog(Translations.Strings.ErrorLog + ex.Message);
                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    return;
                }

                btnBrowse.IsEnabled = false;
                SetEnabled(false);

                SetImage("Images/ppOverheat.gif", true);
                statusMessage.Text = Translations.Strings.StatusUpdating;
                _cancellationTokenSource = new CancellationTokenSource();
                UpdateActionButtons(true);

                Progress<ProgressReport> updateProgress = new Progress<ProgressReport>(OnProgressChanged);

                try
                {
                    await currentUpdate.UpdateAsync(updateProgress, _cancellationTokenSource.Token);
                    await Task.Delay(300); // we need to wait a bit incase the "writing to output file" report comes late
                    textJson.Text = "";
                    statusMessage.Text = Translations.Strings.StatusDone;
                    SetImage("Images/ppHop.gif", true);
                }
                catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
                {
                    statusMessage.Text = Translations.Strings.StatusError;
                    SetImage("Images/peepoSad.png", false);
                    AppendLog(Translations.Strings.ErrorLog + ex.Message);
                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch
                {
                    statusMessage.Text = Translations.Strings.StatusCanceled;
                    SetImage("Images/ppHop.gif", true);
                }
                btnBrowse.IsEnabled = true;
                statusProgressBar.Value = 0;
                _cancellationTokenSource.Dispose();
                UpdateActionButtons(false);

                currentUpdate = null;
                GC.Collect();
            }
            catch (Exception ex)
            {
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                if (Settings.Default.VerboseErrors)
                {
                    MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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
                textCrop.Margin = new Thickness(0, 12, 0, 36);

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
                textCrop.Margin = new Thickness(0, 17, 0, 36);

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
                textCrop.Margin = new Thickness(0, 12, 0, 0);

                Settings.Default.ChatDownloadType = (int)ChatFormat.Text;
                Settings.Default.Save();
            }
        }

        private void checkStart_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledCropStart((bool)checkStart.IsChecked);
        }

        private void checkEnd_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledCropEnd((bool)checkEnd.IsChecked);
        }

        private void MenuItemEnqueue_Click(object sender, RoutedEventArgs e)
        {
            WindowQueueOptions queueOptions = new WindowQueueOptions(this);
            queueOptions.ShowDialog();
        }
    }
}