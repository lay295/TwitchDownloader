using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TwitchDownloader;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Enums;
using Xabe.FFmpeg.Model;
using Xabe.FFmpeg.Streams;

namespace TwitchDownloader
{
    public partial class frmVodDownload : Form
    {
        List<KeyValuePair<string, string>> videoQualties = new List<KeyValuePair<string, string>>();

        public frmVodDownload()
        {
            InitializeComponent();
        }

        private async void BtnGetInfo_Click(object sender, EventArgs e)
        {
            if (!textUrl.Text.All(char.IsDigit) || textUrl.Text.Length == 0)
            {
                MessageBox.Show("Please enter a valid VOD ID", "Invalid VOD ID", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                btnGetInfo.Enabled = false;

                string vodId = textUrl.Text;
                Task<JObject> taskInfo = GetInfo(vodId);
                Task<JObject> taskAccessToken = GetToken(vodId);
                await Task.WhenAll(taskInfo, taskAccessToken);
                string thumbUrl = taskInfo.Result["data"][0]["thumbnail_url"].ToString().Replace("%{width}", pictureThumb.Width.ToString()).Replace("%{height}", pictureThumb.Height.ToString());
                Task<Bitmap> taskThumb = GetThumb(thumbUrl);
                Task<string[]> taskPlaylist = GetPlaylist(vodId, taskAccessToken.Result["token"].ToString(), taskAccessToken.Result["sig"].ToString());
                await Task.WhenAll(taskThumb, taskPlaylist);

                string[] playlist = taskPlaylist.Result;
                for (int i = 0; i < playlist.Length; i++)
                {
                    if (playlist[i].Contains("#EXT-X-MEDIA"))
                    {
                        string lastPart = playlist[i].Substring(playlist[i].IndexOf("NAME=\"") + 6);
                        string stringQuality = lastPart.Substring(0, lastPart.IndexOf("\""));

                        videoQualties.Add(new KeyValuePair<string, string>(stringQuality, playlist[i + 2]));
                        comboQuality.Items.Add(stringQuality);
                    }
                }

                comboQuality.SelectedIndex = 0;
                pictureThumb.Image = taskThumb.Result;
                textFilename.Text = String.Format("{0}_{1}", vodId, taskInfo.Result["data"][0]["user_name"].ToString());
                textTitle.Text = taskInfo.Result["data"][0]["title"].ToString();
                labelStreamer.Text = taskInfo.Result["data"][0]["user_name"].ToString();
                labelCreated.Text = taskInfo.Result["data"][0]["created_at"].ToString();
                TimeSpan vodLength = TimeSpan.Parse(Regex.Replace(taskInfo.Result["data"][0]["duration"].ToString(), @"[^\d]", ":").TrimEnd(':'));
                numEndHour.Value = vodLength.Hours;
                numEndMinute.Value = vodLength.Minutes;
                numEndSecond.Value = vodLength.Seconds;
                labelLength.Text = String.Format("{0}:{1}:{2}", vodLength.Hours, vodLength.Minutes, vodLength.Seconds);
                textFolder.Text = Properties.Settings.Default.DOWNLOAD_FOLDER;

                btnGetInfo.Enabled = true;
                SetEnabled(true);
            }
            catch (WebException)
            {
                MessageBox.Show("Unable to get Twitch VOD information. Please double check VOD ID and try again", "Unable to get info", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnGetInfo.Enabled = true;
                SetEnabled(false);
            }
        }

        private async Task<string[]> GetPlaylist(string vodId, string token, string sig)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                string playlist = await client.DownloadStringTaskAsync(String.Format("http://usher.twitch.tv/vod/{0}?nauth={1}&nauthsig={2}&allow_source=true&player=twitchweb", vodId, token, sig));
                return playlist.Split('\n');
            }
        }

        private async Task<Bitmap> GetThumb(string url)
        {
            Bitmap result = new Bitmap(100, 100);
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                using (Stream s = await client.OpenReadTaskAsync(url))
                {
                    result = new Bitmap(s);
                }
            }
            return result;
        }

