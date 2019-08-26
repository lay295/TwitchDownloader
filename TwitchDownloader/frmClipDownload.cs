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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TwitchDownloader
{
    public partial class frmClipDownload : Form
    {
        public frmClipDownload()
        {
            InitializeComponent();
        }

        private async void BtnGetInfo_Click(object sender, EventArgs e)
        {
            if (!textUrl.Text.All(char.IsLetter) || textUrl.Text.Length == 0)
            {
                MessageBox.Show("Please enter a valid Clip Slug. For example https://clips.twitch.tv/FrailOutstandingCroissantPhilosoraptor would be FrailOutstandingCroissantPhilosoraptor", "Invalid Clip Slug", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                btnGetInfo.Enabled = false;

                string clipId = textUrl.Text;
                Task<JObject> taskInfo = GetInfo(clipId);
                Task<JObject> taskLinks = GetLinks(clipId);
                await Task.WhenAll(taskInfo, taskLinks);

                JToken clipData = taskInfo.Result["data"][0];
                string thumbUrl = clipData["thumbnail_url"].ToString();
                Task<Bitmap> taskThumb = GetThumb(thumbUrl);
                await Task.WhenAll(taskThumb);

                pictureThumb.Image = taskThumb.Result;
                labelStreamer.Text = clipData["broadcaster_name"].ToString();
                labelCreated.Text = clipData["created_at"].ToString();
                textTitle.Text = clipData["title"].ToString();

                foreach (var quality in taskLinks.Result["quality_options"])
                {
                    comboQuality.Items.Add(new TwitchClip(quality["quality"].ToString(), quality["frame_rate"].ToString(), quality["source"].ToString()));
                }

                comboQuality.SelectedIndex = 0;
                comboQuality.Enabled = true;
                btnDownload.Enabled = true;
                btnGetInfo.Enabled = true;
            }
            catch (WebException)
            {
                MessageBox.Show("Unable to get Clip information. Please double check Clip Slug and try again", "Unable to get info", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnGetInfo.Enabled = true;
            }
        }

        private async Task<Bitmap> GetThumb(string thumbUrl)
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

        private async Task<JObject> GetInfo(object clipId)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.DownloadStringTaskAsync(String.Format("https://api.twitch.tv/helix/clips?id={0}", clipId));
                JObject result = JObject.Parse(response);
                return result;
            }
        }

        private async Task<JObject> GetLinks(object clipId)
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                //API is deprecated - hopefully keeps working for a while. Can genereate full url from thumbnail but fails ocasionally https://discuss.dev.twitch.tv/t/clips-api-does-not-expose-video-url/15763/2
                string response = await client.DownloadStringTaskAsync(String.Format("https://clips.twitch.tv/api/v2/clips/{0}/status", clipId));
                JObject result = JObject.Parse(response);
                return result;
            }
        }

        private void BtnDownload_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            saveFileDialog.Filter = "MP4 Files | *.mp4";
            saveFileDialog.RestoreDirectory = true;

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                DownloadInfo info = new DownloadInfo((TwitchClip)comboQuality.SelectedItem, saveFileDialog.FileName);
                toolStatus.Text = "Downloading";
                btnGetInfo.Enabled = false;
                comboQuality.Enabled = false;

                backgroundDownloadManager.RunWorkerAsync(info);
            }
        }

        private void BackgroundDownloadManager_DoWork(object sender, DoWorkEventArgs e)
        {
            DownloadInfo clipInfo = (DownloadInfo)e.Argument;

            using (WebClient client = new WebClient())
            {
                client.DownloadFile(clipInfo.clip.url, clipInfo.path);
            }
        }

        private void BackgroundDownloadManager_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            toolStatus.Text = "Done Downloading";
            btnGetInfo.Enabled = true;
        }
    }
}

public class TwitchClip
{
    public string quality { get; set; }
    public string framerate { get; set; }
    public string url { get; set; }

    public TwitchClip(string Quality, string Framerate, string Url)
    {
        quality = Quality;
        framerate = Framerate;
        url = Url;
    }

    override
    public string ToString()
    {
        //Only show framerate if it's not 30fps
        return String.Format("{0}p{1}", quality, framerate == "30" ? "" : framerate);
    }
}

public class DownloadInfo
{
    public TwitchClip clip { get; set; }
    public string path { get; set; }

    public DownloadInfo(TwitchClip Clip, string Path)
    {
        clip = Clip;
        path = Path;
    }
}