using Microsoft.Win32;
using Newtonsoft.Json.Linq;
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
        public Dictionary<string, string> videoQualties = new Dictionary<string, string>();
        public int currentVideoId;
        public DateTime currentVideoTime;

        public PageVodDownload()
        {
            InitializeComponent();
        }

        private void SetEnabled(bool isEnabled)
        {
            comboQuality.IsEnabled = isEnabled;
            checkStart.IsEnabled = isEnabled;
            checkEnd.IsEnabled = isEnabled;
            btnDownload.IsEnabled = isEnabled;
            btnQueue.IsEnabled = isEnabled;
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

        private async void btnGetInfo_Click(object sender, RoutedEventArgs e)
        {
            int videoId = ValidateUrl(textUrl.Text);
            if (videoId > 0)
            {
                currentVideoId = videoId;
                try
                {
                    Task<GqlVideoResponse> taskVideoInfo = TwitchHelper.GetVideoInfo(videoId);
                    Task<GqlVideoTokenResponse> taskAccessToken = TwitchHelper.GetVideoToken(videoId, textOauth.Text);
                    await Task.WhenAll(taskVideoInfo, taskAccessToken);
                    string thumbUrl = taskVideoInfo.Result.data.video.thumbnailURLs.FirstOrDefault();
                    Task<BitmapImage> thumbImage = InfoHelper.GetThumb(thumbUrl);
                    Task<string[]> taskPlaylist = TwitchHelper.GetVideoPlaylist(videoId, taskAccessToken.Result.data.videoPlaybackAccessToken.value, taskAccessToken.Result.data.videoPlaybackAccessToken.signature);
                    await taskPlaylist;
                    try
                    {
                        await thumbImage;
                    }
                    catch
                    {
                        AppendLog("ERROR: Unable to find thumbnail");
                    }

                    comboQuality.Items.Clear();
                    videoQualties.Clear();
                    string[] playlist = taskPlaylist.Result;
                    for (int i = 0; i < playlist.Length; i++)
                    {
                        if (playlist[i].Contains("#EXT-X-MEDIA"))
                        {
                            string lastPart = playlist[i].Substring(playlist[i].IndexOf("NAME=\"") + 6);
                            string stringQuality = lastPart.Substring(0, lastPart.IndexOf("\""));

                            if (!videoQualties.ContainsKey(stringQuality))
                            {
                                videoQualties.Add(stringQuality, playlist[i + 2]);
                                comboQuality.Items.Add(stringQuality);
                            }
                        }
                    }
                    comboQuality.SelectedIndex = 0;

                    if (!thumbImage.IsFaulted)
                        imgThumbnail.Source = thumbImage.Result;
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
                    numEndHour.Value = (int)vodLength.TotalHours;
                    numEndMinute.Value = vodLength.Minutes;
                    numEndSecond.Value = vodLength.Seconds;
                    labelLength.Text = string.Format("{0:00}:{1:00}:{2:00}", (int)vodLength.TotalHours, vodLength.Minutes, vodLength.Seconds);

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
            else
            {
                MessageBox.Show("Please enter a valid video ID/URL" + Environment.NewLine + "Examples:" + Environment.NewLine + "https://www.twitch.tv/videos/470741744" + Environment.NewLine + "470741744", "Invalid Video ID/URL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            options.Oauth = textOauth.Text;
            options.Quality = comboQuality.Text;
            options.Id = currentVideoId;
            options.CropBeginning = (bool)checkStart.IsChecked;
            options.CropBeginningTime = (int)(new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value).TotalSeconds);
            options.CropEnding = (bool)checkEnd.IsChecked;
            options.CropEndingTime = (int)(new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value).TotalSeconds);
            options.FfmpegPath = "ffmpeg";
            options.TempFolder = Settings.Default.TempPath;
            return options;
        }

        private void OnProgressChanged(ProgressReport progress)
        {
            switch (progress.ReportType)
            {
                case ReportType.Percent:
                    statusProgressBar.Value = (int)progress.Data;
                    break;
                case ReportType.Status or ReportType.StatusInfo:
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

        public bool ValidateInput()
        {
            string fixedString = FormatString(labelLength.Text.ToString(CultureInfo.InvariantCulture));
            TimeSpan videoLength = TimeSpan.Parse(fixedString);
            TimeSpan beginTime = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
            TimeSpan endTime = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);

            if ((int)numDownloadThreads.Value < 1)
                numDownloadThreads.Value = 1;

            if ((bool)checkStart.IsChecked)
            {
                if (beginTime.TotalSeconds >= videoLength.TotalSeconds || beginTime.TotalSeconds < 0)
                    return false;

                if ((bool)checkEnd.IsChecked)
                {
                    if (endTime.TotalSeconds - beginTime.TotalSeconds < 0)
                        return false;
                }
            }

            return true;
        }

        private string FormatString(string oldString)
        {
            List<int> returnParts = new List<int>();
            List<string> stringParts = new List<string>(oldString.Split(':'));

            int hours = Int32.Parse(stringParts[0]);
            if (hours > 23)
            {
                returnParts.Add(hours / 24);
                returnParts.Add(hours % 24);
                returnParts.Add(Int32.Parse(stringParts[1]));
                returnParts.Add(Int32.Parse(stringParts[2]));

                return String.Join(":", returnParts.ToArray());
            }
            else
            {
                return oldString;
            }
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
            textOauth.Text = Settings.Default.OAuth;
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

        private void textOauth_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsInitialized)
            {
                Settings.Default.OAuth = textOauth.Text;
                Settings.Default.Save();
            }
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new ProcessStartInfo("https://www.buymeacoffee.com/lay295") { UseShellExecute = true });
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
            bool isStart = (bool)checkStart.IsChecked;

            if (isStart)
            {
                SetEnabledCropStart(true);
            }
            else
            {
                SetEnabledCropStart(false);
            }
        }

        private void checkEnd_OnCheckStateChanged(object sender, RoutedEventArgs e)
        {
            bool isEnd = (bool)checkEnd.IsChecked;

            if (isEnd)
            {
                SetEnabledCropEnd(true);
            }
            else
            {
                SetEnabledCropEnd(false);
            }
        }

        private async void SplitButton_Click(object sender, RoutedEventArgs e)
        {
            if (!((HandyControl.Controls.SplitButton)sender).IsDropDownOpen)
            {
                bool isValid = ValidateInput();

                if (isValid)
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();

                    saveFileDialog.Filter = "MP4 Files | *.mp4";
                    saveFileDialog.RestoreDirectory = true;
                    saveFileDialog.FileName = MainWindow.GetFilename(Settings.Default.TemplateVod, textTitle.Text, currentVideoId.ToString(), currentVideoTime, textStreamer.Text);

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        SetEnabled(false);
                        btnGetInfo.IsEnabled = false;

                        VideoDownloadOptions options = GetOptions(saveFileDialog.FileName, null);

                        VideoDownloader currentDownload = new VideoDownloader(options);
                        Progress<ProgressReport> downloadProgress = new Progress<ProgressReport>(OnProgressChanged);

                        SetImage("Images/ppOverheat.gif", true);
                        statusMessage.Text = "Downloading";

                        try
                        {
                            await currentDownload.DownloadAsync(downloadProgress, new CancellationToken());
                            statusMessage.Text = "Done";
                            SetImage("Images/ppHop.gif", true);
                        }
                        catch (Exception ex)
                        {
                            statusMessage.Text = "ERROR";
                            SetImage("Images/peepoSad.png", false);
                            AppendLog("ERROR: " + ex.Message);
                        }
                        btnGetInfo.IsEnabled = true;

                        currentDownload = null;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }
                else
                {
                    AppendLog("ERROR: Invalid Crop Inputs");
                }
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            bool isValid = ValidateInput();

            if (isValid)
            {
                WindowQueueOptions queueOptions = new WindowQueueOptions(this);
                queueOptions.ShowDialog();
            }
            else
            {
                AppendLog("ERROR: Invalid Crop Inputs");
            }
        }
    }
}