        private async Task<JObject> GetToken(string vodId)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.DownloadStringTaskAsync(String.Format("https://api.twitch.tv/api/vods/{0}/access_token", vodId));
                JObject result = JObject.Parse(response);
                return result;
            }
        }

        private async Task<JObject> GetInfo(string id)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.DownloadStringTaskAsync("https://api.twitch.tv/helix/videos?id=" + id);
                JObject result = JObject.Parse(response);
                return result;
            }
        }

        private void SetEnabled(bool isEnabled)
        {
            comboQuality.Enabled = isEnabled;
            textFolder.Enabled = isEnabled;
            btnFolder.Enabled = isEnabled;
            textFilename.Enabled = isEnabled;
            checkCropStart.Enabled = isEnabled;
            numStartHour.Enabled = isEnabled;
            numStartMinute.Enabled = isEnabled;
            numStartSecond.Enabled = isEnabled;
            checkCropEnd.Enabled = isEnabled;
            numEndHour.Enabled = isEnabled;
            numEndMinute.Enabled = isEnabled;
            numEndSecond.Enabled = isEnabled;
            btnDownload.Enabled = isEnabled;
        }

        private void BtnFolder_Click(object sender, EventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                textFolder.Text = dialog.FileName;
                Properties.Settings.Default.DOWNLOAD_FOLDER = dialog.FileName;
            }
        }
        private void BtnDownload_Click(object sender, EventArgs e)
        {
            SetEnabled(false);
            btnGetInfo.Enabled = false;
            btnDownload.Enabled = false;
            DownloadOptions options = new DownloadOptions();
            options.id = textUrl.Text;
            options.title = textTitle.Text;
            options.quality = (string)comboQuality.SelectedItem;
            options.folder = textFolder.Text;
            options.filename = textFilename.Text;
            options.streamer = labelStreamer.Text;
            options.created = labelCreated.Text;
            options.length = TimeSpan.Parse(labelLength.Text);
            options.cropped_begin = checkCropStart.Checked;
            options.cropped_end = checkCropEnd.Checked;
            options.cropped_begin_time = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
            options.cropped_end_time = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
            options.crop_end = 0.0;
            options.crop_begin = 0.0;
            toolStatus.Text = "Downloading";
            backgroundDownloadManager.RunWorkerAsync(options);

            Properties.Settings.Default.Save();
        }

        private void BackgroundDownloadManager_DoWork(object sender, DoWorkEventArgs e)
        {
            DownloadOptions options = (DownloadOptions)e.Argument;
            List<Thread> threads = new List<Thread>();
            int downloadThreads = 10;
            string tempFolder = Path.Combine(Path.GetTempPath(), "TwitchDownloader");
            string downloadFolder = Path.Combine(tempFolder, options.id);

            if (Directory.Exists(downloadFolder))
                DeleteDirectory(downloadFolder);

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
                        videoList.Add(new KeyValuePair<string, double>(videoChunks[i + 1], Double.Parse(videoChunks[i].Remove(0, 8).TrimEnd(','))));
                }
            }
            Queue<string> videoParts = new Queue<string>(GenerateCroppedVideoList(videoList, options));
            List<string> videoPartsList = new List<string>(videoParts);
            int partCount = videoParts.Count;
            int doneCount = 0;

            while (videoParts.Count > 0 || threads.Count > 0)
            {
                Thread.Sleep(1000);

                List<Thread> toRemove = new List<Thread>();
                foreach (var thread in threads)
                {
                    if (!thread.IsAlive)
                        toRemove.Add(thread);
                }
                foreach (var thread in toRemove)
                {
                    threads.Remove(thread);
                    doneCount++;

                    int percent = (int)Math.Floor(((double)doneCount / (double)partCount) * 100);
                    backgroundDownloadManager.ReportProgress(percent, String.Format("Downloading {0}% (1/3)", percent));
                }
                
                for (int i = threads.Count - 1; i < downloadThreads - 1; i++)
                {
                    if (videoParts.Count > 0)
                    {
                        string videoPart = videoParts.Dequeue();
                        if (videoPart is string && videoPart != null)
                        {
                            Thread thread = new Thread(() => DownloadThread(videoPart, baseUrl, downloadFolder));
                            thread.Start();
                            threads.Add(thread);
                        }
                    }
                }
            }

            backgroundDownloadManager.ReportProgress(0, "Combining Parts (2/3)");

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

            string outputConvert = Path.Combine(options.folder, options.filename + ".mp4");
            Task<IMediaInfo> info = MediaInfo.Get(Path.Combine(downloadFolder, "output.ts"));
            Task.WaitAll(info);
            double seekTime = options.crop_begin;
            double seekDuration = info.Result.Duration.TotalSeconds - seekTime - options.crop_end;
            Task<IConversionResult> conversionResult = Conversion.New().Start(String.Format("-y -i \"{0}\" -ss {1} -t {2} -acodec copy -vcodec copy -copyts \"{3}\"", Path.Combine(downloadFolder, "output.ts"), seekTime.ToString(), seekDuration.ToString(), outputConvert));
            Task.WaitAll(conversionResult);
            if (Directory.Exists(downloadFolder))
                DeleteDirectory(downloadFolder);
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
            while (!isDone && errorCount < 4)
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(baseUrl + videoPart, Path.Combine(downloadFolder, videoPart));
                        isDone = true;
                    }
                }
                catch
                {
                    errorCount++;
                }
            }

            if (!isDone)
                throw new Exception("RIP");
        }

        private void BackgroundDownloadManager_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnGetInfo.Enabled = true;
            toolStatus.Text = "Done Downloading";
        }

        private void BackgroundDownloadManager_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string message = (string)e.UserState;
            toolStatus.Text = message;
            toolProgressBar.Value = e.ProgressPercentage;
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
    public string id { get; set; }
    public string title { get; set; }
    public string quality { get; set; }
    public string folder { get; set; }
    public string filename { get; set; }
    public string streamer { get; set; }
    public string created { get; set; }
    public TimeSpan length { get; set; }
    public bool cropped_begin { get; set; }
    public TimeSpan cropped_begin_time { get; set; }
    public double crop_begin { get; set; }
    public bool cropped_end { get; set; }
    public TimeSpan cropped_end_time { get; set; }
    public double crop_end { get; set; }
    public DownloadOptions()
    {
        
    }
}