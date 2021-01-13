using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public static class TwitchHelper
    {
        public static async Task<JObject> GetVideoInfo(int videoId)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Accept", "application/vnd.twitchtv.v5+json");
                client.Headers.Add("Client-ID", "v8kfhyc2980it9e7t5hhc7baukzuj2");
                string response = await client.DownloadStringTaskAsync("https://api.twitch.tv/kraken/videos/" + videoId);
                JObject result = JObject.Parse(response);
                return result;
            }
        }

        public static async Task<JObject> GetVideoToken(int videoId, string authToken)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                if (authToken != null && authToken != "")
                    client.Headers.Add("Authorization", "OAuth " + authToken);
                string response = await client.UploadStringTaskAsync("https://gql.twitch.tv/gql", "{\"operationName\":\"PlaybackAccessToken_Template\",\"query\":\"query PlaybackAccessToken_Template($login: String!, $isLive: Boolean!, $vodID: ID!, $isVod: Boolean!, $playerType: String!) {  streamPlaybackAccessToken(channelName: $login, params: {platform: \\\"web\\\", playerBackend: \\\"mediaplayer\\\", playerType: $playerType}) @include(if: $isLive) {    value    signature    __typename  }  videoPlaybackAccessToken(id: $vodID, params: {platform: \\\"web\\\", playerBackend: \\\"mediaplayer\\\", playerType: $playerType}) @include(if: $isVod) {    value    signature    __typename  }}\",\"variables\":{\"isLive\":false,\"login\":\"\",\"isVod\":true,\"vodID\":\"" + videoId + "\",\"playerType\":\"embed\"}}");
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
                client.Headers.Add("Accept", "application/vnd.twitchtv.v5+json");
                client.Headers.Add("Client-ID", "v8kfhyc2980it9e7t5hhc7baukzuj2");
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

        public static List<ThirdPartyEmote> GetThirdPartyEmotes(int streamerId, string cacheFolder, Emotes embededEmotes = null, bool bttv = true, bool ffz = true)
        {
            List<ThirdPartyEmote> returnList = new List<ThirdPartyEmote>();
            List<string> alreadyAdded = new List<string>();

            string bttvFolder = Path.Combine(cacheFolder, "bttv");
            string ffzFolder = Path.Combine(cacheFolder, "ffz");

            if (embededEmotes != null)
            {
                foreach (ThirdPartyEmoteData emoteData in embededEmotes.thirdParty)
                {
                    try
                    {
                        MemoryStream ms = new MemoryStream(emoteData.data);
                        SKCodec codec = SKCodec.Create(ms);
                        ThirdPartyEmote newEmote = new ThirdPartyEmote(new List<SKBitmap>() { SKBitmap.Decode(emoteData.data) }, codec, emoteData.name, codec.FrameCount == 0 ? "png" : "gif", "", emoteData.imageScale, emoteData.data);
                        returnList.Add(newEmote);
                        alreadyAdded.Add(emoteData.name);
                    }
                    catch { }
                }
            }

            using (WebClient client = new WebClient())
            {
                if (bttv)
                {
                    if (!Directory.Exists(bttvFolder))
                        Directory.CreateDirectory(bttvFolder);

                    //Global BTTV Emotes
                    JArray BBTV = JArray.Parse(client.DownloadString("https://api.betterttv.net/3/cached/emotes/global"));
                    foreach (var emote in BBTV)
                    {
                        string id = emote["id"].ToString();
                        string name = emote["code"].ToString();
                        if (alreadyAdded.Contains(name))
                            continue;
                        byte[] bytes;
                        string fileName = Path.Combine(bttvFolder, id + "_2x.png");
                        if (File.Exists(fileName))
                            bytes = File.ReadAllBytes(fileName);
                        else
                        {
                            bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/emote/{0}/2x", id));
                            File.WriteAllBytes(fileName, bytes);
                        }

                        MemoryStream ms = new MemoryStream(bytes);
                        returnList.Add(new ThirdPartyEmote(new List<SKBitmap>() { SKBitmap.Decode(bytes) }, SKCodec.Create(ms), name, emote["imageType"].ToString(), id, 2, bytes));
                        alreadyAdded.Add(name);
                    }

                    //Channel specific BTTV emotes
                    try
                    {
                        JObject BBTV_channel = JObject.Parse(client.DownloadString("https://api.betterttv.net/3/cached/users/twitch/" + streamerId));
                        foreach (var emote in BBTV_channel["sharedEmotes"])
                        {
                            string id = emote["id"].ToString();
                            string name = emote["code"].ToString();
                            if (alreadyAdded.Contains(name))
                                continue;
                            byte[] bytes;
                            string fileName = Path.Combine(bttvFolder, id + "_2x.png");
                            if (File.Exists(fileName))
                                bytes = File.ReadAllBytes(fileName);
                            else
                            {
                                bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/emote/{0}/2x", id));
                                File.WriteAllBytes(fileName, bytes);
                            }
                            MemoryStream ms = new MemoryStream(bytes);
                            returnList.Add(new ThirdPartyEmote(new List<SKBitmap>() { SKBitmap.Decode(bytes) }, SKCodec.Create(ms), name, emote["imageType"].ToString(), id, 2, bytes));
                            alreadyAdded.Add(name);
                        }
                        foreach (var emote in BBTV_channel["channelEmotes"])
                        {
                            string id = emote["id"].ToString();
                            string name = emote["code"].ToString();
                            if (alreadyAdded.Contains(name))
                                continue;
                            byte[] bytes;
                            string fileName = Path.Combine(bttvFolder, id + "_2x.png");
                            if (File.Exists(fileName))
                                bytes = File.ReadAllBytes(fileName);
                            else
                            {
                                bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/emote/{0}/2x", id));
                                File.WriteAllBytes(fileName, bytes);
                            }
                            MemoryStream ms = new MemoryStream(bytes);
                            returnList.Add(new ThirdPartyEmote(new List<SKBitmap>() { SKBitmap.Decode(bytes) }, SKCodec.Create(ms), name, emote["imageType"].ToString(), id, 2, bytes));
                            alreadyAdded.Add(name);
                        }
                    }
                    catch { }
                }

                if (ffz)
                {
                    if (!Directory.Exists(ffzFolder))
                        Directory.CreateDirectory(ffzFolder);

                    //Global FFZ emotes
                    JArray FFZ = JArray.Parse(client.DownloadString("https://api.betterttv.net/3/cached/frankerfacez/emotes/global"));
                    foreach (var emote in FFZ)
                    {
                        string id = emote["id"].ToString();
                        string name = emote["code"].ToString();
                        if (alreadyAdded.Contains(name))
                            continue;
                        byte[] bytes;
                        string fileName = Path.Combine(ffzFolder, id + "_1x.png");
                        if (File.Exists(fileName))
                            bytes = File.ReadAllBytes(fileName);
                        else
                        {
                            bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/1", id));
                            File.WriteAllBytes(fileName, bytes);
                        }
                        MemoryStream ms = new MemoryStream(bytes);
                        returnList.Add(new ThirdPartyEmote(new List<SKBitmap>() { SKBitmap.Decode(bytes) }, SKCodec.Create(ms), name, emote["imageType"].ToString(), id, 1, bytes));
                        alreadyAdded.Add(name);
                    }

                    //Channel specific FFZ emotes
                    try
                    {
                        JArray FFZ_channel = JArray.Parse(client.DownloadString("https://api.betterttv.net/3/cached/frankerfacez/users/twitch/" + streamerId));
                        foreach (var emote in FFZ_channel)
                        {
                            string id = emote["id"].ToString();
                            string name = emote["code"].ToString();
                            if (alreadyAdded.Contains(name))
                                continue;
                            byte[] bytes;
                            int scale = 2;
                            string fileName = Path.Combine(ffzFolder, id + "_2x.png");
                            try
                            {
                                if (File.Exists(fileName))
                                    bytes = File.ReadAllBytes(fileName);
                                else
                                {
                                    bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/2", id));
                                    File.WriteAllBytes(fileName, bytes);
                                }
                            }
                            catch
                            {
                                fileName = Path.Combine(ffzFolder, id + "_1x.png");
                                if (File.Exists(fileName))
                                    bytes = File.ReadAllBytes(fileName);
                                else
                                {
                                    bytes = client.DownloadData(String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/1", id));
                                    File.WriteAllBytes(fileName, bytes);
                                }
                                scale = 1;
                            }
                            MemoryStream ms = new MemoryStream(bytes);
                            returnList.Add(new ThirdPartyEmote(new List<SKBitmap>() { SKBitmap.Decode(bytes) }, SKCodec.Create(ms), name, emote["imageType"].ToString(), id, scale, bytes));
                            alreadyAdded.Add(name);
                        }
                    }
                    catch { }
                }
            }

            return returnList;
        }

        public static Dictionary<string, SKBitmap> GetEmotes(List<Comment> comments, string cacheFolder, Emotes embededEmotes = null, bool deepSearch = false)
        {
            Dictionary<string, SKBitmap> returnDictionary = new Dictionary<string, SKBitmap>();
            List<string> alreadyAdded = new List<string>();
            List<string> failedEmotes = new List<string>();

            string emoteFolder = Path.Combine(cacheFolder, "emotes");
            if (!Directory.Exists(emoteFolder))
                Directory.CreateDirectory(emoteFolder);

            if (embededEmotes != null)
            {
                foreach (FirstPartyEmoteData emoteData in embededEmotes.firstParty)
                {
                    try
                    {
                        if (!returnDictionary.ContainsKey(emoteData.id))
                        {
                            returnDictionary.Add(emoteData.id, SKBitmap.Decode(emoteData.data));
                            alreadyAdded.Add(emoteData.id);
                        }
                    }
                    catch { }
                }
            }

            using (WebClient client = new WebClient())
            {
                foreach (var comment in comments)
                {
                    if (comment.message.fragments == null)
                        continue;

                    foreach (var fragment in comment.message.fragments)
                    {
                        if (fragment.emoticon != null)
                        {
                            string id = fragment.emoticon.emoticon_id;
                            if (!alreadyAdded.Contains(id) && !failedEmotes.Contains(id))
                            {
                                try
                                {
                                    string filePath = Path.Combine(emoteFolder, id + "_1x.png");

                                    if (File.Exists(filePath))
                                    {
                                        SKBitmap emoteImage = SKBitmap.Decode(filePath);
                                        if (emoteImage == null)
                                        {
                                            try
                                            {
                                                File.Delete(filePath);
                                            }
                                            catch { }
                                        }
                                        else
                                        {
                                            returnDictionary.Add(id, emoteImage);
                                            alreadyAdded.Add(id);
                                        }
                                    }

                                    if (!alreadyAdded.Contains(id))
                                    {
                                        byte[] bytes = client.DownloadData(String.Format("https://static-cdn.jtvnw.net/emoticons/v1/{0}/1.0", id));
                                        alreadyAdded.Add(id);
                                        MemoryStream ms = new MemoryStream(bytes);
                                        SKBitmap emoteImage = SKBitmap.Decode(ms);
                                        returnDictionary.Add(id, emoteImage);
                                        File.WriteAllBytes(filePath, bytes);
                                    }
                                }
                                catch (WebException)
                                {
                                    string emoteName = fragment.text;
                                    bool foundEmote = false;

                                    if (deepSearch)
                                    {
                                        //lets try waybackmachine, very slow though :(
                                        try
                                        {
                                            for (int i = 1; i <= 3; i++)
                                            {
                                                JObject response = JObject.Parse(client.DownloadString($"https://archive.org/wayback/available?url=https://static-cdn.jtvnw.net/emoticons/v1/{id}/{i}.0/"));
                                                if (response["archived_snapshots"]["closest"] != null && response["archived_snapshots"]["closest"]["available"].ToObject<bool>() == true)
                                                {
                                                    string filePath = Path.Combine(emoteFolder, id + "_1x.png");
                                                    byte[] bytes = client.DownloadData(response["archived_snapshots"]["closest"]["url"].ToString().Replace("/https://static-cdn.jtvnw.net", "if_/https://static-cdn.jtvnw.net"));
                                                    MemoryStream ms = new MemoryStream(bytes);
                                                    SKBitmap emoteImage = SKBitmap.Decode(ms);
                                                    SKBitmap emoteImageScaled = new SKBitmap(28, 28);
                                                    emoteImage.ScalePixels(emoteImageScaled, SKFilterQuality.High);
                                                    alreadyAdded.Add(id);
                                                    returnDictionary.Add(id, emoteImageScaled);
                                                    emoteImage.Dispose();
                                                    emoteImageScaled.Encode(SKEncodedImageFormat.Png, 100).SaveTo(new FileStream(filePath, FileMode.Create));
                                                    foundEmote = true;
                                                    break;
                                                }
                                            }
                                        }
                                        catch { }


                                        if (foundEmote)
                                            continue;
                                        else
                                        {
                                            //sometimes emote still exists but id is different, I use twitch metrics because I can't find an api to find an emote by name
                                            try
                                            {
                                                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://www.twitchmetrics.net/e/" + emoteName);
                                                request.AllowAutoRedirect = false;
                                                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                                                string redirUrl = response.Headers["Location"];
                                                response.Close();
                                                string newId = redirUrl.Split('/').Last().Split('-').First();
                                                byte[] bytes = client.DownloadData(String.Format("https://static-cdn.jtvnw.net/emoticons/v1/{0}/1.0", newId));
                                                string filePath = Path.Combine(emoteFolder, id + "_1x.png");
                                                File.WriteAllBytes(filePath, bytes);
                                                alreadyAdded.Add(id);
                                                MemoryStream ms = new MemoryStream(bytes);
                                                SKBitmap emoteImage = SKBitmap.Decode(ms);
                                                returnDictionary.Add(id, emoteImage);
                                                foundEmote = true;
                                            }
                                            catch
                                            {

                                            }
                                        }
                                    }
                                    if (!foundEmote)
                                    {
                                        failedEmotes.Add(id);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return returnDictionary.Where(x => x.Value != null).ToDictionary(z => z.Key, z => z.Value);
        }

        public static List<ChatBadge> GetChatBadges(int streamerId)
        {
            List<ChatBadge> chatBadges = new List<ChatBadge>();
            using (WebClient client = new WebClient())
            {
                //Global chat badges
                JObject globalBadges = JObject.Parse(client.DownloadString("https://badges.twitch.tv/v1/badges/global/display"));
                //Subscriber badges
                JObject subBadges = JObject.Parse(client.DownloadString(String.Format("https://badges.twitch.tv/v1/badges/channels/{0}/display", streamerId)));

                foreach (var badge in globalBadges["badge_sets"].Union(subBadges["badge_sets"]))
                {
                    JProperty jBadgeProperty = badge.ToObject<JProperty>();
                    string name = jBadgeProperty.Name;
                    Dictionary<string, SKBitmap> versions = new Dictionary<string, SKBitmap>();

                    foreach (var version in badge.First["versions"])
                    {
                        JProperty jVersionProperty = version.ToObject<JProperty>();
                        string versionString = jVersionProperty.Name;
                        string downloadUrl = version.First["image_url_2x"].ToString();
                        byte[] bytes = client.DownloadData(downloadUrl);
                        MemoryStream ms = new MemoryStream(bytes);
                        try
                        {
                            //For some reason, twitch has corrupted images sometimes :) for example
                            //https://static-cdn.jtvnw.net/badges/v1/a9811799-dce3-475f-8feb-3745ad12b7ea/1
                            SKBitmap badgeImage = SKBitmap.Decode(ms);
                            versions.Add(versionString, badgeImage);
                        }
                        catch (ArgumentException)
                        { }
                    }

                    chatBadges.Add(new ChatBadge() { Name = name, Versions = versions });
                }

                try
                {
                    byte[] bytes = client.DownloadData("https://cdn.betterttv.net/emote/58493695987aab42df852e0f/2x");
                    MemoryStream ms = new MemoryStream(bytes);
                    SKBitmap badgeImage = SKBitmap.Decode(ms);
                    SKBitmap scaledBitmap = new SKBitmap(36, 36);
                    using (SKCanvas canvas = new SKCanvas(scaledBitmap))
                    {
                        canvas.DrawBitmap(badgeImage, new SKRect(0, 0, 36, 36), new SKPaint());
                    }
                    chatBadges.Add(new ChatBadge() { Name = "ilovekeepo69", Versions = new Dictionary<string, SKBitmap>() { { "1", scaledBitmap } } });
                }
                catch { }
            }

            return chatBadges;
        }

        public static Dictionary<string, SKBitmap> GetTwitterEmojis(List<Comment> comments, string cacheFolder)
        {
            Dictionary<string, SKBitmap> emojiCache = new Dictionary<string, SKBitmap>();
            string emojiRegex = "[#*0-9]\uFE0F\u20E3|[\u00A9\u00AE\u203C\u2049\u2122\u2139\u2194-\u2199\u21A9\u21AA\u231A\u231B\u2328\u23CF\u23E9-\u23F3\u23F8-\u23FA\u24C2\u25AA\u25AB\u25B6\u25C0\u25FB-\u25FE\u2600-\u2604\u260E\u2611\u2614\u2615\u2618]|\u261D(?:\uD83C[\uDFFB-\uDFFF])?|[\u2620\u2622\u2623\u2626\u262A\u262E\u262F\u2638-\u263A\u2640\u2642\u2648-\u2653\u265F\u2660\u2663\u2665\u2666\u2668\u267B\u267E\u267F\u2692-\u2697\u2699\u269B\u269C\u26A0\u26A1\u26AA\u26AB\u26B0\u26B1\u26BD\u26BE\u26C4\u26C5\u26C8\u26CE\u26CF\u26D1\u26D3\u26D4\u26E9\u26EA\u26F0-\u26F5\u26F7\u26F8]|\u26F9(?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\u26FA\u26FD\u2702\u2705\u2708\u2709]|[\u270A-\u270D](?:\uD83C[\uDFFB-\uDFFF])?|[\u270F\u2712\u2714\u2716\u271D\u2721\u2728\u2733\u2734\u2744\u2747\u274C\u274E\u2753-\u2755\u2757\u2763\u2764\u2795-\u2797\u27A1\u27B0\u27BF\u2934\u2935\u2B05-\u2B07\u2B1B\u2B1C\u2B50\u2B55\u3030\u303D\u3297\u3299]|\uD83C(?:[\uDC04\uDCCF\uDD70\uDD71\uDD7E\uDD7F\uDD8E\uDD91-\uDD9A]|\uDDE6\uD83C[\uDDE8-\uDDEC\uDDEE\uDDF1\uDDF2\uDDF4\uDDF6-\uDDFA\uDDFC\uDDFD\uDDFF]|\uDDE7\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEF\uDDF1-\uDDF4\uDDF6-\uDDF9\uDDFB\uDDFC\uDDFE\uDDFF]|\uDDE8\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDEE\uDDF0-\uDDF5\uDDF7\uDDFA-\uDDFF]|\uDDE9\uD83C[\uDDEA\uDDEC\uDDEF\uDDF0\uDDF2\uDDF4\uDDFF]|\uDDEA\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDED\uDDF7-\uDDFA]|\uDDEB\uD83C[\uDDEE-\uDDF0\uDDF2\uDDF4\uDDF7]|\uDDEC\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEE\uDDF1-\uDDF3\uDDF5-\uDDFA\uDDFC\uDDFE]|\uDDED\uD83C[\uDDF0\uDDF2\uDDF3\uDDF7\uDDF9\uDDFA]|\uDDEE\uD83C[\uDDE8-\uDDEA\uDDF1-\uDDF4\uDDF6-\uDDF9]|\uDDEF\uD83C[\uDDEA\uDDF2\uDDF4\uDDF5]|\uDDF0\uD83C[\uDDEA\uDDEC-\uDDEE\uDDF2\uDDF3\uDDF5\uDDF7\uDDFC\uDDFE\uDDFF]|\uDDF1\uD83C[\uDDE6-\uDDE8\uDDEE\uDDF0\uDDF7-\uDDFB\uDDFE]|\uDDF2\uD83C[\uDDE6\uDDE8-\uDDED\uDDF0-\uDDFF]|\uDDF3\uD83C[\uDDE6\uDDE8\uDDEA-\uDDEC\uDDEE\uDDF1\uDDF4\uDDF5\uDDF7\uDDFA\uDDFF]|\uDDF4\uD83C\uDDF2|\uDDF5\uD83C[\uDDE6\uDDEA-\uDDED\uDDF0-\uDDF3\uDDF7-\uDDF9\uDDFC\uDDFE]|\uDDF6\uD83C\uDDE6|\uDDF7\uD83C[\uDDEA\uDDF4\uDDF8\uDDFA\uDDFC]|\uDDF8\uD83C[\uDDE6-\uDDEA\uDDEC-\uDDF4\uDDF7-\uDDF9\uDDFB\uDDFD-\uDDFF]|\uDDF9\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDED\uDDEF-\uDDF4\uDDF7\uDDF9\uDDFB\uDDFC\uDDFF]|\uDDFA\uD83C[\uDDE6\uDDEC\uDDF2\uDDF3\uDDF8\uDDFE\uDDFF]|\uDDFB\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDEE\uDDF3\uDDFA]|\uDDFC\uD83C[\uDDEB\uDDF8]|\uDDFD\uD83C\uDDF0|\uDDFE\uD83C[\uDDEA\uDDF9]|\uDDFF\uD83C[\uDDE6\uDDF2\uDDFC]|[\uDE01\uDE02\uDE1A\uDE2F\uDE32-\uDE3A\uDE50\uDE51\uDF00-\uDF21\uDF24-\uDF84]|\uDF85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDF86-\uDF93\uDF96\uDF97\uDF99-\uDF9B\uDF9E-\uDFC1]|\uDFC2(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC3\uDFC4](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDFC5\uDFC6]|\uDFC7(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC8\uDFC9]|\uDFCA(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDFCB\uDFCC](?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\uDFCD-\uDFF0]|\uDFF3(?:\uFE0F\u200D\uD83C\uDF08)?|\uDFF4(?:\u200D\u2620\uFE0F|\uDB40\uDC67\uDB40\uDC62\uDB40(?:\uDC65\uDB40\uDC6E\uDB40\uDC67|\uDC73\uDB40\uDC63\uDB40\uDC74|\uDC77\uDB40\uDC6C\uDB40\uDC73)\uDB40\uDC7F)?|[\uDFF5\uDFF7-\uDFFF])|\uD83D(?:[\uDC00-\uDC14]|\uDC15(?:\u200D\uD83E\uDDBA)?|[\uDC16-\uDC40]|\uDC41(?:\uFE0F\u200D\uD83D\uDDE8\uFE0F)?|[\uDC42\uDC43](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC44\uDC45]|[\uDC46-\uDC50](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC51-\uDC65]|[\uDC66\uDC67](?:\uD83C[\uDFFB-\uDFFF])?|\uDC68(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\u2764\uFE0F\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|[\uDC68\uDC69]\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD]))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C\uDFFB|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFC]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFD]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC69(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\u2764\uFE0F\u200D\uD83D(?:\uDC8B\u200D\uD83D)?[\uDC68\uDC69]|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|\uDC69\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFC-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB\uDFFD-\uDFFF]|\uDC69\uD83C\uDFFB)|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|\uDC69\uD83C[\uDFFB\uDFFC])|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D(?:\uDC68\uD83C[\uDFFB-\uDFFD\uDFFF]|\uDC69\uD83C[\uDFFB-\uDFFD])|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F|\uD83C[\uDF3E\uDF73\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC6A|[\uDC6B-\uDC6D](?:\uD83C[\uDFFB-\uDFFF])?|\uDC6E(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC6F(?:\u200D[\u2640\u2642]\uFE0F)?|\uDC70(?:\uD83C[\uDFFB-\uDFFF])?|\uDC71(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC72(?:\uD83C[\uDFFB-\uDFFF])?|\uDC73(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDC74-\uDC76](?:\uD83C[\uDFFB-\uDFFF])?|\uDC77(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC78(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC79-\uDC7B]|\uDC7C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC7D-\uDC80]|[\uDC81\uDC82](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDC83(?:\uD83C[\uDFFB-\uDFFF])?|\uDC84|\uDC85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC86\uDC87](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDC88-\uDCA9]|\uDCAA(?:\uD83C[\uDFFB-\uDFFF])?|[\uDCAB-\uDCFD\uDCFF-\uDD3D\uDD49-\uDD4E\uDD50-\uDD67\uDD6F\uDD70\uDD73]|\uDD74(?:\uD83C[\uDFFB-\uDFFF])?|\uDD75(?:\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?|\uFE0F\u200D[\u2640\u2642]\uFE0F)?|[\uDD76-\uDD79]|\uDD7A(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD87\uDD8A-\uDD8D]|[\uDD90\uDD95\uDD96](?:\uD83C[\uDFFB-\uDFFF])?|[\uDDA4\uDDA5\uDDA8\uDDB1\uDDB2\uDDBC\uDDC2-\uDDC4\uDDD1-\uDDD3\uDDDC-\uDDDE\uDDE1\uDDE3\uDDE8\uDDEF\uDDF3\uDDFA-\uDE44]|[\uDE45-\uDE47](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDE48-\uDE4A]|\uDE4B(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDE4C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE4D\uDE4E](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDE4F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE80-\uDEA2]|\uDEA3(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDEA4-\uDEB3]|[\uDEB4-\uDEB6](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDEB7-\uDEBF]|\uDEC0(?:\uD83C[\uDFFB-\uDFFF])?|[\uDEC1-\uDEC5\uDECB]|\uDECC(?:\uD83C[\uDFFB-\uDFFF])?|[\uDECD-\uDED2\uDED5\uDEE0-\uDEE5\uDEE9\uDEEB\uDEEC\uDEF0\uDEF3-\uDEFA\uDFE0-\uDFEB])|\uD83E(?:[\uDD0D\uDD0E]|\uDD0F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD10-\uDD17]|[\uDD18-\uDD1C](?:\uD83C[\uDFFB-\uDFFF])?|\uDD1D|[\uDD1E\uDD1F](?:\uD83C[\uDFFB-\uDFFF])?|[\uDD20-\uDD25]|\uDD26(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD27-\uDD2F]|[\uDD30-\uDD36](?:\uD83C[\uDFFB-\uDFFF])?|\uDD37(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD38\uDD39](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDD3A|\uDD3C(?:\u200D[\u2640\u2642]\uFE0F)?|[\uDD3D\uDD3E](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDD3F-\uDD45\uDD47-\uDD71\uDD73-\uDD76\uDD7A-\uDDA2\uDDA5-\uDDAA\uDDAE-\uDDB4]|[\uDDB5\uDDB6](?:\uD83C[\uDFFB-\uDFFF])?|\uDDB7|[\uDDB8\uDDB9](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDDBA|\uDDBB(?:\uD83C[\uDFFB-\uDFFF])?|[\uDDBC-\uDDCA]|[\uDDCD-\uDDCF](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|\uDDD0|\uDDD1(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1|\uD83C(?:\uDFFB(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C\uDFFB)?|\uDFFC(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB\uDFFC])?|\uDFFD(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFD])?|\uDFFE(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFE])?|\uDFFF(?:\u200D\uD83E\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF])?))?|[\uDDD2-\uDDD5](?:\uD83C[\uDFFB-\uDFFF])?|\uDDD6(?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDDD7-\uDDDD](?:\u200D[\u2640\u2642]\uFE0F|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F)?)?|[\uDDDE\uDDDF](?:\u200D[\u2640\u2642]\uFE0F)?|[\uDDE0-\uDDFF\uDE70-\uDE73\uDE78-\uDE7A\uDE80-\uDE82\uDE90-\uDE95])";
            string emojiFolder = Path.Combine(cacheFolder, "emojis");
            if (!Directory.Exists(emojiFolder))
                Directory.CreateDirectory(emojiFolder);
            using (WebClient client = new WebClient())
            {
                foreach (var comment in comments)
                {
                    if (comment.message.fragments == null)
                        continue;

                    foreach (var fragment in comment.message.fragments)
                    {
                        if (fragment.emoticon == null)
                        {
                            string[] fragmentParts = fragment.text.Split(' ');
                            for (int i = 0; i < fragmentParts.Length; i++)
                            {
                                string output = fragmentParts[i].Trim();

                                if (output == "󠀀")
                                    continue;

                                MatchCollection matches = Regex.Matches(output, emojiRegex);
                                foreach (Match m in matches)
                                {
                                    if (m.Success)
                                    {
                                        for (var k = 0; k < m.Value.Length; k += char.IsSurrogatePair(m.Value, k) ? 2 : 1)
                                        {
                                            string codepoint = String.Format("{0:X4}", char.ConvertToUtf32(m.Value, k)).ToLower();
                                            codepoint = codepoint.Replace("fe0f", "");
                                            if (codepoint != "" && !emojiCache.ContainsKey(codepoint))
                                            {
                                                try
                                                {
                                                    byte[] bytes;
                                                    string fileName = Path.Combine(emojiFolder, codepoint + ".png");
                                                    if (File.Exists(fileName))
                                                        bytes = File.ReadAllBytes(fileName);
                                                    else
                                                    {
                                                        bytes = client.DownloadData(String.Format("https://abs.twimg.com/emoji/v2/72x72/{0}.png", codepoint));
                                                        File.WriteAllBytes(fileName, bytes);
                                                    }

                                                    SKBitmap emojiImage = SKBitmap.Decode(bytes);
                                                    emojiCache.Add(codepoint, emojiImage);
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return emojiCache;
        }

        public static List<CheerEmote> GetBits(string cacheFolder)
        {
            List<CheerEmote> cheerEmotes = new List<CheerEmote>();
            string bitsFolder = Path.Combine(cacheFolder, "bits");
            if (!Directory.Exists(bitsFolder))
                Directory.CreateDirectory(bitsFolder);

            using (WebClient client = new WebClient())
            {
                client.Headers.Add("Accept", "application/vnd.twitchtv.v5+json");
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                JObject globalCheer = JObject.Parse(client.DownloadString("https://api.twitch.tv/kraken/bits/actions"));

                foreach (JToken emoteToken in globalCheer["actions"])
                {
                    string prefix = emoteToken["prefix"].ToString();
                    List<KeyValuePair<int, ThirdPartyEmote>> tierList = new List<KeyValuePair<int, ThirdPartyEmote>>();
                    CheerEmote newEmote = new CheerEmote() { prefix = prefix, tierList = tierList };
                    byte[] finalBytes = null;
                    foreach (JToken tierToken in emoteToken["tiers"])
                    {
                        try
                        {
                            int minBits = tierToken["min_bits"].ToObject<int>();
                            string fileName = Path.Combine(bitsFolder, prefix + minBits + "_2x.gif");

                            if (File.Exists(fileName))
                            {
                                finalBytes = File.ReadAllBytes(fileName);
                            }
                            else
                            {
                                byte[] bytes = client.DownloadData(tierToken["images"]["dark"]["animated"]["2"].ToString());
                                File.WriteAllBytes(fileName, bytes);
                                finalBytes = bytes;
                            }

                            if (finalBytes != null)
                            {
                                MemoryStream ms = new MemoryStream(finalBytes);
                                ThirdPartyEmote emote = new ThirdPartyEmote(new List<SKBitmap>() { SKBitmap.Decode(finalBytes) }, SKCodec.Create(ms), prefix, "gif", "", 2, finalBytes);
                                tierList.Add(new KeyValuePair<int, ThirdPartyEmote>(minBits, emote));
                            }
                        }
                        catch
                        { }
                    }
                    cheerEmotes.Add(newEmote);
                }
            }

            return cheerEmotes;
        }

        public static int TimestampToSeconds(string input)
        {
            //There might be a better way to do this, gets string 0h0m0s and returns timespan
            TimeSpan returnSpan = new TimeSpan(0);
            string[] inputArray = input.Remove(input.Length - 1).Replace('h', ':').Replace('m', ':').Split(':');

            returnSpan = returnSpan.Add(TimeSpan.FromSeconds(Int32.Parse(inputArray[inputArray.Length - 1])));
            if (inputArray.Length > 1)
                returnSpan = returnSpan.Add(TimeSpan.FromMinutes(Int32.Parse(inputArray[inputArray.Length - 2])));
            if (inputArray.Length > 2)
                returnSpan = returnSpan.Add(TimeSpan.FromHours(Int32.Parse(inputArray[inputArray.Length - 3])));

            return (int)returnSpan.TotalSeconds;
        }

        public static string GetStreamerName(int id)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("Accept", "application/vnd.twitchtv.v5+json; charset=UTF-8");
                    client.Headers.Add("Client-Id", "v8kfhyc2980it9e7t5hhc7baukzuj2");

                    JObject response = JObject.Parse(client.DownloadString("https://api.twitch.tv/kraken/users/" + id));
                    return response["name"].ToString();
                }
            }
            catch { return ""; }
        }
    }
}
