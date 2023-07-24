using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Services;
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
        public TimeSpan vodLength;
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
            int videoId = ValidateUrl(textUrl.Text.Trim());
            if (videoId <= 0)
            {
                MessageBox.Show(Translations.Strings.InvalidVideoLinkIdMessage.Replace(@"\n", Environment.NewLine), Translations.Strings.InvalidVideoLinkId, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            currentVideoId = videoId;
            try
            {
                Task<GqlVideoResponse> taskVideoInfo = TwitchHelper.GetVideoInfo(videoId);
                Task<GqlVideoTokenResponse> taskAccessToken = TwitchHelper.GetVideoToken(videoId, TextOauth.Text);
                await Task.WhenAll(taskVideoInfo, taskAccessToken);
                Task<string[]> taskPlaylist = TwitchHelper.GetVideoPlaylist(videoId, taskAccessToken.Result.data.videoPlaybackAccessToken.value, taskAccessToken.Result.data.videoPlaybackAccessToken.signature);
                try
                {
                    string thumbUrl = taskVideoInfo.Result.data.video.thumbnailURLs.FirstOrDefault();
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

                comboQuality.Items.Clear();
                videoQualties.Clear();
                string[] playlist = await taskPlaylist;
                if (playlist[0].Contains("vod_manifest_restricted"))
                {
                    throw new NullReferenceException(Translations.Strings.InsufficientAccessMayNeedOauth);
                }

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

                vodLength = TimeSpan.FromSeconds(taskVideoInfo.Result.data.video.lengthSeconds);
                textStreamer.Text = taskVideoInfo.Result.data.video.owner.displayName;
                textTitle.Text = taskVideoInfo.Result.data.video.title;
                var videoCreatedAt = taskVideoInfo.Result.data.video.createdAt;
                textCreatedAt.Text = Settings.Default.UTCVideoTime ? videoCreatedAt.ToString(CultureInfo.CurrentCulture) : videoCreatedAt.ToLocalTime().ToString(CultureInfo.CurrentCulture);
                currentVideoTime = Settings.Default.UTCVideoTime ? videoCreatedAt : videoCreatedAt.ToLocalTime();
                var urlTimeCodeMatch = Regex.Match(textUrl.Text, @"(?<=\?t=)\d+h\d+m\d+s");
                if (urlTimeCodeMatch.Success)
                {
                    var time = TimeSpanExtensions.ParseTimeCode(urlTimeCodeMatch.ValueSpan);
                    checkStart.IsChecked = true;
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
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                MessageBox.Show(Translations.Strings.UnableToGetVideoInfo, Translations.Strings.UnableToGetInfo, MessageBoxButton.OK, MessageBoxImage.Error);
                if (Settings.Default.VerboseErrors)
                {
                    MessageBox.Show(ex.ToString(), Translations.Strings.VerboseErrorOutput, MessageBoxButton.OK, MessageBoxImage.Error);
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
            VideoDownloadOptions options = new VideoDownloadOptions
            {
                DownloadThreads = (int)numDownloadThreads.Value,
                ThrottleKib = Settings.Default.DownloadThrottleEnabled
                    ? Settings.Default.MaximumBandwidthKib
                    : -1,
                Filename = filename ?? Path.Combine(folder, FilenameService.GetFilename(Settings.Default.TemplateVod, textTitle.Text, currentVideoId.ToString(), currentVideoTime, textStreamer.Text,
                    checkStart.IsChecked == true ? new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value) : TimeSpan.Zero,
                    checkEnd.IsChecked == true ? new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value) : vodLength) + ".mp4"),
                Oauth = TextOauth.Text,
                Quality = GetQualityWithoutSize(comboQuality.Text).ToString(),
                Id = currentVideoId,
                CropBeginning = (bool)checkStart.IsChecked,
                CropBeginningTime = (int)(new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value).TotalSeconds),
                CropEnding = (bool)checkEnd.IsChecked,
                CropEndingTime = (int)(new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value).TotalSeconds),
                FfmpegPath = "ffmpeg",
                TempFolder = Settings.Default.TempPath
            };
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
                : vodLength;
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

        private static ReadOnlySpan<char> GetQualityWithoutSize(string qualityWithSize)
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

            const long ONE_KIBIBYTE = 1024;
            const long ONE_MEBIBYTE = 1_048_576;
            const long ONE_GIBIBYTE = 1_073_741_824;

            return sizeInBytes switch
            {
                < 1 => "",
                < ONE_KIBIBYTE => $" - {sizeInBytes}B",
                < ONE_MEBIBYTE => $" - {(float)sizeInBytes / ONE_KIBIBYTE:F1}KiB",
                < ONE_GIBIBYTE => $" - {(float)sizeInBytes / ONE_MEBIBYTE:F1}MiB",
                _ => $" - {(float)sizeInBytes / ONE_GIBIBYTE:F1}GiB",
            };
        }

        private static long EstimateVideoSizeBytes(int bandwidth, TimeSpan startTime, TimeSpan endTime)
        {
            if (bandwidth < 1)
                return 0;
            if (endTime < startTime)
                return 0;

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
            {
                ImageBehavior.SetAnimatedSource(statusImage, image);
            }
            else
            {
                ImageBehavior.SetAnimatedSource(statusImage, null);
                statusImage.Source = image;
            }
        }

        private static int ValidateUrl(string text)
        {
            var vodIdMatch = Regex.Match(text, @"(?<=^|twitch\.tv\/videos\/)\d+(?=$|\?)");
            if (vodIdMatch.Success && int.TryParse(vodIdMatch.ValueSpan, out var vodId))
            {
                return vodId;
            }

            return -1;
        }

        public bool ValidateInputs()
        {
            if ((bool)checkStart.IsChecked)
            {
                var beginTime = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                if (beginTime.TotalSeconds >= vodLength.TotalSeconds)
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
            TextOauth.Text = Settings.Default.OAuth;
        }

        private void numDownloadThreads_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            if (this.IsInitialized && numDownloadThreads.IsEnabled)
            {
                Settings.Default.VodDownloadThreads = (int)numDownloadThreads.Value;
                Settings.Default.Save();
            }
        }

        private void TextOauth_TextChanged(object sender, RoutedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.OAuth = TextOauth.Text;
                Settings.Default.Save();
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
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.InvalidCropInputs);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "MP4 Files | *.mp4",
                FileName = FilenameService.GetFilename(Settings.Default.TemplateVod, textTitle.Text, currentVideoId.ToString(), currentVideoTime, textStreamer.Text,
                    checkStart.IsChecked == true ? new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value) : TimeSpan.Zero,
                    checkEnd.IsChecked == true ? new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value) : vodLength)
            };
            if (saveFileDialog.ShowDialog() == false)
            {
                return;
            }

            SetEnabled(false);
            btnGetInfo.IsEnabled = false;

            VideoDownloadOptions options = GetOptions(saveFileDialog.FileName, null);

            Progress<ProgressReport> downloadProgress = new Progress<ProgressReport>(OnProgressChanged);
            VideoDownloader currentDownload = new VideoDownloader(options, downloadProgress);
            _cancellationTokenSource = new CancellationTokenSource();

            SetImage("Images/ppOverheat.gif", true);
            statusMessage.Text = Translations.Strings.StatusDownloading;
            UpdateActionButtons(true);
            try
            {
                await currentDownload.DownloadAsync(_cancellationTokenSource.Token);
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

            currentDownload = null;
            GC.Collect();
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
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.InvalidCropInputs);
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