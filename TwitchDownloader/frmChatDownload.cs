using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TwitchDownloader
{
    public partial class frmChatDownload : Form
    {
        bool isVod = true;
        int streamerId = 0;
        JObject videoData = new JObject();
        public frmChatDownload()
        {
            InitializeComponent();
        }

        private async void BtnGetInfo_Click(object sender, EventArgs e)
        {
            try
            {
                isVod = textUrl.Text.Any(char.IsDigit);

                btnGetInfo.Enabled = false;
                if (isVod)
                {
                    string VodId = textUrl.Text;
                    Task<JObject> taskInfo = GetVodInfo(VodId);
                    await Task.WhenAll(taskInfo);

                    JToken clipData = taskInfo.Result;
                    videoData = taskInfo.Result;
                    string thumbUrl = videoData["data"][0]["thumbnail_url"].ToString().Replace("%{width}", pictureThumb.Width.ToString()).Replace("%{height}", pictureThumb.Height.ToString());
                    Task<Bitmap> taskThumb = GetClipThumb(thumbUrl);
                    await Task.WhenAll(taskThumb);

                    pictureThumb.Image = taskThumb.Result;
                    textTitle.Text = taskInfo.Result["data"][0]["title"].ToString();
                    labelStreamer.Text = taskInfo.Result["data"][0]["user_name"].ToString();
                    labelCreated.Text = taskInfo.Result["data"][0]["created_at"].ToString();
                    streamerId = taskInfo.Result["data"][0]["user_id"].ToObject<int>();
                    SetEnabled(true, false);
                }
                else
                {
                    string clipId = textUrl.Text;
                    Task<JObject> taskInfo = GetClipInfo(clipId);
                    await Task.WhenAll(taskInfo);

                    JToken clipData = taskInfo.Result;
                    videoData = taskInfo.Result;
                    string thumbUrl = clipData["thumbnails"]["medium"].ToString();
                    Task<Bitmap> taskThumb = GetClipThumb(thumbUrl);
                    await Task.WhenAll(taskThumb);

                    pictureThumb.Image = taskThumb.Result;
                    labelStreamer.Text = clipData["broadcaster"]["display_name"].ToString();
                    labelCreated.Text = clipData["created_at"].ToString();
                    textTitle.Text = clipData["title"].ToString();
                    streamerId = clipData["broadcaster"]["id"].ToObject<int>();
                    SetEnabled(true, false);
                    SetEnabled(false, true);
                }

                btnGetInfo.Enabled = true;
            }
            catch (WebException)
            {
                MessageBox.Show("Unable to get information. Please double check VOD ID or Clip Slug and try again", "Unable to get info", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnGetInfo.Enabled = true;
                SetEnabled(false, false);
            }
        }

        private void SetEnabled(bool isEnabled, bool onlyCrop)
        {
            checkCropStart.Enabled = isEnabled;
            numStartHour.Enabled = isEnabled;
            numStartMinute.Enabled = isEnabled;
            numStartSecond.Enabled = isEnabled;
            checkCropEnd.Enabled = isEnabled;
            numEndHour.Enabled = isEnabled;
            numEndMinute.Enabled = isEnabled;
            numEndSecond.Enabled = isEnabled;

            if (!onlyCrop)
            {
                radioJSON.Enabled = isEnabled;
                radioTXT.Enabled = isEnabled;
                btnDownload.Enabled = isEnabled;
            }
        }

        private async Task<JObject> GetClipInfo(object clipId)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Accept", "application/vnd.twitchtv.v5+json");
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.DownloadStringTaskAsync(String.Format("https://api.twitch.tv/kraken/clips/{0}", clipId));
                JObject result = JObject.Parse(response);
                return result;
            }
        }

        private async Task<JObject> GetVodInfo(string id)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.DownloadStringTaskAsync("https://api.twitch.tv/helix/videos?id=" + id);
                JObject result = JObject.Parse(response);
                return result;
            }
        }

        private async Task<Bitmap> GetClipThumb(string thumbUrl)
        {
            Bitmap result = new Bitmap(100, 100);
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                using (Stream s = await client.OpenReadTaskAsync(thumbUrl))
                {
                    result = new Bitmap(s);
                }
            }
            return result;
        }

        private void BtnDownload_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            if (radioJSON.Checked)
                saveFileDialog.Filter = "JSON Files | *.json";
            else
                saveFileDialog.Filter = "TXT Files | *.txt";

            saveFileDialog.RestoreDirectory = true;

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                ChatDownloadInfo info;
                if (isVod)
                {
                    int startTime = 0;
                    int duration = 0;

                    if (checkCropStart.Checked)
                    {
                        TimeSpan start = new TimeSpan((int)numStartHour.Value, (int)numStartMinute.Value, (int)numStartSecond.Value);
                        startTime = (int)Math.Round(start.TotalSeconds);
                    }

                    if (checkCropEnd.Checked)
                    {
                        TimeSpan end = new TimeSpan((int)numEndHour.Value, (int)numEndMinute.Value, (int)numEndSecond.Value);
                        duration = (int)Math.Ceiling(end.TotalSeconds - startTime);
                    }
                    else
                    {
                        TimeSpan vodLength = TimeSpan.Parse(Regex.Replace(videoData["data"][0]["duration"].ToString(), @"[^\d]", ":").TrimEnd(':'));
                        duration = (int)Math.Ceiling(vodLength.TotalSeconds);
                    }
                    info = new ChatDownloadInfo(isVod, textUrl.Text, saveFileDialog.FileName, videoData["data"][0]["id"].ToString(), startTime, duration, radioJSON.Checked, labelStreamer.Text, streamerId);
                }
                else
                    info = new ChatDownloadInfo(isVod, textUrl.Text, saveFileDialog.FileName, videoData["vod"]["id"].ToString(), videoData["vod"]["offset"].ToObject<int>(), videoData["duration"].ToObject<double>(), radioJSON.Checked, labelStreamer.Text, streamerId);
                toolStatus.Text = "Downloading";
                btnGetInfo.Enabled = false;
                SetEnabled(false, false);

                backgroundDownloadManager.RunWorkerAsync(info);
            }
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

                    int percent = (int)Math.Floor((latestMessage - videoStart)/videoDuration * 100);
                    backgroundDownloadManager.ReportProgress(percent, String.Format("Downloading {0}%", percent));

                    if (isFirst)
                        isFirst = false;

                }

                result["streamer"] = streamer;
                result["comments"] = comments;

                using (StreamWriter sw = new StreamWriter(clipInfo.path))
                {
                    if (clipInfo.is_json)
                    {
                        sw.Write(result.ToString(Newtonsoft.Json.Formatting.Indented));
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
                }
            }
        }

        private void BackgroundDownloadManager_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            toolStatus.Text = "Done";
            btnGetInfo.Enabled = true;
        }

        private void BackgroundDownloadManager_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string message = (string)e.UserState;
            toolStatus.Text = message;
            toolProgressBar.Value = e.ProgressPercentage >= 100 ? 100 : e.ProgressPercentage;
        }
    }
}

public class ChatDownloadInfo
{
    public bool is_vod { get; set; }
    public string id { get; set; }
    public string path { get; set; }
    public string vod_id { get; set; }
    public int offset { get; set; }
    public double duration { get; set; }
    public bool is_json { get; set; }
    public string streamer_name { get; set; }
    public int streamer_id { get; set; }

    public ChatDownloadInfo(bool Is_vod, string Id, string Path, string Vod_id, int Offset , double Duration, bool Is_json, string Streamer_name, int Streamer_id)
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