using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public static class TwitchHelper
    {
        public static async Task<GqlVideoResponse> GetVideoInfo(int videoId)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.UploadStringTaskAsync(new Uri("https://gql.twitch.tv/gql", UriKind.Absolute), "{\"query\":\"query{video(id:\\\"" + videoId + "\\\"){title,thumbnailURLs(height:180,width:320),createdAt,lengthSeconds,owner{id,displayName}}}\",\"variables\":{}}");
                return JsonConvert.DeserializeObject<GqlVideoResponse>(response);
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

        public static async Task<GqlClipResponse> GetClipInfo(object clipId)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.UploadStringTaskAsync(new Uri("https://gql.twitch.tv/gql", UriKind.Absolute), "{\"query\":\"query{clip(slug:\\\"" + clipId + "\\\"){title,thumbnailURL,createdAt,durationSeconds,broadcaster{id,displayName},videoOffsetSeconds,video{id}}}\",\"variables\":{}}");
                return JsonConvert.DeserializeObject<GqlClipResponse>(response);
            }
        }

        public static async Task<JArray> GetClipLinks(object clipId)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await client.UploadStringTaskAsync(new Uri("https://gql.twitch.tv/gql", UriKind.Absolute), "[{\"operationName\":\"VideoAccessToken_Clip\",\"variables\":{\"slug\":\"" + clipId + "\"},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"36b89d2507fce29e5ca551df756d27c1cfe079e2609642b4390aa4c35796eb11\"}}}]");
                JArray result = JArray.Parse(response);
                return result;
            }
        }

        public static async Task<GqlVideoSearchResponse> GetGqlVideos(string channelName, string cursor = "", int limit = 50)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                string response = await client.UploadStringTaskAsync("https://gql.twitch.tv/gql", "{\"query\":\"query{user(login:\\\"" + channelName + "\\\"){videos(first: " + limit + "" + (cursor == "" ? "" : ",after:\\\"" + cursor + "\\\"") + ") { edges { node { title, id, lengthSeconds, previewThumbnailURL(height: 180, width: 320), createdAt, viewCount }, cursor }, pageInfo { hasNextPage, hasPreviousPage }, totalCount }}}\",\"variables\":{}}");
                GqlVideoSearchResponse result = JsonConvert.DeserializeObject<GqlVideoSearchResponse>(response);
                return result;
            }
        }

        public static async Task<GqlClipSearchResponse> GetGqlClips(string channelName, string period = "LAST_WEEK", string cursor = "", int limit = 50)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                string response = await client.UploadStringTaskAsync("https://gql.twitch.tv/gql", "{\"query\":\"query{user(login:\\\"" + channelName + "\\\"){clips(first: " + limit + ", after: \\\"" + cursor + "\\\", criteria: { period: " + period + " }) {  edges { cursor, node { id, slug, title, createdAt, durationSeconds, thumbnailURL, viewCount } }, pageInfo { hasNextPage, hasPreviousPage } }}}\",\"variables\":{}}");
                GqlClipSearchResponse result = JsonConvert.DeserializeObject<GqlClipSearchResponse>(response);
                return result;
            }
        }

        public static List<TwitchEmote> GetThirdPartyEmotes(int streamerId, string cacheFolder, Emotes embededEmotes = null, bool bttv = true, bool ffz = true, bool stv = true)
        {
            List<TwitchEmote> returnList = new List<TwitchEmote>();
            List<string> alreadyAdded = new List<string>();

            string bttvFolder = Path.Combine(cacheFolder, "bttv");
            string ffzFolder = Path.Combine(cacheFolder, "ffz");
            string stvFolder = Path.Combine(cacheFolder, "stv");

            if (embededEmotes != null)
            {
                foreach (ThirdPartyEmoteData emoteData in embededEmotes.thirdParty)
                {
                    try
                    {
                        MemoryStream ms = new MemoryStream(emoteData.data);
                        SKCodec codec = SKCodec.Create(ms);
                        TwitchEmote newEmote = new TwitchEmote(new List<SKBitmap>() { SKBitmap.Decode(emoteData.data) }, codec, emoteData.name, codec.FrameCount == 0 ? "png" : "gif", "", emoteData.imageScale, emoteData.data);
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
                        TwitchHelper.CreateDirectory(bttvFolder);

                    //Global BTTV Emotes
                    JArray BBTV = JArray.Parse(client.DownloadString("https://api.betterttv.net/3/cached/emotes/global"));
                    foreach (var emote in BBTV)
                    {
                        string id = emote["id"].ToString();
                        string name = emote["code"].ToString();
                        if (alreadyAdded.Contains(name))
                            continue;
                        string fileName = Path.Combine(bttvFolder, id + "_2x.png");
                        string url = String.Format("https://cdn.betterttv.net/emote/{0}/2x", id);

                        TwitchEmote newEmote = GetTwitchEmote(fileName, url, name, emote["imageType"].ToString(), id, 2);
                        if (newEmote != null)
                        {
                            returnList.Add(newEmote);
                            alreadyAdded.Add(name);
                        }
                    }

                    //Channel specific BTTV emotes
                    try
                    {
                        JObject BBTV_channel = JObject.Parse(client.DownloadString("https://api.betterttv.net/3/cached/users/twitch/" + streamerId));
                        foreach (var emote in BBTV_channel["sharedEmotes"])
                        {
                            string id = emote["id"].ToString();
                            string name = emote["code"].ToString();
                            string mime = emote["imageType"].ToString();
                            if (alreadyAdded.Contains(name))
                                continue;
                            string fileName = Path.Combine(bttvFolder, id + "_2x." + mime);
                            string url = String.Format("https://cdn.betterttv.net/emote/{0}/2x", id);
                            TwitchEmote newEmote = GetTwitchEmote(fileName, url, name, mime, id, 2);
                            if (newEmote != null)
                            {
                                returnList.Add(newEmote);
                                alreadyAdded.Add(name);
                            }
                        }
                        foreach (var emote in BBTV_channel["channelEmotes"])
                        {
                            string id = emote["id"].ToString();
                            string name = emote["code"].ToString();
                            string mime = emote["imageType"].ToString();
                            if (alreadyAdded.Contains(name))
                                continue;
                            string fileName = Path.Combine(bttvFolder, id + "_2x." + mime);
                            string url = String.Format("https://cdn.betterttv.net/emote/{0}/2x", id);
                            TwitchEmote newEmote = GetTwitchEmote(fileName, url, name, mime, id, 2);
                            if (newEmote != null)
                            {
                                returnList.Add(newEmote);
                                alreadyAdded.Add(name);
                            }
                        }
                    }
                    catch { }
                }

                if (ffz)
                {
                    if (!Directory.Exists(ffzFolder))
                        TwitchHelper.CreateDirectory(ffzFolder);

                    //Global FFZ emotes
                    JArray FFZ = JArray.Parse(client.DownloadString("https://api.betterttv.net/3/cached/frankerfacez/emotes/global"));
                    foreach (var emote in FFZ)
                    {
                        string id = emote["id"].ToString();
                        string name = emote["code"].ToString();
                        string mime = emote["imageType"].ToString();
                        if (alreadyAdded.Contains(name))
                            continue;
                        string fileName = Path.Combine(ffzFolder, id + "_1x." + mime);
                        string url = String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/1", id);
                        TwitchEmote newEmote = GetTwitchEmote(fileName, url, name, mime, id, 2);
                        if (newEmote != null)
                        {
                            returnList.Add(newEmote);
                            alreadyAdded.Add(name);
                        }
                    }

                    //Channel specific FFZ emotes
                    try
                    {
                        JArray FFZ_channel = JArray.Parse(client.DownloadString("https://api.betterttv.net/3/cached/frankerfacez/users/twitch/" + streamerId));
                        foreach (var emote in FFZ_channel)
                        {
                            string id = emote["id"].ToString();
                            string name = emote["code"].ToString();
                            string mime = emote["imageType"].ToString();
                            if (alreadyAdded.Contains(name))
                                continue;
                            string fileName = Path.Combine(ffzFolder, id + "_2x." + mime);
                            string fileNameLow = Path.Combine(ffzFolder, id + "_1x" + mime);
                            TwitchEmote newEmote = null;
                            if (File.Exists(fileNameLow))
                            {
                                newEmote = GetTwitchEmote(fileName, String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/1", id), name, mime, id, 1);
                            }
                            if (newEmote == null)
                            {
                                newEmote = GetTwitchEmote(fileName, String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/2", id), name, mime, id, 2);
                                if (newEmote == null)
                                    newEmote = GetTwitchEmote(fileName, String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/1", id), name, mime, id, 1);
                            }
                            if (newEmote != null)
                            {
                                returnList.Add(newEmote);
                                alreadyAdded.Add(name);
                            }
                        }
                    }
                    catch { }
                }
                
                if (stv)
                {
                    if (!Directory.Exists(stvFolder))
                        TwitchHelper.CreateDirectory(stvFolder);

                    //Global 7tv Emotes
                    JArray STV = JArray.Parse(client.DownloadString("https://api.7tv.app/v2/emotes/global"));
                    foreach (var emote in STV)
                    {
                        string id = emote["id"].ToString();
                        string name = emote["name"].ToString();
                        string mime = emote["mime"].ToString().Split('/')[1];
                        string url2x = emote["urls"][1][1].ToString(); // 2x
                        if (alreadyAdded.Contains(name))
                            continue;
                        byte[] bytes;
                        string fileName = Path.Combine(stvFolder, id + "_2x." + mime);
                        if (File.Exists(fileName))
                            bytes = File.ReadAllBytes(fileName);
                        else
                        {
                            bytes = client.DownloadData(url2x);
                            File.WriteAllBytes(fileName, bytes);
                        }
                        MemoryStream ms = new MemoryStream(bytes);
                        returnList.Add(new TwitchEmote(new List<SKBitmap>() { SKBitmap.Decode(bytes) }, SKCodec.Create(ms), name, mime, id, 2, bytes));
                        alreadyAdded.Add(name);
                    }

                    //Channel specific 7tv emotes
                    try
                    {
                        JArray STV_channel = JArray.Parse(client.DownloadString(String.Format("https://api.7tv.app/v2/users/{0}/emotes", streamerId)));
                        foreach (var emote in STV_channel)
                        {
                            string id = emote["id"].ToString();
                            string name = emote["name"].ToString();
                            string mime = emote["mime"].ToString().Split('/')[1];
                            string url2x = emote["urls"][1][1].ToString(); // 2x
                            if (alreadyAdded.Contains(name))
                                continue;
                            byte[] bytes;
                            string fileName = Path.Combine(stvFolder, id + "_2x." + mime);
                            if (File.Exists(fileName))
                                bytes = File.ReadAllBytes(fileName);
                            else
                            {
                                bytes = client.DownloadData(url2x);
                                File.WriteAllBytes(fileName, bytes);
                            }
                            MemoryStream ms = new MemoryStream(bytes);
                            returnList.Add(new TwitchEmote(new List<SKBitmap>() { SKBitmap.Decode(bytes) }, SKCodec.Create(ms), name, mime, id, 2, bytes));
                            alreadyAdded.Add(name);
                        }
                    }
                    catch { }
                }
            }

            return returnList;
        }

        private static TwitchEmote GetTwitchEmote(string fileName, string url, string name, string imageType, string id, int imageScale)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    SKBitmap emoteBitmap = null;
                    byte[] bytes = null;
                    bool fileExists = File.Exists(fileName);
                    if (fileExists)
                    {
                        //File may already be locked, can just download again from URL
                        try
                        {
                            bytes = File.ReadAllBytes(fileName);
                        }
                        catch { }
                        emoteBitmap = SKBitmap.Decode(bytes);
                    }

                    if (!fileExists || emoteBitmap == null)
                    {
                        bytes = client.DownloadData(url);
                        try
                        {
                            //File may already be open, just a cache so can ignore
                            File.WriteAllBytes(fileName, bytes);
                        }
                        catch { }
                        emoteBitmap = SKBitmap.Decode(bytes);
                    }

                    MemoryStream ms = new MemoryStream(bytes);
                    return new TwitchEmote(new List<SKBitmap>() { emoteBitmap }, SKCodec.Create(ms), name, imageType, id, imageScale, bytes);
                }
            }
            catch 
            {
                return null;
            }
        }

        public static List<TwitchEmote> GetEmotes(List<Comment> comments, string cacheFolder, Emotes embededEmotes = null, bool deepSearch = false)
        {
            List<TwitchEmote> returnList = new List<TwitchEmote>();
            List<string> alreadyAdded = new List<string>();
            List<string> failedEmotes = new List<string>();

            string emoteFolder = Path.Combine(cacheFolder, "emotes");
            if (!Directory.Exists(emoteFolder))
                TwitchHelper.CreateDirectory(emoteFolder);

            if (embededEmotes != null)
            {
                foreach (FirstPartyEmoteData emoteData in embededEmotes.firstParty)
                {
                    try
                    {
                        MemoryStream ms = new MemoryStream(emoteData.data);
                        SKCodec codec = SKCodec.Create(ms);
                        TwitchEmote newEmote = new TwitchEmote(new List<SKBitmap>() { SKBitmap.Decode(emoteData.data) }, codec, emoteData.id, codec.FrameCount == 0 ? "png" : "gif", emoteData.id, emoteData.imageScale, emoteData.data);
                        returnList.Add(newEmote);
                        alreadyAdded.Add(emoteData.id);
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
                                    string filePath = "";
                                    if (File.Exists(Path.Combine(emoteFolder, id + "_1x.gif")))
                                        filePath = Path.Combine(emoteFolder, id + "_1x.gif");
                                    else if (File.Exists(Path.Combine(emoteFolder, id + "_1x.png")) && !id.Contains("emotesv2_"))
                                        filePath = Path.Combine(emoteFolder, id + "_1x.png");

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
                                            try
                                            {
                                                byte[] bytes = File.ReadAllBytes(filePath);
                                                MemoryStream ms = new MemoryStream(bytes);
                                                SKCodec codec = SKCodec.Create(ms);
                                                returnList.Add(new TwitchEmote(new List<SKBitmap>() { SKBitmap.Decode(bytes) }, codec, id, codec.FrameCount == 0 ? "png" : "gif", id, 1, bytes));
                                                alreadyAdded.Add(id);
                                            }
                                            catch { }
                                        }
                                    }

                                    if (!alreadyAdded.Contains(id))
                                    {
                                        byte[] bytes = client.DownloadData(String.Format("https://static-cdn.jtvnw.net/emoticons/v2/{0}/default/dark/1.0", id));
                                        alreadyAdded.Add(id);
                                        MemoryStream ms = new MemoryStream(bytes);
                                        SKCodec codec = SKCodec.Create(ms);
                                        TwitchEmote newEmote = new TwitchEmote(new List<SKBitmap>() { SKBitmap.Decode(bytes) }, codec, id, codec.FrameCount == 0 ? "png" : "gif", id, 1, bytes);
                                        returnList.Add(newEmote);
                                        try
                                        {
                                            File.WriteAllBytes(Path.Combine(emoteFolder, newEmote.id + "_1x." + newEmote.imageType), bytes);
                                        }
                                        catch { }
                                    }
                                }
                                catch (WebException)
                                {
                                    failedEmotes.Add(id);
                                }
                            }
                        }
                    }
                }
            }

            return returnList;
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
                        try
                        {
                            byte[] bytes = client.DownloadData(downloadUrl);
                            MemoryStream ms = new MemoryStream(bytes);
                            //For some reason, twitch has corrupted images sometimes :) for example
                            //https://static-cdn.jtvnw.net/badges/v1/a9811799-dce3-475f-8feb-3745ad12b7ea/1
                            SKBitmap badgeImage = SKBitmap.Decode(ms);
                            versions.Add(versionString, badgeImage);
                        }
                        catch (ArgumentException)
                        { }
                        catch (WebException)
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
            
            string emojiFolder = Path.Combine(cacheFolder, "emojis");
            if (!Directory.Exists(emojiFolder))
            {
                TwitchHelper.CreateDirectory(emojiFolder);
            }

            int emojiCount = Directory.GetFiles(emojiFolder, "*.png").Length;
            //Twemoji 13.1 has 3577 emojis
            if (emojiCount < 3577)
            {
                string emojiZip = Path.Combine(Path.GetTempPath(), "emojis.zip");
                new WebClient().DownloadFile("https://github.com/twitter/twemoji/archive/refs/tags/v13.1.0.zip", emojiZip);
                using (ZipArchive archive = ZipFile.OpenRead(emojiZip))
                {
                    var emojiAssetsPath = Path.Combine("twemoji-13.1.0", "assets", "72x72");
                    var emojis = archive.Entries.Where(x => Path.GetDirectoryName(x.FullName) == emojiAssetsPath && !String.IsNullOrWhiteSpace(x.Name));
                    foreach (var emoji in emojis)
                    {
                        try
                        {
                            emoji.ExtractToFile(Path.Combine(emojiFolder, emoji.Name));
                        }
                        catch { }
                    }
                }
            }

            List<string> emojiList = new List<string>(Directory.GetFiles(emojiFolder, "*.png"));
            foreach (var emojiPath in emojiList)
            {
                SKBitmap emojiImage = SKBitmap.Decode(File.ReadAllBytes(emojiPath));
                emojiCache.Add(Path.GetFileNameWithoutExtension(emojiPath), emojiImage);
            }

            return emojiCache;
        }

        public static List<CheerEmote> GetBits(string cacheFolder, string channel_id = "")
        {
            List<CheerEmote> cheerEmotes = new List<CheerEmote>();
            string bitsFolder = Path.Combine(cacheFolder, "bits");
            if (!Directory.Exists(bitsFolder))
                TwitchHelper.CreateDirectory(bitsFolder);

            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                GqlCheerResponse cheerResponse = JsonConvert.DeserializeObject<GqlCheerResponse>(client.UploadString("https://gql.twitch.tv/gql", "{\"query\":\"query{cheerConfig{groups{nodes{id, prefix, tiers{bits}}, templateURL}},user(id:\\\"" + channel_id + "\\\"){cheer{cheerGroups{nodes{id,prefix,tiers{bits}},templateURL}}}}\",\"variables\":{}}"));

                if (cheerResponse != null && cheerResponse.data != null)
                {
                    List<CheerGroup> groupList = new List<CheerGroup>();

                    foreach (CheerGroup group in cheerResponse.data.cheerConfig.groups)
                    {
                        groupList.Add(group);
                    }

                    if (cheerResponse.data.user != null && cheerResponse.data.user.cheer != null && cheerResponse.data.user.cheer.cheerGroups != null)
                    {
                        foreach (var group in cheerResponse.data.user.cheer.cheerGroups)
                        {
                            groupList.Add(group);
                        }
                    }

                    foreach (CheerGroup group in groupList)
                    {
                        string templateURL = group.templateURL;

                        foreach (CheerNode node in group.nodes)
                        {
                            string prefix = node.prefix;
                            List<KeyValuePair<int, TwitchEmote>> tierList = new List<KeyValuePair<int, TwitchEmote>>();
                            CheerEmote newEmote = new CheerEmote() { prefix = prefix, tierList = tierList };
                            foreach (Tier tier in node.tiers)
                            {
                                try
                                {
                                    int minBits = tier.bits;
                                    string fileName = Path.Combine(bitsFolder, prefix + minBits + "_2x.gif");
                                    byte[] finalBytes = null;

                                    if (File.Exists(fileName))
                                    {
                                        try
                                        {
                                            finalBytes = File.ReadAllBytes(fileName);
                                        }
                                        catch { }
                                    }
                                    if (finalBytes == null)
                                    {
                                        string url = templateURL.Replace("PREFIX", node.prefix.ToLower()).Replace("BACKGROUND", "dark").Replace("ANIMATION", "animated").Replace("TIER", tier.bits.ToString()).Replace("SCALE.EXTENSION", "2.gif");
                                        byte[] bytes = client.DownloadData(url);
                                        try
                                        {
                                            File.WriteAllBytes(fileName, bytes);
                                        }
                                        catch { }
                                        finalBytes = bytes;
                                    }

                                    if (finalBytes != null)
                                    {
                                        MemoryStream ms = new MemoryStream(finalBytes);
                                        TwitchEmote emote = new TwitchEmote(new List<SKBitmap>() { SKBitmap.Decode(finalBytes) }, SKCodec.Create(ms), prefix, "gif", "", 2, finalBytes);
                                        tierList.Add(new KeyValuePair<int, TwitchEmote>(minBits, emote));
                                    }
                                }
                                catch
                                { }
                            }
                            cheerEmotes.Add(newEmote);
                        }
                    }
                }
            }

            return cheerEmotes;
        }

        public static DirectoryInfo CreateDirectory(string path)
        {
            DirectoryInfo directoryInfo = null;
            try
            {
                directoryInfo = Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    SetDirectoryPermissions(path);
                }
            }
            catch { }

            return directoryInfo;
        }

        public static void SetDirectoryPermissions(string path)
        {
            var folderInfo = new Mono.Unix.UnixFileInfo(path);
            folderInfo.FileAccessPermissions = Mono.Unix.FileAccessPermissions.AllPermissions;
            folderInfo.Refresh();
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
                    client.Encoding = Encoding.UTF8;
                    client.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                    JObject response = JObject.Parse(client.UploadString("https://gql.twitch.tv/gql", "{\"query\":\"query{user(id:\\\"" + id.ToString() + "\\\"){login}}\",\"variables\":{}}"));

                    return response["data"]["user"]["login"].ToString();
                }
            }
            catch { return ""; }
        }
    }
}
