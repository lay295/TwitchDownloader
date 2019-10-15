using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TwitchDownloaderWPF
{
    public class InfoHelper
    {
        public static async Task<BitmapImage> GetThumb(string thumbUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                BitmapImage img = new BitmapImage();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.BeginInit();
                img.StreamSource = await client.GetStreamAsync(thumbUrl);
                img.EndInit();
                return img;
            }
        }

        public static async Task<JObject> GetVideoInfo(int videoId)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.DownloadStringTaskAsync("https://api.twitch.tv/helix/videos?id=" + videoId);
                JObject result = JObject.Parse(response);
                return result;
            }
        }

        public static async Task<JObject> GetVideoToken(int videoId)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.DownloadStringTaskAsync(String.Format("https://api.twitch.tv/api/vods/{0}/access_token", videoId));
                JObject result = JObject.Parse(response);
                return result;
            }
        }

        public static async Task<string[]> GetVideoPlaylist(int videoId, string token, string sig)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string playlist = await client.DownloadStringTaskAsync(String.Format("http://usher.twitch.tv/vod/{0}?nauth={1}&nauthsig={2}&allow_source=true&player=twitchweb", videoId, token, sig));
                return playlist.Split('\n');
            }
        }

        public static async Task<JObject> GetClipInfo(object clipId)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.DownloadStringTaskAsync(String.Format("https://api.twitch.tv/helix/clips?id={0}", clipId));
                JObject result = JObject.Parse(response);
                return result;
            }
        }

        public static async Task<JObject> GetClipInfoChat(object clipId)
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

        public static async Task<JObject> GetClipLinks(object clipId)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                //API is deprecated - hopefully keeps working for a while. Can genereate full url from thumbnail but fails ocasionally https://discuss.dev.twitch.tv/t/clips-api-does-not-expose-video-url/15763/2
                string response = await client.DownloadStringTaskAsync(String.Format("https://clips.twitch.tv/api/v2/clips/{0}/status", clipId));
                JObject result = JObject.Parse(response);
                return result;
            }
        }
    }
}
