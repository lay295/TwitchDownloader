﻿using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TwitchDownloader;
using TwitchDownloader.Properties;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects.Gql;
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
        public int streamerId;
        public DateTime currentVideoTime;
        private CancellationTokenSource _cancellationTokenSource;

        public PageChatDownload()
        {
            InitializeComponent();
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            SetEnabled(false, false);
            SetEnabledCropStart(false);
            SetEnabledCropEnd(false);
            checkEmbed.IsChecked = Settings.Default.ChatEmbedEmotes;
            checkBttvEmbed.IsChecked = Settings.Default.BTTVEmotes;
            checkFfzEmbed.IsChecked = Settings.Default.FFZEmotes;
            checkStvEmbed.IsChecked = Settings.Default.STVEmotes;
            numChatDownloadConnections.Value = Settings.Default.ChatDownloadThreads;
            _ = (ChatFormat)Settings.Default.ChatDownloadType switch
            {
                ChatFormat.Text => radioText.IsChecked = true,
                ChatFormat.Html => radioHTML.IsChecked = true,
                _ => radioJson.IsChecked = true
            };
        }

        private void SetEnabled(bool isEnabled, bool isClip)
        {
            checkCropStart.IsEnabled = isEnabled & !isClip;
            checkCropEnd.IsEnabled = isEnabled & !isClip;
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

        private async void btnGetInfo_Click(object sender, RoutedEventArgs e)
        {
            string id = ValidateUrl(textUrl.Text.Trim());
            if (id == "")
            {
                MessageBox.Show("Please double check the VOD/Clip link", "Unable to parse input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            btnGetInfo.IsEnabled = false;
            downloadId = id;
            if (id.All(Char.IsDigit))
                downloadType = DownloadType.Video;
            else
                downloadType = DownloadType.Clip;

            try
            {
                if (downloadType == DownloadType.Video)
                {
                    GqlVideoResponse videoInfo = await TwitchHelper.GetVideoInfo(int.Parse(downloadId));

                    try
                    {
                        string thumbUrl = videoInfo.data.video.thumbnailURLs.FirstOrDefault();
                        imgThumbnail.Source = await InfoHelper.GetThumb(thumbUrl);
                    }
                    catch
                    {
                        AppendLog("ERROR: Unable to find thumbnail");
                        var (success, image) = await InfoHelper.TryGetThumb(InfoHelper.THUMBNAIL_MISSING_URL);
                        if (success)
                        {
                            imgThumbnail.Source = image;
                        }
                    }
                    TimeSpan vodLength = TimeSpan.FromSeconds(videoInfo.data.video.lengthSeconds);
                    textTitle.Text = videoInfo.data.video.title;
                    textStreamer.Text = videoInfo.data.video.owner.displayName;
                    textCreatedAt.Text = videoInfo.data.video.createdAt.ToString();
                    currentVideoTime = videoInfo.data.video.createdAt.ToLocalTime();
                    streamerId = int.Parse(videoInfo.data.video.owner.id);
                    Regex urlTimecodeRegex = new Regex(@"\?t=(\d?\dh)(\d?\dm)(\d?\ds)"); // ?t=##h##m##s
                    Match urlTimecodeMatch = urlTimecodeRegex.Match(textUrl.Text);
                    if (urlTimecodeMatch.Success)
                    {
                        checkCropStart.IsChecked = true;
                        numStartHour.Value = int.Parse(urlTimecodeMatch.Groups[1].Value[..urlTimecodeMatch.Groups[1].ToString().IndexOf('h')]);
                        numStartMinute.Value = int.Parse(urlTimecodeMatch.Groups[2].Value[..urlTimecodeMatch.Groups[2].ToString().IndexOf('m')]);
                        numStartSecond.Value = int.Parse(urlTimecodeMatch.Groups[3].Value[..urlTimecodeMatch.Groups[3].ToString().IndexOf('s')]);
                    }
                    else
                    {
                        numStartHour.Value = 0;
                        numStartMinute.Value = 0;
                        numStartSecond.Value = 0;
                    }
                    numStartHour.Maximum = (int)vodLength.TotalHours;

                    numEndHour.Value = (int)vodLength.TotalHours;
                    numEndHour.Maximum = (int)vodLength.TotalHours;
                    numEndMinute.Value = vodLength.Minutes;
                    numEndSecond.Value = vodLength.Seconds;
                    labelLength.Text = vodLength.ToString("c");
                    SetEnabled(true, false);
                }
                else if (downloadType == DownloadType.Clip)
                {
                    string clipId = downloadId;
                    GqlClipResponse clipInfo = await TwitchHelper.GetClipInfo(clipId);

                    try
                    {
                        string thumbUrl = clipInfo.data.clip.thumbnailURL;
                        imgThumbnail.Source = await InfoHelper.GetThumb(thumbUrl);
                    }
                    catch
                    {
                        AppendLog("ERROR: Unable to find thumbnail");
                        var (success, image) = await InfoHelper.TryGetThumb(InfoHelper.THUMBNAIL_MISSING_URL);
                        if (success)
                        {
                            imgThumbnail.Source = image;
                        }
                    }
                    TimeSpan clipLength = TimeSpan.FromSeconds(clipInfo.data.clip.durationSeconds);
                    textStreamer.Text = clipInfo.data.clip.broadcaster.displayName;
                    textCreatedAt.Text = clipInfo.data.clip.createdAt.ToString();
                    currentVideoTime = clipInfo.data.clip.createdAt.ToLocalTime();
                    textTitle.Text = clipInfo.data.clip.title;
                    streamerId = int.Parse(clipInfo.data.clip.broadcaster.id);
                    labelLength.Text = clipLength.ToString("c");
                    SetEnabled(true, true);
                    SetEnabledCropStart(false);
                    SetEnabledCropEnd(false);
                }

                btnGetInfo.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to get Clip/Video information. Please double check your link and try again", "Unable to get info", MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog("ERROR: " + ex.Message);
                btnGetInfo.IsEnabled = true;
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
            var vodClipIdRegex = new Regex(@"(?<=^|(?:clips\.)?twitch\.tv\/(?:videos|\S+\/clip)?\/?)[\w-]+?(?=$|\?)");
            var vodClipIdMatch = vodClipIdRegex.Match(text);
            return vodClipIdMatch.Success
                ? vodClipIdMatch.Value
                : null;
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

            if (radioCompressionNone.IsChecked == true)
                options.Compression = ChatCompression.None;
            else if (radioCompressionGzip.IsChecked == true)
                options.Compression = ChatCompression.Gzip;

            options.EmbedData = (bool)checkEmbed.IsChecked;
            options.BttvEmotes = (bool)checkBttvEmbed.IsChecked;
            options.FfzEmotes = (bool)checkFfzEmbed.IsChecked;
            options.StvEmotes = (bool)checkStvEmbed.IsChecked;
            options.Filename = filename;
            options.ConnectionCount = (int)numChatDownloadConnections.Value;
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
            SettingsPage settings = new SettingsPage();
            settings.ShowDialog();
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void numChatDownloadConnections_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (this.IsInitialized)
            {
                numChatDownloadConnections.Value = Math.Clamp((int)numChatDownloadConnections.Value, 1, 50);
                Settings.Default.ChatDownloadThreads = (int)numChatDownloadConnections.Value;
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
                textCrop.Margin = new Thickness(0, 12, 0, 41);

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

            saveFileDialog.FileName = MainWindow.GetFilename(Settings.Default.TemplateChat, textTitle.Text, downloadId, currentVideoTime, textStreamer.Text);

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ChatDownloadOptions downloadOptions = GetOptions(saveFileDialog.FileName);
                if (downloadType == DownloadType.Video)
                {
                    int startTime = 0;
                    int endTime = 0;

                    if (checkCropStart.IsChecked == true)
                    {
                        downloadOptions.CropBeginning = true;
                        TimeSpan start = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                        startTime = (int)start.TotalSeconds;
                        downloadOptions.CropBeginningTime = startTime;
                    }

                    if (checkCropEnd.IsChecked == true)
                    {
                        downloadOptions.CropEnding = true;
                        TimeSpan end = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
                        endTime = (int)end.TotalSeconds;
                        downloadOptions.CropEndingTime = endTime;
                    }

                    downloadOptions.Id = downloadId;
                }
                else
                {
                    downloadOptions.Id = downloadId;
                }

                if (radioTimestampUTC.IsChecked == true)
                    downloadOptions.TimeFormat = TimestampFormat.Utc;
                else if (radioTimestampRelative.IsChecked == true)
                    downloadOptions.TimeFormat = TimestampFormat.Relative;
                else if (radioTimestampNone.IsChecked == true)
                    downloadOptions.TimeFormat = TimestampFormat.None;

                ChatDownloader currentDownload = new ChatDownloader(downloadOptions);

                btnGetInfo.IsEnabled = false;
                SetEnabled(false, false);

                SetImage("Images/ppOverheat.gif", true);
                statusMessage.Text = "Downloading";
                _cancellationTokenSource = new CancellationTokenSource();
                UpdateActionButtons(true);

                Progress<ProgressReport> downloadProgress = new Progress<ProgressReport>(OnProgressChanged);

                try
                {
                    await currentDownload.DownloadAsync(downloadProgress, _cancellationTokenSource.Token);
                    statusMessage.Text = "Done";
                    SetImage("Images/ppHop.gif", true);
                }
                catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
                {
                    statusMessage.Text = "ERROR";
                    SetImage("Images/peepoSad.png", false);
                    AppendLog("ERROR: " + ex.Message);
                    if (Settings.Default.VerboseErrors)
                    {
                        MessageBox.Show(ex.ToString(), "Verbose error output", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch
                {
                    statusMessage.Text = "Canceled";
                    SetImage("Images/ppHop.gif", true);
                }
                btnGetInfo.IsEnabled = true;
                statusProgressBar.Value = 0;
                _cancellationTokenSource.Dispose();
                UpdateActionButtons(false);

                currentDownload = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            statusMessage.Text = "Canceling";
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException) { }
        }

        private void checkCropStart_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledCropStart((bool)checkCropStart.IsChecked);
        }

        private void checkCropEnd_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledCropEnd((bool)checkCropEnd.IsChecked);
        }


        private void MenuItemEnqueue_Click(object sender, RoutedEventArgs e)
        {
            WindowQueueOptions queueOptions = new WindowQueueOptions(this);
            queueOptions.ShowDialog();
        }
    }
}