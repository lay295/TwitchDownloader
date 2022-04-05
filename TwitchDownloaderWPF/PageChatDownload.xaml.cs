using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Newtonsoft.Json;
using TwitchDownloaderWPF;
using WpfAnimatedGif;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore;
using System.Threading;
using TwitchDownloader;
using TwitchDownloader.Properties;
using TwitchDownloaderCore.TwitchObjects;

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

        public PageChatDownload()
        {
            InitializeComponent();
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            SetEnabled(false, false);
        }

        private void SetEnabled(bool isEnabled, bool onlyCrop)
        {
            checkStart.IsEnabled = isEnabled;
            checkEnd.IsEnabled = isEnabled;
            numStartHour.IsEnabled = isEnabled;
            numStartMinute.IsEnabled = isEnabled;
            numStartSecond.IsEnabled = isEnabled;
            numEndHour.IsEnabled = isEnabled;
            numEndMinute.IsEnabled = isEnabled;
            numEndSecond.IsEnabled = isEnabled;
            
            if (!onlyCrop)
            {
                radioRelative.IsEnabled = isEnabled;
                radioUTC.IsEnabled = isEnabled;
                radioNone.IsEnabled = isEnabled;
                checkEmbed.IsEnabled = isEnabled;
                btnDownload.IsEnabled = isEnabled;
                btnQueue.IsEnabled = isEnabled;
                radioJson.IsEnabled = isEnabled;
                radioText.IsEnabled = isEnabled;
            }
        }

        private async void btnGetInfo_Click(object sender, RoutedEventArgs e)
        {
            string id = ValidateUrl(textUrl.Text);
            if (id != "")
            {
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
                        GqlVideoResponse taskInfo = await TwitchHelper.GetVideoInfo(Int32.Parse(downloadId));

                        string thumbUrl = taskInfo.data.video.thumbnailURLs.FirstOrDefault();
                        Task<BitmapImage> taskThumb = InfoHelper.GetThumb(thumbUrl);

                        try
                        {
                            await taskThumb;
                        }
                        catch
                        {
                            AppendLog("ERROR: Unable to find thumbnail");
                        }
                        if (!taskThumb.IsFaulted)
                            imgThumbnail.Source = taskThumb.Result;
                        textTitle.Text = taskInfo.data.video.title;
                        textStreamer.Text = taskInfo.data.video.owner.displayName;
                        textCreatedAt.Text = taskInfo.data.video.createdAt.ToString();
                        currentVideoTime = taskInfo.data.video.createdAt.ToLocalTime();
                        streamerId = int.Parse(taskInfo.data.video.owner.id);
                        SetEnabled(true, false);
                    }
                    else if (downloadType == DownloadType.Clip)
                    {
                        string clipId = downloadId;
                        GqlClipResponse taskInfo = await TwitchHelper.GetClipInfo(clipId);

                        string thumbUrl = taskInfo.data.clip.thumbnailURL;
                        Task<BitmapImage> taskThumb = InfoHelper.GetThumb(thumbUrl);
                        await Task.WhenAll(taskThumb);

                        imgThumbnail.Source = taskThumb.Result;
                        textStreamer.Text = taskInfo.data.clip.broadcaster.displayName;
                        textCreatedAt.Text = taskInfo.data.clip.createdAt.ToString();
                        currentVideoTime = taskInfo.data.clip.createdAt.ToLocalTime();
                        textTitle.Text = taskInfo.data.clip.title;
                        streamerId = int.Parse(taskInfo.data.clip.broadcaster.id);
                        SetEnabled(true, false);
                        SetEnabled(false, true);
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
            else
            {
                MessageBox.Show("Please double check the VOD/Clip link", "Unable to parse input", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        static public string ValidateUrl(string text)
        {
            Regex clipRegex = new Regex(@"twitch.tv\/(\S+)\/clip\/");
            if (text.All(Char.IsLetter) || text.All(Char.IsDigit))
            {
                return text;
            }
            else if (text.Contains("twitch.tv/videos/"))
            {
                int number;
                Uri url = new UriBuilder(text).Uri;
                string path = String.Format("{0}{1}{2}{3}", url.Scheme, Uri.SchemeDelimiter, url.Authority, url.AbsolutePath);
                bool success = Int32.TryParse(Regex.Match(path, @"\d+").Value, out number);
                if (success)
                    return number.ToString();
                else
                    return "";
            }
            else if (text.Contains("clips.twitch.tv/") || clipRegex.IsMatch(text))
            {
                Uri url = new UriBuilder(text).Uri;
                string path = String.Format("{0}{1}{2}{3}", url.Scheme, Uri.SchemeDelimiter, url.Authority, url.AbsolutePath);
                return path.Split('/').Last();
            }
            return "";
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke((Action)(() =>
                textLog.AppendText(message + Environment.NewLine)
            ));
        }

        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            if (radioJson.IsChecked == true)
                saveFileDialog.Filter = "JSON Files | *.json";
            else
                saveFileDialog.Filter = "TXT Files | *.txt";

            saveFileDialog.RestoreDirectory = true;
            saveFileDialog.FileName = MainWindow.GetFilename(Settings.Default.TemplateChat, textTitle.Text, downloadId, currentVideoTime, textStreamer.Text);

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ChatDownloadOptions downloadOptions = GetOptions(saveFileDialog.FileName);
                    if (downloadType == DownloadType.Video)
                    {
                        int startTime = 0;
                        int endTime = 0;

                        if (checkStart.IsChecked == true)
                        {
                            downloadOptions.CropBeginning = true;
                            TimeSpan start = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                            startTime = (int)Math.Round(start.TotalSeconds);
                            downloadOptions.CropBeginningTime = startTime;
                        }

                        if (checkEnd.IsChecked == true)
                        {
                            downloadOptions.CropEnding = true;
                            TimeSpan end = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
                            endTime = (int)Math.Round(end.TotalSeconds);
                            downloadOptions.CropEndingTime = endTime;
                        }

                        downloadOptions.Id = downloadId;
                    }
                    else
                    {
                        downloadOptions.Id = downloadId;
                    }

                    if ((bool)radioUTC.IsChecked)
                        downloadOptions.TimeFormat = TimestampFormat.Utc;
                    if ((bool)radioRelative.IsChecked)
                        downloadOptions.TimeFormat = TimestampFormat.Relative;
                    if ((bool)radioNone.IsChecked)
                        downloadOptions.TimeFormat = TimestampFormat.None;

                    ChatDownloader currentDownload = new ChatDownloader(downloadOptions);

                    btnGetInfo.IsEnabled = false;
                    SetEnabled(false, false);
                    SetImage("Images/ppOverheat.gif", true);
                    statusMessage.Text = "Downloading";

                    Progress<ProgressReport> downloadProgress = new Progress<ProgressReport>(OnProgressChanged);

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
                    statusProgressBar.Value = 0;
                }
                catch (Exception ex)
                {
                    AppendLog("ERROR: " + ex.Message);
                }
            }
        }

        public ChatDownloadOptions GetOptions(string filename)
        {
            ChatDownloadOptions options = new ChatDownloadOptions();
            options.IsJson = (bool)radioJson.IsChecked;
            options.Timestamp = true;
            options.EmbedEmotes = (bool)checkEmbed.IsChecked;
            options.Filename = filename;
            options.ConnectionCount = (int)numChatDownloadConnections.Value;
            return options;
        }

        private void OnProgressChanged(ProgressReport progress)
        {
            if (progress.reportType == ReportType.Percent)
                statusProgressBar.Value = (int)progress.data;
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

        private void btnSettings_Loaded(object sender, RoutedEventArgs e)
        {
            btnDonate.Visibility = Settings.Default.HideDonation ? Visibility.Collapsed : Visibility.Visible;
        }

        private void radioText_Checked(object sender, RoutedEventArgs e)
        {
            timeText.Visibility = Visibility.Visible;
            timeOptions.Visibility = Visibility.Visible;
            checkEmbed.IsEnabled = false;
        }

        private void radioText_Unchecked(object sender, RoutedEventArgs e)
        {
            timeText.Visibility = Visibility.Collapsed;
            timeOptions.Visibility = Visibility.Collapsed;
            checkEmbed.IsEnabled = true;
        }

        private void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            WindowQueueOptions queueOptions = new WindowQueueOptions(this);
            queueOptions.ShowDialog();
        }

        private void numChatDownloadConnections_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            numChatDownloadConnections.Value = Math.Clamp((int)numChatDownloadConnections.Value, 1, 50);
        }
    }
}

public class ChatDownloadInfo
{
    public DownloadType is_vod { get; set; }
    public string id { get; set; }
    public string path { get; set; }
    public string vod_id { get; set; }
    public int offset { get; set; }
    public double duration { get; set; }
    public bool is_json { get; set; }
    public string streamer_name { get; set; }
    public int streamer_id { get; set; }

    public ChatDownloadInfo(DownloadType Is_vod, string Id, string Path, string Vod_id, int Offset, double Duration, bool Is_json, string Streamer_name, int Streamer_id)
    {
        is_vod = Is_vod;
        id = Id;
        path = Path;
        vod_id = Vod_id;
        offset = Offset;
        duration = Duration;
        is_json = Is_json;
        streamer_name = Streamer_name;
        streamer_id = Streamer_id;
    }
}