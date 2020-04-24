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
using System.Windows.Shapes;
using Newtonsoft.Json;
using TwitchDownloaderWPF;
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
        JObject videoData = new JObject();
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
                btnDownload.IsEnabled = isEnabled;
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
                        Task<JObject> taskInfo = InfoHelper.GetVideoInfo(Int32.Parse(downloadId));
                        await Task.WhenAll(taskInfo);

                        JToken clipData = taskInfo.Result;
                        videoData = taskInfo.Result;
                        string thumbUrl = videoData["data"][0]["thumbnail_url"].ToString().Replace("%{width}", 512.ToString()).Replace("%{height}", 290.ToString());
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
                        textTitle.Text = taskInfo.Result["data"][0]["title"].ToString();
                        textStreamer.Text = taskInfo.Result["data"][0]["user_name"].ToString();
                        textCreatedAt.Text = taskInfo.Result["data"][0]["created_at"].ToString();
                        streamerId = taskInfo.Result["data"][0]["user_id"].ToObject<int>();
                        SetEnabled(true, false);
                    }
                    else if (downloadType == DownloadType.Clip)
                    {
                        string clipId = downloadId;
                        Task<JObject> taskInfo = InfoHelper.GetClipInfoChat(clipId);
                        await Task.WhenAll(taskInfo);

                        JToken clipData = taskInfo.Result;
                        videoData = taskInfo.Result;
                        string thumbUrl = clipData["thumbnails"]["medium"].ToString();
                        Task<BitmapImage> taskThumb = InfoHelper.GetThumb(thumbUrl);
                        await Task.WhenAll(taskThumb);

                        imgThumbnail.Source = taskThumb.Result;
                        textStreamer.Text = clipData["broadcaster"]["display_name"].ToString();
                        textCreatedAt.Text = clipData["created_at"].ToString();
                        textTitle.Text = clipData["title"].ToString();
                        streamerId = clipData["broadcaster"]["id"].ToObject<int>();
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

        private string ValidateUrl(string text)
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

        private void BackgroundDownloadManager_DoWork(object sender, DoWorkEventArgs e)
        {
            ChatDownloadInfo clipInfo = (ChatDownloadInfo)e.Argument;

            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Accept", "application/vnd.twitchtv.v5+json; charset=UTF-8");
                client.Headers.Add("Client-Id", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                bool isFirst = true;
                string cursor = "";
                double latestMessage = clipInfo.offset - 1;
                double videoStart = clipInfo.offset;
                double videoDuration = clipInfo.duration;
                JObject result = new JObject();
                JArray comments = new JArray();
                JObject streamer = new JObject();

                streamer["name"] = clipInfo.streamer_name;
                streamer["id"] = clipInfo.streamer_id;

                while (latestMessage < (videoStart + videoDuration))
                {
                    string response;
                    if (isFirst)
                        response = client.DownloadString(String.Format("https://api.twitch.tv/v5/videos/{0}/comments?content_offset_seconds={1}", clipInfo.vod_id, clipInfo.offset));
                    else
                        response = client.DownloadString(String.Format("https://api.twitch.tv/v5/videos/{0}/comments?cursor={1}", clipInfo.vod_id, cursor));

                    JObject res = JObject.Parse(response);

                    foreach (var comment in res["comments"])
                    {
                        if (latestMessage < (videoStart + videoDuration))
                            comments.Add(comment);

                        latestMessage = comment["content_offset_seconds"].ToObject<double>();
                    }
                    if (res["_next"] == null)
                        break;
                    else
                        cursor = res["_next"].ToString();

                    int percent = (int)Math.Floor((latestMessage - videoStart) / videoDuration * 100);
                    (sender as BackgroundWorker).ReportProgress(percent, String.Format("Downloading {0}%", percent));

                    if (isFirst)
                        isFirst = false;

                }

                result["streamer"] = streamer;
                result["comments"] = comments;

                using (StreamWriter sw = new StreamWriter(clipInfo.path))
                {
                    if (clipInfo.is_json)
                    {
                        sw.Write(result.ToString(Formatting.None));
                    }
                    else
                    {
                        foreach (var comment in result["comments"])
                        {
                            string username = comment["commenter"]["display_name"].ToString();
                            string message = comment["message"]["body"].ToString();
                            sw.WriteLine(String.Format("{0}: {1}", username, message));
                        }
                    }

                    sw.Flush();
                    sw.Close();
                    clipInfo = null;
                    result = null;
                }
            }
        }

        private void BackgroundDownloadManager_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnGetInfo.IsEnabled = true;
            statusProgressBar.Value = 0;
            if (e.Error == null)
            {
                statusMessage.Text = "Done";
                SetImage("Images/ppHop.gif", true);

            }
            else
            {
                statusMessage.Text = "ERROR";
                SetImage("Images/peepoSad.png", false);
                AppendLog("ERROR: " + e.Error.Message);
            }
        }

        private void BackgroundDownloadManager_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string message = (string)e.UserState;
            statusMessage.Text = message;
            statusProgressBar.Value = e.ProgressPercentage >= 100 ? 100 : e.ProgressPercentage;
        }

        private void AppendLog(string message)
        {
            textLog.Dispatcher.BeginInvoke((Action)(() =>
                textLog.AppendText(message + Environment.NewLine)
            ));
        }

        private void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            if (radioJson.IsChecked == true)
                saveFileDialog.Filter = "JSON Files | *.json";
            else
                saveFileDialog.Filter = "TXT Files | *.txt";

            saveFileDialog.RestoreDirectory = true;

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ChatDownloadInfo info;
                    if (downloadType == DownloadType.Video)
                    {
                        int startTime = 0;
                        int duration = 0;

                        if (checkStart.IsChecked == true)
                        {
                            TimeSpan start = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                            startTime = (int)Math.Round(start.TotalSeconds);
                        }

                        if (checkEnd.IsChecked == true)
                        {
                            TimeSpan end = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
                            duration = (int)Math.Ceiling(end.TotalSeconds - startTime);
                        }
                        else
                        {
                            TimeSpan vodLength = GenerateTimespan(((JValue)videoData["data"][0]["duration"]).ToString(CultureInfo.InvariantCulture));
                            duration = (int)Math.Ceiling(vodLength.TotalSeconds);
                        }
                        info = new ChatDownloadInfo(downloadType, textUrl.Text, saveFileDialog.FileName, videoData["data"][0]["id"].ToString(), startTime, duration, (bool)radioJson.IsChecked, textStreamer.Text, streamerId);
                    }
                    else
                        info = new ChatDownloadInfo(downloadType, textUrl.Text, saveFileDialog.FileName, videoData["vod"]["id"].ToString(), videoData["vod"]["offset"].ToObject<int>(), videoData["duration"].ToObject<double>(), (bool)radioJson.IsChecked, textStreamer.Text, streamerId);
                    statusMessage.Text = "Downloading";
                    btnGetInfo.IsEnabled = false;
                    SetEnabled(false, false);

                    BackgroundWorker backgroundDownloadManager = new BackgroundWorker();
                    backgroundDownloadManager.WorkerReportsProgress = true;
                    backgroundDownloadManager.DoWork += BackgroundDownloadManager_DoWork;
                    backgroundDownloadManager.ProgressChanged += BackgroundDownloadManager_ProgressChanged;
                    backgroundDownloadManager.RunWorkerCompleted += BackgroundDownloadManager_RunWorkerCompleted;

                    SetImage("Images/ppOverheat.gif", true);
                    statusMessage.Text = "Downloading";
                    backgroundDownloadManager.RunWorkerAsync(info);
                }
                catch (Exception ex)
                {
                    AppendLog("ERROR: " + ex.Message);
                }
            }
        }

        private TimeSpan GenerateTimespan(string input)
        {
            //There might be a better way to do this, gets string 0h0m0s and returns timespan
            TimeSpan returnSpan = new TimeSpan(0);
            string[] inputArray = input.Remove(input.Length - 1).Replace('h', ':').Replace('m', ':').Split(':');

            returnSpan = returnSpan.Add(TimeSpan.FromSeconds(Int32.Parse(inputArray[inputArray.Length - 1])));
            if (inputArray.Length > 1)
                returnSpan = returnSpan.Add(TimeSpan.FromMinutes(Int32.Parse(inputArray[inputArray.Length - 2])));
            if (inputArray.Length > 2)
                returnSpan = returnSpan.Add(TimeSpan.FromHours(Int32.Parse(inputArray[inputArray.Length - 3])));

            return returnSpan;
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