using Microsoft.Win32;
using System;
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
using TwitchDownloaderCore;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Models.Interfaces;
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
    /// <summary>
    /// Interaction logic for PageVodDownload.xaml
    /// </summary>
    public partial class PageVodDownload : Page
    {
        public long currentVideoId;
        public DateTime currentVideoTime;
        public TimeSpan vodLength;
        public int viewCount;
        public string game;
        public string streamerId;
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
            RadioTrimSafe.IsEnabled = isEnabled;
            RadioTrimExact.IsEnabled = isEnabled;
            SetEnabledTrimStart(isEnabled & checkStart.IsChecked.GetValueOrDefault());
            SetEnabledTrimEnd(isEnabled & checkEnd.IsChecked.GetValueOrDefault());
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

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private async void btnGetInfo_Click(object sender, RoutedEventArgs e)
        {
            await GetVideoInfo();
        }

        private async Task GetVideoInfo()
        {
            long videoId = ValidateUrl(textUrl.Text.Trim());
            if (videoId <= 0)
            {
                MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.InvalidVideoLinkIdMessage.Replace(@"\n", Environment.NewLine), Translations.Strings.InvalidVideoLinkId, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            currentVideoId = videoId;
            try
            {
                Task<GqlVideoResponse> taskVideoInfo = TwitchHelper.GetVideoInfo(videoId);
                Task<GqlVideoTokenResponse> taskAccessToken = TwitchHelper.GetVideoToken(videoId, TextOauth.Text);
                await Task.WhenAll(taskVideoInfo, taskAccessToken);

                if (taskAccessToken.Result.data.videoPlaybackAccessToken is null)
                {
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
                }

                var thumbUrl = taskVideoInfo.Result.data.video.thumbnailURLs.FirstOrDefault();
                if (!ThumbnailService.TryGetThumb(thumbUrl, out var image))
                {
                    AppendLog(Translations.Strings.ErrorLog + Translations.Strings.UnableToFindThumbnail);
                    _ = ThumbnailService.TryGetThumb(ThumbnailService.THUMBNAIL_MISSING_URL, out image);
                }
                imgThumbnail.Source = image;

                comboQuality.Items.Clear();

                var playlistString = await TwitchHelper.GetVideoPlaylist(videoId, taskAccessToken.Result.data.videoPlaybackAccessToken.value, taskAccessToken.Result.data.videoPlaybackAccessToken.signature);
                if (playlistString.Contains("vod_manifest_restricted") || playlistString.Contains("unauthorized_entitlements"))
                {
                    throw new NullReferenceException(Translations.Strings.InsufficientAccessMayNeedOauth);
                }

                var videoPlaylist = M3U8.Parse(playlistString);
                videoPlaylist.SortStreamsByQuality();
                var qualities = VideoQualities.FromM3U8(videoPlaylist);

                //Add video qualities to combo quality
                foreach (var quality in qualities)
                {
                    var item = new ComboBoxItem { Content = quality.Name, Tag = quality };
                    comboQuality.Items.Add(item);
                }

                comboQuality.SelectedIndex = 0;

                vodLength = TimeSpan.FromSeconds(taskVideoInfo.Result.data.video.lengthSeconds);
                textStreamer.Text = taskVideoInfo.Result.data.video.owner?.displayName ?? Translations.Strings.UnknownUser;
                streamerId = taskVideoInfo.Result.data.video.owner?.id;
                textTitle.Text = taskVideoInfo.Result.data.video.title;
                var videoCreatedAt = taskVideoInfo.Result.data.video.createdAt;
                textCreatedAt.Text = Settings.Default.UTCVideoTime ? videoCreatedAt.ToString(CultureInfo.CurrentCulture) : videoCreatedAt.ToLocalTime().ToString(CultureInfo.CurrentCulture);
                currentVideoTime = Settings.Default.UTCVideoTime ? videoCreatedAt : videoCreatedAt.ToLocalTime();
                var urlTimeCodeMatch = TwitchRegex.UrlTimeCode.Match(textUrl.Text);
                if (urlTimeCodeMatch.Success)
                {
                    var time = UrlTimeCode.Parse(urlTimeCodeMatch.ValueSpan);
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
                viewCount = taskVideoInfo.Result.data.video.viewCount;
                game = taskVideoInfo.Result.data.video.game?.displayName ?? Translations.Strings.UnknownGame;

                UpdateVideoSizeEstimates();

                SetEnabled(true);
            }
            catch (Exception ex)
            {
                btnGetInfo.IsEnabled = true;
                AppendLog(Translations.Strings.ErrorLog + ex.Message);
                MessageBox.Show(Application.Current.MainWindow!, Translations.Strings.UnableToGetVideoInfo, Translations.Strings.UnableToGetInfo, MessageBoxButton.OK, MessageBoxImage.Error);
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

        public VideoDownloadOptions GetOptions(string filename, string folder)
        {
            VideoDownloadOptions options = new VideoDownloadOptions
            {
                DownloadThreads = (int)numDownloadThreads.Value,
                ThrottleKib = Settings.Default.DownloadThrottleEnabled
                    ? Settings.Default.MaximumBandwidthKib
                    : -1,
                Filename = filename ?? Path.Combine(folder, FilenameService.GetFilename(Settings.Default.TemplateVod, textTitle.Text, currentVideoId.ToString(), currentVideoTime, textStreamer.Text, streamerId,
                    checkStart.IsChecked == true ? new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value) : TimeSpan.Zero,
                    checkEnd.IsChecked == true ? new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value) : vodLength,
                    vodLength, viewCount, game) + FilenameService.GuessVodFileExtension(((ComboBoxItem)comboQuality.SelectedItem).Tag.ToString())),
                Oauth = TextOauth.Text,
                Quality = ((ComboBoxItem)comboQuality.SelectedItem).Tag.ToString(),
                Id = currentVideoId,
                TrimBeginning = checkStart.IsChecked.GetValueOrDefault(),
                TrimBeginningTime = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value),
                TrimEnding = checkEnd.IsChecked.GetValueOrDefault(),
                TrimEndingTime = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value),
                FfmpegPath = "ffmpeg",
                TempFolder = Settings.Default.TempPath
            };

            if (RadioTrimSafe.IsChecked == true)
                options.TrimMode = VideoTrimMode.Safe;
            else if (RadioTrimExact.IsChecked == true)
                options.TrimMode = VideoTrimMode.Exact;

            return options;
        }

        private void UpdateVideoSizeEstimates()
        {
            int selectedIndex = comboQuality.SelectedIndex;

            var trimStart = checkStart.IsChecked == true
                ? new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value)
                : TimeSpan.Zero;
            var trimEnd = checkEnd.IsChecked == true
                ? new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value)
                : vodLength;

            foreach (var item in comboQuality.Items.Cast<ComboBoxItem>())
            {
                var quality = (IVideoQuality<M3U8.Stream>)item.Tag;
                var bandwidth = quality.Item.StreamInfo.Bandwidth;

                var sizeInBytes = VideoSizeEstimator.EstimateVideoSize(bandwidth, trimStart, trimEnd);
                if (sizeInBytes == 0)
                {
                    item.Content = quality.Name;
                }
                else
                {
                    var newVideoSize = VideoSizeEstimator.StringifyByteCount(sizeInBytes);
                    item.Content = $"{quality.Name} - {newVideoSize}";
                }
            }

            comboQuality.SelectedIndex = selectedIndex;
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

        private static long ValidateUrl(string text)
        {
            var vodIdMatch = IdParse.MatchVideoId(text);
            if (vodIdMatch is {Success: true} && long.TryParse(vodIdMatch.ValueSpan, out var vodId))
            {
                return vodId;
            }

            return -1;
        }

        public bool ValidateInputs()
        {
            if (checkStart.IsChecked.GetValueOrDefault())
            {
                var beginTime = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                if (vodLength > TimeSpan.Zero && beginTime.TotalSeconds >= vodLength.TotalSeconds)
                {
                    return false;
                }

                if (checkEnd.IsChecked.GetValueOrDefault())
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

        private void Page_Initialized(object sender, EventArgs e)
        {
            SetEnabled(false);
            SetEnabledTrimStart(false);
            SetEnabledTrimEnd(false);
            WebRequest.DefaultWebProxy = null;
            numDownloadThreads.Value = Settings.Default.VodDownloadThreads;
            TextOauth.Text = Settings.Default.OAuth;
            _ = (VideoTrimMode)Settings.Default.VodTrimMode switch
            {
                VideoTrimMode.Exact => RadioTrimExact.IsChecked = true,
                _ => RadioTrimSafe.IsChecked = true,
            };
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

        private void checkStart_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledTrimStart(checkStart.IsChecked.GetValueOrDefault());

            UpdateVideoSizeEstimates();
        }

        private void checkEnd_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            SetEnabledTrimEnd(checkEnd.IsChecked.GetValueOrDefault());

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
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.InvalidTrimInputs);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = comboQuality.Text.Contains("Audio", StringComparison.OrdinalIgnoreCase) ? "M4A Files | *.m4a" : "MP4 Files | *.mp4",
                FileName = FilenameService.GetFilename(Settings.Default.TemplateVod, textTitle.Text, currentVideoId.ToString(), currentVideoTime, textStreamer.Text, streamerId,
                    checkStart.IsChecked == true ? new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value) : TimeSpan.Zero,
                    checkEnd.IsChecked == true ? new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value) : vodLength,
                    vodLength, viewCount, game) + FilenameService.GuessVodFileExtension(comboQuality.Text)
            };
            if (saveFileDialog.ShowDialog() == false)
            {
                return;
            }

            SetEnabled(false);
            btnGetInfo.IsEnabled = false;

            VideoDownloadOptions options = GetOptions(saveFileDialog.FileName, null);
            options.CacheCleanerCallback = HandleCacheCleanerCallback;

            var downloadProgress = new WpfTaskProgress((LogLevel)Settings.Default.LogLevels, SetPercent, SetStatus, AppendLog);
            VideoDownloader currentDownload = new VideoDownloader(options, downloadProgress);
            _cancellationTokenSource = new CancellationTokenSource();

            SetImage("Images/ppOverheat.gif", true);
            statusMessage.Text = Translations.Strings.StatusDownloading;
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

        private DirectoryInfo[] HandleCacheCleanerCallback(DirectoryInfo[] directories)
        {
            return Dispatcher.Invoke(() =>
            {
                var window = new WindowOldVideoCacheManager(directories)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.ShowDialog();

                return window.GetItemsToDelete();
            });
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

        private void MenuItemEnqueue_Click(object sender, RoutedEventArgs e)
        {
            if (!SplitBtnDownload.IsDropDownOpen)
            {
                return;
            }

            if (ValidateInputs())
            {
                var queueOptions = new WindowQueueOptions(this)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                queueOptions.ShowDialog();
            }
            else
            {
                AppendLog(Translations.Strings.ErrorLog + Translations.Strings.InvalidTrimInputs);
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

        private async void TextUrl_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await GetVideoInfo();
                e.Handled = true;
            }
        }

        private void RadioTrimSafe_OnCheckedStateChanged(object sender, RoutedEventArgs e)
        {
            if (IsInitialized)
            {
                Settings.Default.VodTrimMode = (int)VideoTrimMode.Safe;
                Settings.Default.Save();
            }
        }

        private void RadioTrimExact_OnCheckedStateChanged(object sender, RoutedEventArgs e)
        {
            if (IsInitialized)
            {
                Settings.Default.VodTrimMode = (int)VideoTrimMode.Exact;
                Settings.Default.Save();
            }
        }
    }
}