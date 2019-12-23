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

        public static async Task<JArray> GetClipLinks(object clipId)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.UploadStringTaskAsync(new Uri("https://gql.twitch.tv/gql", UriKind.Absolute), "[{\"operationName\":\"VideoAccessToken_Clip\",\"variables\":{\"slug\":\"" + clipId + "\"},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"9bfcc0177bffc730bd5a5a89005869d2773480cf1738c592143b5173634b7d15\"}}}]");
                JArray result = JArray.Parse(response);
                return result;
            }
        }
    }
}
