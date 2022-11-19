using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TwitchDownloaderCore.Properties;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public static class TwitchHelper
    {
        private static HttpClient httpClient = new HttpClient();
        private static readonly string[] bttvZeroWidth = { "SoSnowy", "IceCold", "SantaHat", "TopHat", "ReinDeer", "CandyCane", "cvMask", "cvHazmat" };
        public static async Task<GqlVideoResponse> GetVideoInfo(int videoId)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{video(id:\\\"" + videoId + "\\\"){title,thumbnailURLs(height:180,width:320),createdAt,lengthSeconds,owner{id,displayName}}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            string response = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<GqlVideoResponse>(response);
        }

        public static async Task<JObject> GetVideoToken(int videoId, string authToken)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"operationName\":\"PlaybackAccessToken_Template\",\"query\":\"query PlaybackAccessToken_Template($login: String!, $isLive: Boolean!, $vodID: ID!, $isVod: Boolean!, $playerType: String!) {  streamPlaybackAccessToken(channelName: $login, params: {platform: \\\"web\\\", playerBackend: \\\"mediaplayer\\\", playerType: $playerType}) @include(if: $isLive) {    value    signature    __typename  }  videoPlaybackAccessToken(id: $vodID, params: {platform: \\\"web\\\", playerBackend: \\\"mediaplayer\\\", playerType: $playerType}) @include(if: $isVod) {    value    signature    __typename  }}\",\"variables\":{\"isLive\":false,\"login\":\"\",\"isVod\":true,\"vodID\":\"" + videoId + "\",\"playerType\":\"embed\"}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            if (authToken != null && authToken != "")
                request.Headers.Add("Authorization", "OAuth " + authToken);
            string response = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
            return JObject.Parse(response);
        }

        public static async Task<string[]> GetVideoPlaylist(int videoId, string token, string sig)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(String.Format("http://usher.twitch.tv/vod/{0}?nauth={1}&nauthsig={2}&allow_source=true&player=twitchweb", videoId, token, sig)),
                Method = HttpMethod.Get
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            string playlist = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
            return playlist.Split('\n');
        }

        public static async Task<GqlClipResponse> GetClipInfo(object clipId)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{clip(slug:\\\"" + clipId + "\\\"){title,thumbnailURL,createdAt,durationSeconds,broadcaster{id,displayName},videoOffsetSeconds,video{id}}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            string response = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<GqlClipResponse>(response);
        }

        public static async Task<JArray> GetClipLinks(object clipId)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("[{\"operationName\":\"VideoAccessToken_Clip\",\"variables\":{\"slug\":\"" + clipId + "\"},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"36b89d2507fce29e5ca551df756d27c1cfe079e2609642b4390aa4c35796eb11\"}}}]", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            string response = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
            JArray result = JArray.Parse(response);
            return result;
        }

        public static async Task<GqlVideoSearchResponse> GetGqlVideos(string channelName, string cursor = "", int limit = 50)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{user(login:\\\"" + channelName + "\\\"){videos(first: " + limit + "" + (cursor == "" ? "" : ",after:\\\"" + cursor + "\\\"") + ") { edges { node { title, id, lengthSeconds, previewThumbnailURL(height: 180, width: 320), createdAt, viewCount }, cursor }, pageInfo { hasNextPage, hasPreviousPage }, totalCount }}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            string response = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<GqlVideoSearchResponse>(response);
        }

        public static async Task<GqlClipSearchResponse> GetGqlClips(string channelName, string period = "LAST_WEEK", string cursor = "", int limit = 50)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{user(login:\\\"" + channelName + "\\\"){clips(first: " + limit + ", after: \\\"" + cursor + "\\\", criteria: { period: " + period + " }) {  edges { cursor, node { id, slug, title, createdAt, durationSeconds, thumbnailURL, viewCount } }, pageInfo { hasNextPage, hasPreviousPage } }}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            string response = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<GqlClipSearchResponse>(response);
        }

        public static async Task<EmoteResponse> GetThirdPartyEmoteData(string streamerId, bool getBttv, bool getFfz, bool getStv)
        {
            EmoteResponse emoteReponse = new EmoteResponse();

            if (getBttv)
            {
                JArray BTTV = JArray.Parse(await httpClient.GetStringAsync("https://api.betterttv.net/3/cached/emotes/global"));

                if (streamerId != null)
                {
                    //Channel might not have BTTV emotes
                    try
                    {
                        JObject bttvChannel = JObject.Parse(await httpClient.GetStringAsync("https://api.betterttv.net/3/cached/users/twitch/" + streamerId));
                        BTTV.Merge(bttvChannel["channelEmotes"]);
                        BTTV.Merge(bttvChannel["sharedEmotes"]);
                    }
                    catch { }
                }

                foreach (var emote in BTTV)
                {
                    string id = emote["id"].ToString();
                    string name = emote["code"].ToString();
                    string mime = emote["imageType"].ToString();
                    string url = String.Format("https://cdn.betterttv.net/emote/{0}/[scale]x", id);
                    emoteReponse.BTTV.Add(new EmoteResponseItem() { Id = id, Code = name, ImageType = mime, ImageUrl = url, IsZeroWidth = bttvZeroWidth.Contains(name) });
                }
            }

            if (getFfz)
            {
                JArray FFZ = JArray.Parse(await httpClient.GetStringAsync("https://api.betterttv.net/3/cached/frankerfacez/emotes/global"));

                if (streamerId != null)
                {
                    //Channel might not have FFZ emotes
                    try
                    {
                        FFZ.Merge(JArray.Parse(await httpClient.GetStringAsync("https://api.betterttv.net/3/cached/frankerfacez/users/twitch/" + streamerId)));
                    }
                    catch { }
                }

                foreach (var emote in FFZ)
                {
                    string id = emote["id"].ToString();
                    string name = emote["code"].ToString();
                    string mime = emote["imageType"].ToString();
                    string url = String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/[scale]", id);
                    emoteReponse.FFZ.Add(new EmoteResponseItem() { Id = id, Code = name, ImageType = mime, ImageUrl = url });
                }
            }

            if (getStv)
            {
                JObject globalEmoteObject = JObject.Parse(await httpClient.GetStringAsync("https://7tv.io/v3/emote-sets/global"));
                JArray stvEmotes = (JArray)globalEmoteObject["emotes"];

                if (streamerId != null)
                {
                    //Channel might not have 7TV emotes
                    try
                    {
                        JObject streamerEmoteObject = JObject.Parse(await httpClient.GetStringAsync(string.Format("https://7tv.io/v3/users/twitch/{0}", streamerId)));
                        stvEmotes.Merge((JArray)streamerEmoteObject["emote_set"]["emotes"]);
                    }
                    catch { }
                }

                foreach (var stvEmote in stvEmotes)
                {
                    string emoteId = stvEmote["id"].ToString();
                    string emoteName = stvEmote["name"].ToString();
                    JObject emoteData = (JObject)stvEmote["data"];
                    JObject emoteHost = (JObject)emoteData["host"];
                    JArray emoteFiles = (JArray)emoteHost["files"];
                    string emoteFormat = "avif";
                    foreach (var fileItem in emoteFiles)
                    {
                        // prefer webp
                        if (fileItem["format"].ToString().ToLower().Equals("webp"))
                        {
                            emoteFormat = "webp";
                            break;
                        }
                    }
                    string emoteUrl = string.Format("https:{0}/{1}.{2}", emoteHost["url"].ToString(), "[scale]x", emoteFormat);
                    StvEmoteFlags emoteFlags = (StvEmoteFlags)(int)emoteData["flags"];
                    bool emoteIsListed = (bool)emoteData["listed"];

                    EmoteResponseItem emoteResponse = new() { Id = emoteId, Code = emoteName, ImageType = emoteFormat, ImageUrl = emoteUrl };
                    if ((emoteFlags & StvEmoteFlags.ZeroWidth) == StvEmoteFlags.ZeroWidth)
                    {
                        emoteResponse.IsZeroWidth = true;
                    }
                    if ((emoteFlags & StvEmoteFlags.ContentTwitchDisallowed) == StvEmoteFlags.ContentTwitchDisallowed || (emoteFlags & StvEmoteFlags.Private) == StvEmoteFlags.Private)
                    {
                        continue;
                    }
                    if (emoteIsListed)
                    {
                        emoteReponse.STV.Add(emoteResponse);
                    }
                }
            }

            return emoteReponse;
        }
        public static async Task<List<TwitchEmote>> GetThirdPartyEmotes(int streamerId, string cacheFolder, Emotes embededEmotes = null, bool bttv = true, bool ffz = true, bool stv = true)
        {
            List<TwitchEmote> returnList = new List<TwitchEmote>();
            List<string> alreadyAdded = new List<string>();

            string bttvFolder = Path.Combine(cacheFolder, "bttv");
            string ffzFolder = Path.Combine(cacheFolder, "ffz");
            string stvFolder = Path.Combine(cacheFolder, "stv");

            EmoteResponse emoteDataResponse = await GetThirdPartyEmoteData(streamerId.ToString(), bttv, ffz, stv);

            if (embededEmotes != null)
            {
                foreach (EmbedEmoteData emoteData in embededEmotes.thirdParty)
                {
                    try
                    {
                        TwitchEmote newEmote = new TwitchEmote(emoteData.data, EmoteProvider.ThirdParty, emoteData.imageScale, emoteData.id, emoteData.name);
                        returnList.Add(newEmote);
                        alreadyAdded.Add(emoteData.name);
                    }
                    catch { }
                }
            }

            if (bttv)
            {
                if (!Directory.Exists(bttvFolder))
                    TwitchHelper.CreateDirectory(bttvFolder);

                foreach (var emote in emoteDataResponse.BTTV)
                {
                    if (alreadyAdded.Contains(emote.Code))
                        continue;
                    TwitchEmote newEmote = new TwitchEmote(await GetImage(bttvFolder, emote.ImageUrl.Replace("[scale]", "2"), emote.Id, "2", emote.ImageType), EmoteProvider.ThirdParty, 2, emote.Id, emote.Code);
                    if (emote.IsZeroWidth)
                        newEmote.IsZeroWidth = true;
                    returnList.Add(newEmote);
                    alreadyAdded.Add(emote.Code);
                }
            }

            if (ffz)
            {
                if (!Directory.Exists(ffzFolder))
                    TwitchHelper.CreateDirectory(ffzFolder);

                foreach (var emote in emoteDataResponse.FFZ)
                {
                    if (alreadyAdded.Contains(emote.Code))
                        continue;
                    TwitchEmote newEmote = new TwitchEmote(await GetImage(ffzFolder, emote.ImageUrl.Replace("[scale]", "2"), emote.Id, "2", emote.ImageType), EmoteProvider.ThirdParty, 2, emote.Id, emote.Code);
                    returnList.Add(newEmote);
                    alreadyAdded.Add(emote.Code);
                }
            }

            if (stv)
            {
                if (!Directory.Exists(stvFolder))
                    TwitchHelper.CreateDirectory(stvFolder);

                foreach (var emote in emoteDataResponse.STV)
                {
                    if (alreadyAdded.Contains(emote.Code))
                        continue;
                    TwitchEmote newEmote = new TwitchEmote(await GetImage(stvFolder, emote.ImageUrl.Replace("[scale]", "2"), emote.Id, "2", emote.ImageType), EmoteProvider.ThirdParty, 2, emote.Id, emote.Code);
                    if (emote.IsZeroWidth)
                        newEmote.IsZeroWidth = true;
                    returnList.Add(newEmote);
                    alreadyAdded.Add(emote.Code);
                }
            }

            return returnList;
        }

        public static async Task<List<TwitchEmote>> GetEmotes(List<Comment> comments, string cacheFolder, Emotes embededEmotes = null)
        {
            List<TwitchEmote> returnList = new List<TwitchEmote>();
            List<string> alreadyAdded = new List<string>();
            List<string> failedEmotes = new List<string>();

            string emoteFolder = Path.Combine(cacheFolder, "emotes");
            if (!Directory.Exists(emoteFolder))
                TwitchHelper.CreateDirectory(emoteFolder);

            if (embededEmotes != null)
            {
                foreach (EmbedEmoteData emoteData in embededEmotes.firstParty)
                {
                    try
                    {
                        TwitchEmote newEmote = new TwitchEmote(emoteData.data, EmoteProvider.FirstParty, emoteData.imageScale, emoteData.id, emoteData.name);
                        returnList.Add(newEmote);
                        alreadyAdded.Add(emoteData.name);
                    }
                    catch { }
                }
            }

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
                                byte[] bytes = await GetImage(emoteFolder, String.Format("https://static-cdn.jtvnw.net/emoticons/v2/{0}/default/dark/2.0", id), id, "2", "png");
                                TwitchEmote newEmote = new TwitchEmote(bytes, EmoteProvider.FirstParty, 2, id, id);
                                alreadyAdded.Add(id);
                                returnList.Add(newEmote);
                            }
                            catch (HttpRequestException ex) when (ex.Message.Contains("404"))
                            {
                                failedEmotes.Add(id);
                            }
                        }
                    }
                }
            }

            return returnList;
        }

        public static async Task<List<ChatBadge>> GetChatBadges(int streamerId, string cacheFolder)
        {
            List<ChatBadge> returnList = new List<ChatBadge>();

            JObject globalBadges = JObject.Parse(await httpClient.GetStringAsync("https://badges.twitch.tv/v1/badges/global/display"));
            JObject subBadges = JObject.Parse(await httpClient.GetStringAsync($"https://badges.twitch.tv/v1/badges/channels/{streamerId}/display"));

            string badgeFolder = Path.Combine(cacheFolder, "badges");
            if (!Directory.Exists(badgeFolder))
                TwitchHelper.CreateDirectory(badgeFolder);

            foreach (var badge in subBadges["badge_sets"].Union(globalBadges["badge_sets"]))
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
                        string[] id_parts = downloadUrl.Split('/');
                        string id = id_parts[id_parts.Length - 2];
                        byte[] bytes = await GetImage(badgeFolder, downloadUrl, id, "2", "png");
                        using MemoryStream ms = new MemoryStream(bytes);
                        //For some reason, twitch has corrupted images sometimes :) for example
                        //https://static-cdn.jtvnw.net/badges/v1/a9811799-dce3-475f-8feb-3745ad12b7ea/1
                        SKBitmap badgeImage = SKBitmap.Decode(ms);
                        versions.Add(versionString, badgeImage);
                    }
                    catch (HttpRequestException)
                    { }
                }

                returnList.Add(new ChatBadge(name, versions));
            }

            return returnList;
        }

        public static async Task<Dictionary<string, SKBitmap>> GetTwitterEmojis(string cacheFolder)
        {
            Dictionary<string, SKBitmap> returnCache = new Dictionary<string, SKBitmap>();

            string emojiFolder = Path.Combine(cacheFolder, "emojis");
            if (!Directory.Exists(emojiFolder))
                TwitchHelper.CreateDirectory(emojiFolder);

            int emojiCount = Directory.GetFiles(emojiFolder, "*.png").Length;

            //Twemoji 14 has 3689 emoji images
            if (emojiCount < 3689)
            {
                string emojiZipPath = Path.Combine(emojiFolder, Path.GetRandomFileName());
                byte[] emojiZipData = Resources.twemoji_14_0_0;
                await File.WriteAllBytesAsync(emojiZipPath, emojiZipData);
                using (ZipArchive archive = ZipFile.OpenRead(emojiZipPath))
                {
                    var emojiAssetsPath = Path.Combine("twemoji-14.0.0", "assets", "72x72");
                    var emojis = archive.Entries.Where(x => Path.GetDirectoryName(x.FullName) == emojiAssetsPath && !String.IsNullOrWhiteSpace(x.Name));
                    foreach (var emoji in emojis)
                    {
                        string filePath = Path.Combine(emojiFolder, emoji.Name.ToUpper().Replace("-", " "));
                        if (!File.Exists(filePath))
                        {
                            try
                            {
                                emoji.ExtractToFile(filePath);
                            }
                            catch { }
                        }
                    }
                }

                if (File.Exists(emojiZipPath))
                {
                    File.Delete(emojiZipPath);
                }
            }

            List<string> emojiList = new List<string>(Directory.GetFiles(emojiFolder, "*.png"));
            foreach (var emojiPath in emojiList)
            {
                SKBitmap emojiImage = SKBitmap.Decode(await File.ReadAllBytesAsync(emojiPath));
                returnCache.Add(Path.GetFileNameWithoutExtension(emojiPath), emojiImage);
            }

            return returnCache;
        }

        public static async Task<List<CheerEmote>> GetBits(string cacheFolder, string channel_id = "")
        {
            List<CheerEmote> returnCheermotes = new List<CheerEmote>();

            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{cheerConfig{groups{nodes{id, prefix, tiers{bits}}, templateURL}},user(id:\\\"" + channel_id + "\\\"){cheer{cheerGroups{nodes{id,prefix,tiers{bits}},templateURL}}}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            string response = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
            GqlCheerResponse cheerResponse = JsonConvert.DeserializeObject<GqlCheerResponse>(response);

            string bitFolder = Path.Combine(cacheFolder, "bits");
            if (!Directory.Exists(bitFolder))
                TwitchHelper.CreateDirectory(bitFolder);

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
                            int minBits = tier.bits;
                            string url = templateURL.Replace("PREFIX", node.prefix.ToLower()).Replace("BACKGROUND", "dark").Replace("ANIMATION", "animated").Replace("TIER", tier.bits.ToString()).Replace("SCALE.EXTENSION", "2.gif");
                            TwitchEmote emote = new TwitchEmote(await GetImage(bitFolder, url, node.id + tier.bits, "2", "gif"), EmoteProvider.FirstParty, 2, prefix + minBits, prefix + minBits);
                            tierList.Add(new KeyValuePair<int, TwitchEmote>(minBits, emote));
                        }
                        returnCheermotes.Add(newEmote);
                    }
                }
            }

            return returnCheermotes;
        }

        public static DirectoryInfo CreateDirectory(string path)
        {
            DirectoryInfo directoryInfo = Directory.CreateDirectory(path);

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

        public static async Task<string> GetStreamerName(int id)
        {
            try
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri("https://gql.twitch.tv/gql"),
                    Method = HttpMethod.Post,
                    Content = new StringContent("{\"query\":\"query{user(id:\\\"" + id.ToString() + "\\\"){login}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
                string response = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
                JObject res = JObject.Parse(response);
                return res["data"]["user"]["login"].ToString();
            }
            catch { return ""; }
        }

        public static async Task<byte[]> GetImage(string cachePath, string imageUrl, string imageId, string imageScale, string imageType)
        {
            byte[] imageBytes = null;

            string filePath = Path.Combine(cachePath, imageId + "_" + imageScale + "." + imageType);
            if (File.Exists(filePath))
            {
                try
                {
                    using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] bytes = new byte[stream.Length];
                        stream.Seek(0, SeekOrigin.Begin);
                        await stream.ReadAsync(bytes, 0, bytes.Length);

                        //Check if image file is not corrupt
                        if (bytes.Length > 0)
                        {
                            using SKImage image = SKImage.FromEncodedData(bytes);
                            if (image != null)
                            {
                                imageBytes = bytes;
                            }
                            else
                            {
                                //Try to delete the corrupted image
                                try
                                {
                                    stream.Dispose();
                                    File.Delete(filePath);
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch (IOException)
                {
                    //File being written to by parallel process? Maybe. Can just fallback to HTTP request.
                }
            }

            if (imageBytes != null)
                return imageBytes;

            imageBytes = await httpClient.GetByteArrayAsync(imageUrl);

            //Let's save this image to the cache
            try
            {
                using (FileStream stream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Write(imageBytes, 0, imageBytes.Length);
                }
            }
            catch { }

            return imageBytes;
        }
    }
}
