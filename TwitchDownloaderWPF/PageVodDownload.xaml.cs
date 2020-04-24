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
using Xabe.FFmpeg.Events;

namespace TwitchDownloaderWPF
{
    /// <summary>
    /// Interaction logic for PageVodDownload.xaml
    /// </summary>
    public partial class PageVodDownload : Page
    {
        public Dictionary<string, string> videoQualties = new Dictionary<string, string>();
        public int currentVideoId;

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
                    string thumbUrl = taskInfo.Result["data"][0]["thumbnail_url"].ToString().Replace("%{width}", 512.ToString()).Replace("%{height}", 290.ToString());
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
                    TimeSpan vodLength = GenerateTimespan(taskInfo.Result["data"][0]["duration"].ToString());
                    textStreamer.Text = taskInfo.Result["data"][0]["user_name"].ToString();
                    textTitle.Text = taskInfo.Result["data"][0]["title"].ToString();
                    textCreatedAt.Text = taskInfo.Result["data"][0]["created_at"].ToString();
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

        private void btnDownload_Click(object sender, RoutedEventArgs e)
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

                    BackgroundWorker backgroundDownloadManager = new BackgroundWorker();
                    backgroundDownloadManager.WorkerReportsProgress = true;
                    backgroundDownloadManager.DoWork += BackgroundDownloadManager_DoWork;
                    backgroundDownloadManager.ProgressChanged += BackgroundDownloadManager_ProgressChanged;
                    backgroundDownloadManager.RunWorkerCompleted += BackgroundDownloadManager_RunWorkerCompleted;

                    SetImage("Images/ppOverheat.gif", true);
                    statusMessage.Text = "Downloading";

