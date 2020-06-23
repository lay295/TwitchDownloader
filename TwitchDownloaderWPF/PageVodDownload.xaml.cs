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
using Xabe.FFmpeg.Model;
using Xabe.FFmpeg;
using WpfAnimatedGif;
using TwitchDownloader.Properties;
using TwitchDownloader.Tasks;
using Xabe.FFmpeg.Events;
using System.Collections.ObjectModel;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageVodDownload.xaml
    /// </summary>
    public partial class PageVodDownload : Page
    {
        public Dictionary<string, string> videoQualties = new Dictionary<string, string>();
        public int currentVideoId;
        public TaskVodDownload currentDownload;

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
                    Task<JObject> taskInfo = InfoHelper.GetVideoInfo(videoId);
                    Task<JObject> taskAccessToken = InfoHelper.GetVideoToken(videoId, textOauth.Text);
                    await Task.WhenAll(taskInfo, taskAccessToken);
                    string thumbUrl = taskInfo.Result["preview"]["medium"].ToString();
                    Task<BitmapImage> thumbImage = InfoHelper.GetThumb(thumbUrl);
                    Task<string[]> taskPlaylist = InfoHelper.GetVideoPlaylist(videoId, taskAccessToken.Result["token"].ToString(), taskAccessToken.Result["sig"].ToString());
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
                    numEndHour.Value = vodLength.Hours;
                    numEndMinute.Value = vodLength.Minutes;
                    numEndSecond.Value = vodLength.Seconds;
                    labelLength.Text = String.Format("{0:00}:{1:00}:{2:00}", vodLength.Hours, vodLength.Minutes, vodLength.Seconds);

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

                if (saveFileDialog.ShowDialog() == true)
                {
                    SetEnabled(false);
                    btnGetInfo.IsEnabled = false;

                    DownloadOptions options = new DownloadOptions();
                    options.UpdateValues(this);
                    options.filename = saveFileDialog.FileName;

                    TaskVodDownload currentDownload = new TaskVodDownload(options);
                    currentDownload.Preview = imgThumbnail.Source;
                    Progress<ProgressReport> uploadProgress = new Progress<ProgressReport>(OnProgressChanged);

                    SetImage("Images/ppOverheat.gif", true);
                    statusMessage.Text = "Downloading";

                    try
                    {
                        await Task.Run(() =>currentDownload.runTask(uploadProgress));
                        statusMessage.Text = "Done";
                        SetImage("Images/ppHop.gif", true);
                    }
                    catch (Exception ex)
                    {
                        statusMessage.Text = "ERROR";
                        SetImage("Images/peepoSad.png", false);
                        AppendLog("ERROR: " + ex.Message);
                    }
                    /*
                    BackgroundWorker backgroundDownloadManager = new BackgroundWorker();
                    backgroundDownloadManager.WorkerReportsProgress = true;
                    backgroundDownloadManager.DoWork += BackgroundDownloadManager_DoWork;
                    backgroundDownloadManager.ProgressChanged += BackgroundDownloadManager_ProgressChanged;
                    backgroundDownloadManager.RunWorkerCompleted += BackgroundDownloadManager_RunWorkerCompleted;
                    */
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
            if (progress.reportType == ReportType.Message)
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
            statusProgressBar.Value = e.ProgressPercentage;
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
            TimeSpan videoLength = TimeSpan.Parse(labelLength.Text.ToString(CultureInfo.InvariantCulture));
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
            checkCFR.IsChecked = Settings.Default.EncodeCFR;
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

        private void checkCFR_Changed(object sender, RoutedEventArgs e)
        {
            Settings.Default.EncodeCFR = (bool)checkCFR.IsChecked;
            Settings.Default.Save();
        }

        private void btnQueue_Click(object sender, RoutedEventArgs e)
        {
            bool isValid = ValidateInput();

            if (isValid)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();

                saveFileDialog.Filter = "MP4 Files | *.mp4";
                saveFileDialog.RestoreDirectory = true;

                if (saveFileDialog.ShowDialog() == true)
                {
                    SetEnabled(false);
                    DownloadOptions options = new DownloadOptions();
                    options.UpdateValues(this);
                    options.filename = saveFileDialog.FileName;

                    TaskVodDownload currentDownload = new TaskVodDownload(options);
                    currentDownload.Preview = imgThumbnail.Source;
                    ObservableCollection<ITwitchTask> taskList = ((MainWindow)Window.GetWindow(this)).taskList;
                    taskList.Add(currentDownload);
                }
            }
            else
            {
                AppendLog("ERROR: Invalid Crop Inputs");
            }
        }
    }
}

public class DownloadOptions
{
    public int id { get; set; }
    public string title { get; set; }
    public string quality { get; set; }
    public string filename { get; set; }
    public string streamer { get; set; }
    public TimeSpan length { get; set; }
    public bool cropped_begin { get; set; }
    public TimeSpan cropped_begin_time { get; set; }
    public double crop_begin { get; set; }
    public bool cropped_end { get; set; }
    public TimeSpan cropped_end_time { get; set; }
    public double crop_end { get; set; }
    public int download_threads { get; set; }
    public bool encode_cfr { get; set; }
    public Dictionary<string, string> video_qualities { get; set; }
    public DownloadOptions()
    {

    }

    public void UpdateValues(PageVodDownload currentPage)
    {
        id = currentPage.currentVideoId;
        title = currentPage.textTitle.Text;
        quality = (string)currentPage.comboQuality.SelectedItem;
        streamer = currentPage.textStreamer.Text;
        length = TimeSpan.Parse(currentPage.labelLength.Text.ToString(CultureInfo.InvariantCulture));
        cropped_begin = (bool)currentPage.checkStart.IsChecked;
        cropped_end = (bool)currentPage.checkEnd.IsChecked;
        cropped_begin_time = new TimeSpan((int)currentPage.numStartHour.Value, (int)currentPage.numStartMinute.Value, (int)currentPage.numStartSecond.Value);
        cropped_end_time = new TimeSpan((int)currentPage.numEndHour.Value, (int)currentPage.numEndMinute.Value, (int)currentPage.numEndSecond.Value);
        crop_begin = 0.0;
        crop_end = 0.0;
        download_threads = (int)currentPage.numDownloadThreads.Value;
        encode_cfr = (bool)currentPage.checkCFR.IsChecked;
        video_qualities = currentPage.videoQualties.ToDictionary(entry => entry.Key, entry => entry.Value);
    }
}