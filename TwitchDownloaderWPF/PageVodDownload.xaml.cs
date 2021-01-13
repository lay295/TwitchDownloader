using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using TwitchDownloader;
using TwitchDownloaderWPF;
using Xabe.FFmpeg;
using WpfAnimatedGif;
using TwitchDownloader.Properties;
using Xabe.FFmpeg.Events;
using System.Collections.ObjectModel;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

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
            numStartHour.IsEnabled = isEnabled;
            numStartMinute.IsEnabled = isEnabled;
            numStartSecond.IsEnabled = isEnabled;
            numEndHour.IsEnabled = isEnabled;
            numEndMinute.IsEnabled = isEnabled;
            numEndSecond.IsEnabled = isEnabled;
            btnDownload.IsEnabled = isEnabled;
            btnQueue.IsEnabled = isEnabled;
        }
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
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
                    Task<JObject> taskInfo = TwitchHelper.GetVideoInfo(videoId);
                    Task<JObject> taskAccessToken = TwitchHelper.GetVideoToken(videoId, textOauth.Text);
                    await Task.WhenAll(taskInfo, taskAccessToken);
                    string thumbUrl = taskInfo.Result["preview"]["medium"].ToString();
                    Task<BitmapImage> thumbImage = InfoHelper.GetThumb(thumbUrl);
                    Task<string[]> taskPlaylist = TwitchHelper.GetVideoPlaylist(videoId, taskAccessToken.Result["data"]["videoPlaybackAccessToken"]["value"].ToString(), taskAccessToken.Result["data"]["videoPlaybackAccessToken"]["signature"].ToString());
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
                    TimeSpan vodLength = TimeSpan.FromSeconds(taskInfo.Result["length"].ToObject<int>());
                    textStreamer.Text = taskInfo.Result["channel"]["display_name"].ToString();
                    textTitle.Text = taskInfo.Result["title"].ToString();
                    textCreatedAt.Text = taskInfo.Result["created_at"].ToString();
                    currentVideoTime = taskInfo.Result["created_at"].ToObject<DateTime>().ToLocalTime();
                    numEndHour.Value = (int)vodLength.TotalHours;
                    numEndMinute.Value = vodLength.Minutes;
                    numEndSecond.Value = vodLength.Seconds;
                    labelLength.Text = String.Format("{0:00}:{1:00}:{2:00}", (int)vodLength.TotalHours, vodLength.Minutes, vodLength.Seconds);

                    SetEnabled(true);
                }
                catch (Exception ex)
                {
                    btnGetInfo.IsEnabled = true;
                    AppendLog("ERROR: " + ex.Message);
                    MessageBox.Show("Unable to get the video information." + Environment.NewLine + "Please make sure the video ID is correct and try again.", "Unable To Fetch Video Info", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please enter a valid video ID/URL" + Environment.NewLine + "Examples:" + Environment.NewLine + "https://www.twitch.tv/videos/470741744" + Environment.NewLine + "470741744", "Invalid Video ID/URL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnDownload_Click(object sender, RoutedEventArgs e)
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

                    VideoDownloadOptions options = new VideoDownloadOptions();
                    options.DownloadThreads = (int)numDownloadThreads.Value;
                    options.Filename = saveFileDialog.FileName;
                    options.Oauth = textOauth.Text;
                    options.Quality = comboQuality.Text;
                    options.Id = currentVideoId;
                    options.CropBeginning = (bool)checkStart.IsChecked;
                    options.CropBeginningTime = (int)(new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value).TotalSeconds);
                    options.CropEnding = (bool)checkEnd.IsChecked;
                    options.CropEndingTime = (int)(new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value).TotalSeconds);
                    options.FfmpegPath = "ffmpeg";
                    options.TempFolder = Settings.Default.TempPath;

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
                }
            }
            else
            {
                AppendLog("ERROR: Invalid Crop Inputs");
            }
        }

        private void OnProgressChanged(ProgressReport progress)
        {
            if (progress.reportType == ReportType.Percent)
                statusProgressBar.Value = (int) progress.data;
            if (progress.reportType == ReportType.Message || progress.reportType == ReportType.MessageInfo)
                statusMessage.Text = (string)progress.data;
            if (progress.reportType == ReportType.Log)
                AppendLog((string)progress.data);
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
                int number;
                bool success = Int32.TryParse(text, out number);
                if (success)
                    return number;
                else
                    return -1;
            }
            else if (text.Contains("twitch.tv/videos/"))
            {
                int number;
                //Extract just the numbers from the URL, also remove query string
                Uri url = new UriBuilder(text).Uri;
                string path = String.Format("{0}{1}{2}{3}", url.Scheme, Uri.SchemeDelimiter, url.Authority, url.AbsolutePath);
                bool success = Int32.TryParse(Regex.Match(path, @"\d+").Value, out number);
                if (success)
                    return number;
                else
                    return -1;
            }
            else
                return -1;
        }

        private bool ValidateInput()
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

        public void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke((Action)(() =>
                textLog.AppendText(message + Environment.NewLine)
            ));
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            SetEnabled(false);
            WebRequest.DefaultWebProxy = null;
            numDownloadThreads.Value = Settings.Default.VodDownloadThreads;
            textOauth.Text = Settings.Default.OAuth;
        }

        private void numDownloadThreads_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (numDownloadThreads.IsEnabled)
            {
                Settings.Default.VodDownloadThreads = (int)numDownloadThreads.Value;
                Settings.Default.Save();
            }
        }

        private void textOauth_TextChanged(object sender, TextChangedEventArgs e)
        {
            Settings.Default.OAuth = textOauth.Text;
            Settings.Default.Save();
        }

        private void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void btnDonate_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.buymeacoffee.com/lay295");
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
    }
}