                    backgroundDownloadManager.RunWorkerAsync(options);
                }
            }
            else
            {
                AppendLog("ERROR: Invalid Crop Inputs");
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

        private void BackgroundDownloadManager_DoWork(object sender, DoWorkEventArgs e)
        {
            DownloadOptions options = (DownloadOptions)e.Argument;
            List<Thread> threads = new List<Thread>();
            int downloadThreads = options.download_threads;
            string tempFolder = Path.Combine(Path.GetTempPath(), "TwitchDownloader");
            string downloadFolder = Path.Combine(tempFolder, options.id.ToString());
            ServicePointManager.DefaultConnectionLimit = downloadThreads;

            if (Directory.Exists(downloadFolder))
                DeleteDirectory(downloadFolder);
            Directory.CreateDirectory(downloadFolder);

            string playlistUrl = "";
            foreach (var item in videoQualties)
            {
                if (item.Key == options.quality)
                {
                    playlistUrl = item.Value;
                }
            }
            string baseUrl = playlistUrl.Substring(0, playlistUrl.LastIndexOf("/") + 1);
            List<KeyValuePair<string, double>> videoList = new List<KeyValuePair<string, double>>();
            using (WebClient client = new WebClient())
            {
                string[] videoChunks = client.DownloadString(playlistUrl).Split('\n');

                for (int i = 0; i < videoChunks.Length; i++)
                {
                    if (videoChunks[i].Contains("#EXTINF"))
                        videoList.Add(new KeyValuePair<string, double>(videoChunks[i + 1], Double.Parse(videoChunks[i].Remove(0, 8).TrimEnd(','), CultureInfo.InvariantCulture)));
                }
            }
            Queue<string> videoParts = new Queue<string>(GenerateCroppedVideoList(videoList, options));
            List<string> videoPartsList = new List<string>(videoParts);
            int partCount = videoParts.Count;
            int doneCount = 0;

            Parallel.ForEach(videoParts, new ParallelOptions { MaxDegreeOfParallelism = downloadThreads },
            videoPart =>
            {
                DownloadThread(videoPart, baseUrl, downloadFolder);
                doneCount++;
                int percent = (int)Math.Floor(((double)doneCount / (double)partCount) * 100);
                (sender as BackgroundWorker).ReportProgress(percent, String.Format("Downloading {0}% (1/3)", percent));
            });

            (sender as BackgroundWorker).ReportProgress(0, "Combining Parts (2/3)");

            if (videoPartsList[0].Contains("muted"))
            {
                string inputFileMuted = Path.Combine(downloadFolder, videoPartsList[0]);
                string outputFileMuted = Path.Combine(downloadFolder, "new_" + videoPartsList[0]);
                Task<IConversionResult> unmuteResult = new Conversion().Start(String.Format("-f lavfi -i anullsrc -i \"{0}\" -shortest -c:v copy -c:a aac -map 0:a -map 1:v \"{1}\"", inputFileMuted, outputFileMuted));
                Task.WaitAll(unmuteResult);
                videoPartsList[0] = "new_" + videoPartsList[0];
            }

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(Path.Combine(downloadFolder, options.id + ".txt"), false))
            {
                foreach (var part in videoPartsList)
                {
                    file.WriteLine(String.Format("file '{0}'", Path.Combine(downloadFolder, part)));
                }
            }

            string inputFile = Path.Combine(downloadFolder, options.id + ".txt");
            string outputFile = Path.Combine(downloadFolder, "output.ts");
            Task<IConversionResult> combineResult = new Conversion().Start(String.Format("-f concat -safe 0 -i \"{0}\" -c copy \"{1}\"", inputFile, outputFile));
            Task.WaitAll(combineResult);

            foreach (var part in videoPartsList)
            {
                string file = Path.Combine(downloadFolder, part);
                if (File.Exists(file))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch { }
                }
            }
            
            bool isVFR = false;
            if (options.encode_cfr)
            {
                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = "ffmpeg.exe",
                        Arguments = $"-i \"" + Path.Combine(downloadFolder, "output.ts") + "\" -vf vfrdet -f null -",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                string output = "";
                process.ErrorDataReceived += delegate(object o, DataReceivedEventArgs args)
                {
                    if (args.Data != null && args.Data.Contains("Parsed_vfrdet"))
                    {
                        output += args.Data;
                    }
                };
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                process.WaitForExit();
                double VFR = double.Parse(output.Substring(output.IndexOf("VFR:") + 4, 8));
                if (VFR == 0.0)
                {
                    AppendLog("Constant framerate detected, no need to re-encode");
                }
                else
                {
                    isVFR = true;
                    AppendLog("Detected variable framerate, re-encoding");
                }
            }
            
            if (isVFR)
                (sender as BackgroundWorker).ReportProgress(0, "Re-encoding MP4 (3/3)");
            else
                (sender as BackgroundWorker).ReportProgress(0, "Finalizing MP4 (3/3)");
            string outputConvert = options.filename;
            Task<IMediaInfo> info = MediaInfo.Get(Path.Combine(downloadFolder, "output.ts"));
            Task.WaitAll(info);
            double seekTime = options.crop_begin;
            double seekDuration = Math.Round(info.Result.Duration.TotalSeconds - seekTime - options.crop_end);
            Task<IConversionResult> conversionResult = null;
            if (isVFR)
            {
                int newFps = (int)Math.Ceiling(info.Result.VideoStreams.First().FrameRate);
                conversionResult = Conversion.New().Start(String.Format("-y -i \"{0}\" -ss {1} -analyzeduration {2} -t {3} -crf 20 -filter:v fps=fps={4} \"{5}\"", Path.Combine(downloadFolder, "output.ts"), seekTime.ToString(), int.MaxValue, seekDuration.ToString(), newFps, outputConvert));
            }
            else
            {
                conversionResult = Conversion.New().Start(String.Format("-y -i \"{0}\" -ss {1} -analyzeduration {2} -t {3} -avoid_negative_ts make_zero -acodec copy -vcodec copy \"{4}\"", Path.Combine(downloadFolder, "output.ts"), seekTime.ToString(), int.MaxValue, seekDuration.ToString(), outputConvert));
            }
                
            Task.WaitAll(conversionResult);
            if (Directory.Exists(downloadFolder))
                DeleteDirectory(downloadFolder);
        }

        private List<string> GenerateCroppedVideoList(List<KeyValuePair<string, double>> videoList, DownloadOptions options)
        {
            double beginTime = 0.0;
            double endTime = 0.0;

            TimeSpan startCrop = options.cropped_begin_time;
            TimeSpan endCrop = options.cropped_end_time;

            foreach (var video in videoList)
                endTime += video.Value;

            if (options.cropped_begin)
            {
                for (int i = 0; i < videoList.Count; i++)
                {
                    if (beginTime + videoList[i].Value <= startCrop.TotalSeconds)
                    {
                        beginTime += videoList[i].Value;
                        videoList.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        options.crop_begin = (beginTime + videoList[i].Value) - startCrop.TotalSeconds;
                        options.crop_begin = videoList[i].Value - options.crop_begin;
                        break;
                    }
                }
            }

            if (options.cropped_end)
            {
                for (int i = videoList.Count - 1; i >= 0; i--)
                {
                    if (endTime - videoList[i].Value >= endCrop.TotalSeconds)
                    {
                        endTime -= videoList[i].Value;
                        videoList.RemoveAt(i);
                    }
                    else
                    {
                        options.crop_end = endCrop.TotalSeconds - (endTime - videoList[i].Value);
                        options.crop_end = videoList[i].Value - options.crop_end;
                        break;
                    }
                }
            }
            List<string> result = new List<string>();
            foreach (var video in videoList)
                result.Add(video.Key);

            return result;
        }

        private static void DownloadThread(string videoPart, string baseUrl, string downloadFolder)
        {
            bool isDone = false;
            int errorCount = 0;
            while (!isDone && errorCount < 10)
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(baseUrl + videoPart, Path.Combine(downloadFolder, videoPart));
                        isDone = true;
                    }
                }
                catch (WebException)
                {
                    errorCount++;
                    Thread.Sleep(10000);
                }
            }

            if (!isDone)
                throw new Exception("Video part " + videoPart + " failed after 10 retries");
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

        public static void DeleteDirectory(string target_dir)
        {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
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
    }
}
public class ProgressReport
{
    public int current_steps { get; set; }
    public int total_steps { get; set; }
    public string message { get; set; }
    public ProgressReport(int Current_steps, int Total_steps, string Message)
    {
        current_steps = Current_steps;
        total_steps = Total_steps;
        message = Message;
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
    }
}