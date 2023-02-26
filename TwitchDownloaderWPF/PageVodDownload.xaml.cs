﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using TwitchDownloader;
using TwitchDownloader.Properties;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects.Gql;
using WpfAnimatedGif;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageVodDownload.xaml
    /// </summary>
    public partial class PageVodDownload : Page
    {
        public Dictionary<string, (string url, int bandwidth)> videoQualties = new();
        public int currentVideoId;
        public DateTime currentVideoTime;
        private CancellationTokenSource _cancellationTokenSource;

        public PageVodDownload()
        {
            InitializeComponent();
        }

        private void SetEnabled(bool isEnabled)
        {
            comboQuality.IsEnabled = isEnabled;
            checkStart.IsEnabled = isEnabled;
            checkEnd.IsEnabled = isEnabled;
            SplitBtnDownload.IsEnabled = isEnabled;
            MenuItemEnqueue.IsEnabled = isEnabled;
            SetEnabledCropStart(isEnabled & (bool)checkStart.IsChecked);
            SetEnabledCropEnd(isEnabled & (bool)checkEnd.IsChecked);
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
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0018:Inline variable declaration")]
        private async void btnGetInfo_Click(object sender, RoutedEventArgs e)
        {
            int videoId = ValidateUrl(textUrl.Text);
            if (videoId <= 0)
            {
                MessageBox.Show("Please enter a valid video ID/URL" + Environment.NewLine + "Examples:" + Environment.NewLine + "https://www.twitch.tv/videos/470741744" + Environment.NewLine + "470741744", "Invalid Video ID/URL", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            currentVideoId = videoId;
            try
            {
                Task<GqlVideoResponse> taskVideoInfo = TwitchHelper.GetVideoInfo(videoId);
                Task<GqlVideoTokenResponse> taskAccessToken = TwitchHelper.GetVideoToken(videoId, passwordOauth.Password);
                await Task.WhenAll(taskVideoInfo, taskAccessToken);
                Task<string[]> taskPlaylist = TwitchHelper.GetVideoPlaylist(videoId, taskAccessToken.Result.data.videoPlaybackAccessToken.value, taskAccessToken.Result.data.videoPlaybackAccessToken.signature);
                try
                {
                    string thumbUrl = taskVideoInfo.Result.data.video.thumbnailURLs.FirstOrDefault();
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

                comboQuality.Items.Clear();
                videoQualties.Clear();
                string[] playlist = await taskPlaylist;
                for (int i = 0; i < playlist.Length; i++)
                {
                    if (playlist[i].Contains("#EXT-X-MEDIA"))
                    {
                        string lastPart = playlist[i].Substring(playlist[i].IndexOf("NAME=\"") + 6);
                        string stringQuality = lastPart.Substring(0, lastPart.IndexOf('"'));

                        var bandwidthStartIndex = playlist[i + 1].IndexOf("BANDWIDTH=") + 10;
                        var bandwidthEndIndex = playlist[i + 1].IndexOf(',') - bandwidthStartIndex;
                        int bandwidth = 0; // Cannot be inlined if we want default value of 0
                        int.TryParse(playlist[i + 1].Substring(bandwidthStartIndex, bandwidthEndIndex), out bandwidth);

                        if (!videoQualties.ContainsKey(stringQuality))
                        {
                            videoQualties.Add(stringQuality, (playlist[i + 2], bandwidth));
                            comboQuality.Items.Add(stringQuality);
                        }
                    }
                }
                comboQuality.SelectedIndex = 0;

                TimeSpan vodLength = TimeSpan.FromSeconds(taskVideoInfo.Result.data.video.lengthSeconds);
                textStreamer.Text = taskVideoInfo.Result.data.video.owner.displayName;
                textTitle.Text = taskVideoInfo.Result.data.video.title;
                textCreatedAt.Text = taskVideoInfo.Result.data.video.createdAt.ToString();
                currentVideoTime = taskVideoInfo.Result.data.video.createdAt.ToLocalTime();
                Regex urlTimecodeRegex = new Regex(@"\?t=(\d?\dh)(\d?\dm)(\d?\ds)"); // ?t=##h##m##s
                Match urlTimecodeMatch = urlTimecodeRegex.Match(textUrl.Text);
                if (urlTimecodeMatch.Success)
                {
                    checkStart.IsChecked = true;
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

                UpdateVideoSizeEstimates();

                SetEnabled(true);
            }
            catch (Exception ex)
            {
                btnGetInfo.IsEnabled = true;
                AppendLog("ERROR: " + ex.Message);
                MessageBox.Show("Unable to get the video information." + Environment.NewLine + "Please make sure the video ID is correct and try again.", "Unable To Fetch Video Info", MessageBoxButton.OK, MessageBoxImage.Error);
                if (Settings.Default.VerboseErrors)
                {
                    MessageBox.Show(ex.ToString(), "Verbose error output", MessageBoxButton.OK, MessageBoxImage.Error);
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

        public VideoDownloadOptions GetOptions(string filename, string folder)
        {
            VideoDownloadOptions options = new VideoDownloadOptions();
            options.DownloadThreads = (int)numDownloadThreads.Value;
            //If filename is provided, use that if not use template for queue system
            if (filename != null)
            {
                options.Filename = filename;
            }
            else
            {
                options.Filename = Path.Combine(folder, MainWindow.GetFilename(Settings.Default.TemplateVod, textTitle.Text, currentVideoId.ToString(), currentVideoTime, textStreamer.Text) + ".mp4");
            }
            options.Oauth = passwordOauth.Password;
            options.Quality = GetQualityWithoutSize(comboQuality.Text).ToString();
            options.Id = currentVideoId;
            options.CropBeginning = (bool)checkStart.IsChecked;
            options.CropBeginningTime = (int)(new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value).TotalSeconds);
            options.CropEnding = (bool)checkEnd.IsChecked;
            options.CropEndingTime = (int)(new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value).TotalSeconds);
            options.FfmpegPath = "ffmpeg";
            options.TempFolder = Settings.Default.TempPath;
            return options;
        }

        private void UpdateVideoSizeEstimates()
        {
            int selectedIndex = comboQuality.SelectedIndex;

            var cropStart = checkStart.IsChecked == true
                ? new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value)
                : TimeSpan.Zero;
            var cropEnd = checkEnd.IsChecked == true
                ? new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value)
                : TimeSpan.Parse(labelLength.Text);
            for (int i = 0; i < comboQuality.Items.Count; i++)
            {
                var qualityWithSize = (string)comboQuality.Items[i];
                var quality = GetQualityWithoutSize(qualityWithSize).ToString();
                int bandwidth = videoQualties[quality].bandwidth;

                var newVideoSize = EstimateVideoSize(bandwidth, cropStart, cropEnd);
                comboQuality.Items[i] = $"{quality}{newVideoSize}";
            }

            comboQuality.SelectedIndex = selectedIndex;
        }

        private ReadOnlySpan<char> GetQualityWithoutSize(string qualityWithSize)
        {
            int qualityIndex = qualityWithSize.LastIndexOf(" - ");
            return qualityIndex == -1
                ? qualityWithSize.AsSpan()
                : qualityWithSize.AsSpan(0, qualityIndex);
        }

        // TODO: Move to Core to add support in CLI
        private static string EstimateVideoSize(int bandwidth, TimeSpan startTime, TimeSpan endTime)
        {
            var sizeInBytes = EstimateVideoSizeBytes(bandwidth, startTime, endTime);

            const long ONE_KILOBYTE = 1024;
            const long ONE_MEGABYTE = 1_048_576;
            const long ONE_GIGABYTE = 1_073_741_824;

            return sizeInBytes switch
            {
                < 1 => "",
                < ONE_KILOBYTE => $" - {sizeInBytes}B",
                < ONE_MEGABYTE => $" - {(float)sizeInBytes / ONE_KILOBYTE:F1}KB",
                < ONE_GIGABYTE => $" - {(float)sizeInBytes / ONE_MEGABYTE:F1}MB",
                _ => $" - {(float)sizeInBytes / ONE_GIGABYTE:F1}GB",
            };
        }

        private static long EstimateVideoSizeBytes(int bandwidth, TimeSpan startTime, TimeSpan endTime)
        {
            if (bandwidth == 0)
            {
                return 0;
            }

            var totalTime = endTime - startTime;
            return (long)(bandwidth / 8d * totalTime.TotalSeconds);
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
                ImageBehavior.SetAnimatedSource(statusImage, image);
            else
            {
                ImageBehavior.SetAnimatedSource(statusImage, null);
                statusImage.Source = image;
            }
        }

        private int ValidateUrl(string text)
        {
            if (text.All(Char.IsDigit))
            {
                if (int.TryParse(text, out int number))
                    return number;
                else
                    return -1;
            }
            else if (text.Contains("twitch.tv/videos/"))
            {
                //Extract just the numbers from the URL, also remove query string
                Uri url = new UriBuilder(text).Uri;
                string path = String.Format("{0}{1}{2}{3}", url.Scheme, Uri.SchemeDelimiter, url.Authority, url.AbsolutePath);
                if (int.TryParse(Regex.Match(path, @"\d+").Value, out int number))
                    return number;
                else
                    return -1;
            }
            else
            {
                return -1;
            }
        }

        public bool ValidateInputs()
        {
            if ((bool)checkStart.IsChecked)
            {
                var videoLength = TimeSpan.Parse(labelLength.Text);
                var beginTime = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                if (beginTime.TotalSeconds >= videoLength.TotalSeconds)
                {
                    return false;
                }

                if ((bool)checkEnd.IsChecked)
                {
                    var endTime = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
                    if (endTime.TotalSeconds < beginTime.TotalSeconds)
                    {
                        return false;
                    }
                }
            }

            return true;
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
            SetEnabledCropStart(false);
            SetEnabledCropEnd(false);
            WebRequest.DefaultWebProxy = null;
            numDownloadThreads.Value = Settings.Default.VodDownloadThreads;
            passwordOauth.Password = Settings.Default.OAuth;
        }

        private void numDownloadThreads_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (this.IsInitialized && numDownloadThreads.IsEnabled)
            {
                numDownloadThreads.Value = Math.Clamp((int)numDownloadThreads.Value, 1, 50);
                Settings.Default.VodDownloadThreads = (int)numDownloadThreads.Value;
                Settings.Default.Save();
            }
        }

        private void passwordOauth_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.OAuth = passwordOauth.Password;
                Settings.Default.Save();
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

        private void checkStart_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledCropStart((bool)checkStart.IsChecked);

            UpdateVideoSizeEstimates();
        }

        private void checkEnd_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledCropEnd((bool)checkEnd.IsChecked);

            UpdateVideoSizeEstimates();
        }

        private async void SplitBtnDownloader_Click(object sender, RoutedEventArgs e)
        {
            if (((HandyControl.Controls.SplitButton)sender).IsDropDownOpen)
            {
                return;
            }

            if (!ValidateInputs())
            {
                AppendLog("ERROR: Invalid Crop Inputs");
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "MP4 Files | *.mp4",
                RestoreDirectory = true,
                FileName = MainWindow.GetFilename(Settings.Default.TemplateVod, textTitle.Text, currentVideoId.ToString(), currentVideoTime, textStreamer.Text)
            };
            if (saveFileDialog.ShowDialog() == false)
            {
                return;
            }

            SetEnabled(false);
            btnGetInfo.IsEnabled = false;

            VideoDownloadOptions options = GetOptions(saveFileDialog.FileName, null);

            VideoDownloader currentDownload = new VideoDownloader(options);
            Progress<ProgressReport> downloadProgress = new Progress<ProgressReport>(OnProgressChanged);
            _cancellationTokenSource = new CancellationTokenSource();

            SetImage("Images/ppOverheat.gif", true);
            statusMessage.Text = "Downloading";
            UpdateActionButtons(true);
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

        private void MenuItemEnqueue_Click(object sender, RoutedEventArgs e)
        {
            if (!SplitBtnDownload.IsDropDownOpen)
            {
                return;
            }

            if (ValidateInputs())
            {
                WindowQueueOptions queueOptions = new WindowQueueOptions(this);
                queueOptions.ShowDialog();
            }
            else
            {
                AppendLog("ERROR: Invalid Crop Inputs");
            }
        }

        private void numEndHour_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            UpdateVideoSizeEstimates();
        }

        private void numEndMinute_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            UpdateVideoSizeEstimates();
        }

        private void numEndSecond_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            UpdateVideoSizeEstimates();
        }

        private void numStartHour_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            UpdateVideoSizeEstimates();
        }

        private void numStartMinute_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            UpdateVideoSizeEstimates();
        }

        private void numStartSecond_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            UpdateVideoSizeEstimates();
        }
    }
}