using Newtonsoft.Json;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Properties;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Api;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public static class TwitchHelper
    {
        private static readonly HttpClient httpClient = new HttpClient();
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

        public static async Task<GqlVideoTokenResponse> GetVideoToken(int videoId, string authToken)
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
            return JsonConvert.DeserializeObject<GqlVideoTokenResponse>(response);
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

        public static async Task<List<GqlClipTokenResponse>> GetClipLinks(string clipId)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("[{\"operationName\":\"VideoAccessToken_Clip\",\"variables\":{\"slug\":\"" + clipId + "\"},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"36b89d2507fce29e5ca551df756d27c1cfe079e2609642b4390aa4c35796eb11\"}}}]", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            string response = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<GqlClipTokenResponse>>(response);
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

        public static async Task<EmoteResponse> GetThirdPartyEmoteData(string streamerId, bool getBttv, bool getFfz, bool getStv, bool allowUnlistedEmotes, CancellationToken cancellationToken = new())
        {
            EmoteResponse emoteReponse = new EmoteResponse();

            cancellationToken.ThrowIfCancellationRequested();

            if (getBttv)
            {
                await GetBttvEmoteData(streamerId, emoteReponse.BTTV);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (getFfz)
            {
                await GetFfzEmoteData(streamerId, emoteReponse.FFZ);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (getStv)
            {
                await GetStvEmoteData(streamerId, emoteReponse.STV, allowUnlistedEmotes);
            }

            return emoteReponse;
        }

        private static async Task GetBttvEmoteData(string streamerId, List<EmoteResponseItem> bttvResponse)
        {
            List<BTTVEmote> BTTV = JsonConvert.DeserializeObject<List<BTTVEmote>>(await httpClient.GetStringAsync("https://api.betterttv.net/3/cached/emotes/global"));

            if (streamerId != null)
            {
                //Channel might not have BTTV emotes
                try
                {
                    BTTVChannelEmoteResponse bttvChannel = JsonConvert.DeserializeObject<BTTVChannelEmoteResponse>(await httpClient.GetStringAsync("https://api.betterttv.net/3/cached/users/twitch/" + streamerId));
                    BTTV.AddRange(bttvChannel.channelEmotes);
                    BTTV.AddRange(bttvChannel.sharedEmotes);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
            }

            foreach (var emote in BTTV)
            {
                string id = emote.id;
                string name = emote.code;
                string mime = emote.imageType;
                string url = String.Format("https://cdn.betterttv.net/emote/{0}/[scale]x", id);
                bttvResponse.Add(new EmoteResponseItem() { Id = id, Code = name, ImageType = mime, ImageUrl = url, IsZeroWidth = bttvZeroWidth.Contains(name) });
            }
        }

        private static async Task GetFfzEmoteData(string streamerId, List<EmoteResponseItem> ffzResponse)
        {
            List<FFZEmote> FFZ = JsonConvert.DeserializeObject<List<FFZEmote>>(await httpClient.GetStringAsync("https://api.betterttv.net/3/cached/frankerfacez/emotes/global"));

            if (streamerId != null)
            {
                //Channel might not have FFZ emotes
                try
                {
                    List<FFZEmote> channelEmotes = JsonConvert.DeserializeObject<List<FFZEmote>>(await httpClient.GetStringAsync("https://api.betterttv.net/3/cached/frankerfacez/users/twitch/" + streamerId));
                    FFZ.AddRange(channelEmotes);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
            }

            foreach (var emote in FFZ)
            {
                string id = emote.id.ToString();
                string name = emote.code;
                string mime = emote.imageType;
                string url = String.Format("https://cdn.betterttv.net/frankerfacez_emote/{0}/[scale]", id);
                ffzResponse.Add(new EmoteResponseItem() { Id = id, Code = name, ImageType = mime, ImageUrl = url });
            }
        }

        private static async Task GetStvEmoteData(string streamerId, List<EmoteResponseItem> stvResponse, bool allowUnlistedEmotes)
        {
            STVGlobalEmoteResponse globalEmoteObject = JsonConvert.DeserializeObject<STVGlobalEmoteResponse>(await httpClient.GetStringAsync("https://7tv.io/v3/emote-sets/global"));
            List<STVEmote> stvEmotes = globalEmoteObject.emotes;

            if (streamerId != null)
            {
                // Channel might not be registered on 7tv
                try
                {
                    STVChannelEmoteResponse streamerEmoteObject = JsonConvert.DeserializeObject<STVChannelEmoteResponse>(await httpClient.GetStringAsync(string.Format("https://7tv.io/v3/users/twitch/{0}", streamerId)));
                    // Channel might not have emotes setup
                    if (streamerEmoteObject.emote_set?.emotes != null)
                    {
                        stvEmotes.AddRange(streamerEmoteObject.emote_set.emotes);
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
            }

            foreach (var stvEmote in stvEmotes)
            {
                STVData emoteData = stvEmote.data;
                STVHost emoteHost = emoteData.host;
                List<STVFile> emoteFiles = emoteHost.files;
                if (emoteFiles.Count == 0) // Sometimes there are no hosted files for the emote
                {
                    continue;
                }
                // TODO: Allow and prefer avif when SkiaSharp properly supports it
                string emoteFormat = "";
                foreach (var fileItem in emoteFiles)
                {
                    if (fileItem.format.ToLower() == "webp") // Is the emote offered in webp?
                    {
                        emoteFormat = "webp";
                        break;
                    }
                }
                if (emoteFormat is "") // SkiaSharp does not yet properly support avif, only allow webp - see issue lay295#426
                {
                    continue;
                }
                string emoteUrl = string.Format("https:{0}/{1}.{2}", emoteHost.url, "[scale]x", emoteFormat);
                StvEmoteFlags emoteFlags = emoteData.flags;
                bool emoteIsListed = emoteData.listed;

                EmoteResponseItem emoteResponse = new() { Id = stvEmote.id, Code = stvEmote.name, ImageType = emoteFormat, ImageUrl = emoteUrl };
                if ((emoteFlags & StvEmoteFlags.ZeroWidth) == StvEmoteFlags.ZeroWidth)
                {
                    emoteResponse.IsZeroWidth = true;
                }
                if ((emoteFlags & StvEmoteFlags.ContentTwitchDisallowed) == StvEmoteFlags.ContentTwitchDisallowed || (emoteFlags & StvEmoteFlags.Private) == StvEmoteFlags.Private)
                {
                    continue;
                }
                if (allowUnlistedEmotes || emoteIsListed)
                {
                    stvResponse.Add(emoteResponse);
                }
            }
        }

        public static async Task<List<TwitchEmote>> GetThirdPartyEmotes(int streamerId, string cacheFolder, EmbeddedData embeddedData = null, bool bttv = true, bool ffz = true, bool stv = true, bool allowUnlistedEmotes = true, bool offline = false, CancellationToken cancellationToken = new())
        {
            List<TwitchEmote> returnList = new List<TwitchEmote>();
            List<string> alreadyAdded = new List<string>();

            // No 3rd party emotes are wanted
            if (!bttv && !ffz && !stv)
            {
                return returnList;
            }

            // Load our embedded data from file
            if (embeddedData != null && embeddedData.thirdParty != null)
            {
                foreach (EmbedEmoteData emoteData in embeddedData.thirdParty)
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

            // Directly return if we are in offline, no need for a network request
            if (offline)
            {
                return returnList;
            }

            string bttvFolder = Path.Combine(cacheFolder, "bttv");
            string ffzFolder = Path.Combine(cacheFolder, "ffz");
            string stvFolder = Path.Combine(cacheFolder, "stv");

            EmoteResponse emoteDataResponse = await GetThirdPartyEmoteData(streamerId.ToString(), bttv, ffz, stv, allowUnlistedEmotes, cancellationToken);

            if (bttv)
            {
                if (!Directory.Exists(bttvFolder))
                    TwitchHelper.CreateDirectory(bttvFolder);

                foreach (var emote in emoteDataResponse.BTTV)
                {
                    if (alreadyAdded.Contains(emote.Code))
                        continue;
                    try
                    {
                        TwitchEmote newEmote = new TwitchEmote(await GetImage(bttvFolder, emote.ImageUrl.Replace("[scale]", "2"), emote.Id, "2", emote.ImageType, cancellationToken), EmoteProvider.ThirdParty, 2, emote.Id, emote.Code);
                        if (emote.IsZeroWidth)
                            newEmote.IsZeroWidth = true;
                        returnList.Add(newEmote);
                        alreadyAdded.Add(emote.Code);
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (ffz)
            {
                if (!Directory.Exists(ffzFolder))
                    TwitchHelper.CreateDirectory(ffzFolder);

                foreach (var emote in emoteDataResponse.FFZ)
                {
                    if (alreadyAdded.Contains(emote.Code))
                        continue;
                    try
                    {
                        TwitchEmote newEmote = new TwitchEmote(await GetImage(ffzFolder, emote.ImageUrl.Replace("[scale]", "2"), emote.Id, "2", emote.ImageType, cancellationToken), EmoteProvider.ThirdParty, 2, emote.Id, emote.Code);
                        returnList.Add(newEmote);
                        alreadyAdded.Add(emote.Code);
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (stv)
            {
                if (!Directory.Exists(stvFolder))
                    TwitchHelper.CreateDirectory(stvFolder);

                foreach (var emote in emoteDataResponse.STV)
                {
                    if (alreadyAdded.Contains(emote.Code))
                        continue;
                    try
                    {
                        TwitchEmote newEmote = new TwitchEmote(await GetImage(stvFolder, emote.ImageUrl.Replace("[scale]", "2"), emote.Id, "2", emote.ImageType, cancellationToken), EmoteProvider.ThirdParty, 2, emote.Id, emote.Code);
                        if (emote.IsZeroWidth)
                            newEmote.IsZeroWidth = true;
                        returnList.Add(newEmote);
                        alreadyAdded.Add(emote.Code);
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
                }
            }

            return returnList;
        }

        public static async Task<List<TwitchEmote>> GetEmotes(List<Comment> comments, string cacheFolder, EmbeddedData embeddedData = null, bool offline = false)
        {
            List<TwitchEmote> returnList = new List<TwitchEmote>();
            List<string> alreadyAdded = new List<string>();
            List<string> failedEmotes = new List<string>();

            string emoteFolder = Path.Combine(cacheFolder, "emotes");
            if (!Directory.Exists(emoteFolder))
                TwitchHelper.CreateDirectory(emoteFolder);

            // Load our embedded emotes
            if (embeddedData != null && embeddedData.firstParty != null)
            {
                foreach (EmbedEmoteData emoteData in embeddedData.firstParty)
                {
                    try
                    {
                        TwitchEmote newEmote = new TwitchEmote(emoteData.data, EmoteProvider.FirstParty, emoteData.imageScale, emoteData.id, emoteData.name);
                        returnList.Add(newEmote);
                        alreadyAdded.Add(emoteData.id);
                    }
                    catch { }
                }
            }

            // Directly return if we are in offline, no need for a network request
            if (offline)
            {
                return returnList;
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
                            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                            {
                                failedEmotes.Add(id);
                            }
                        }
                    }
                }
            }

            return returnList;
        }

        public static async Task<List<ChatBadge>> GetChatBadges(int streamerId, string cacheFolder, EmbeddedData embeddedData = null, bool offline = false)
        {
            List<ChatBadge> returnList = new List<ChatBadge>();
            List<string> alreadyAdded = new List<string>();

            // Load our embedded data from file
            if (embeddedData != null && embeddedData.twitchBadges != null)
            {
                foreach (EmbedChatBadge data in embeddedData.twitchBadges)
                {
                    ChatBadge newBadge = new ChatBadge(data.name, data.versions);
                    returnList.Add(newBadge);
                    alreadyAdded.Add(data.name);
                }
            }

            // Directly return if we are in offline, no need for a network request
            if (offline)
            {
                return returnList;
            }

            // TODO: this currently only does twitch badges, but we could also support FFZ, BTTV, 7TV, etc badges!
            // TODO: would want to make this configurable as we do for emotes though...
            TwitchBadgeResponse globalBadges = JsonConvert.DeserializeObject<TwitchBadgeResponse>(await httpClient.GetStringAsync("https://badges.twitch.tv/v1/badges/global/display"));
            TwitchBadgeResponse subBadges = JsonConvert.DeserializeObject<TwitchBadgeResponse>(await httpClient.GetStringAsync($"https://badges.twitch.tv/v1/badges/channels/{streamerId}/display"));

            string badgeFolder = Path.Combine(cacheFolder, "badges");
            if (!Directory.Exists(badgeFolder))
                TwitchHelper.CreateDirectory(badgeFolder);

            foreach (var badge in globalBadges.badge_sets.Union(subBadges.badge_sets))
            {
                string name = badge.Key;
                if (alreadyAdded.Contains(name))
                    continue;

                try
                {
                    Dictionary<string, byte[]> versions = new Dictionary<string, byte[]>();
                    foreach (var version in badge.Value.versions)
                    {
                        string downloadUrl = version.Value.image_url_2x;
                        string[] id_parts = downloadUrl.Split('/');
                        string id = id_parts[id_parts.Length - 2];
                        byte[] bytes = await GetImage(badgeFolder, downloadUrl, id, "2", "png");
                        versions.Add(version.Key, bytes);
                    }

                    //Prefer channel specific badges over global ones
                    if (subBadges.badge_sets.ContainsKey(name))
                    {
                        foreach (var version in subBadges.badge_sets[name].versions)
                        {
                            string downloadUrl = version.Value.image_url_2x;
                            string[] id_parts = downloadUrl.Split('/');
                            string id = id_parts[id_parts.Length - 2];
                            byte[] bytes = await GetImage(badgeFolder, downloadUrl, id, "2", "png");
                            versions[version.Key] = bytes;
                        }
                    }

                    returnList.Add(new ChatBadge(name, versions));
                    alreadyAdded.Add(name);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
            }

            return returnList;
        }

        public static async Task<Dictionary<string, SKBitmap>> GetTwitterEmojis(string cacheFolder)
        {
            Dictionary<string, SKBitmap> returnCache = new Dictionary<string, SKBitmap>();

            string emojiFolder = Path.Combine(cacheFolder, "emojis");
            Regex emojiExtensions = new Regex(@"\.(?:png|PNG)\z", RegexOptions.RightToLeft); // Extensions are case sensitive on Linux and Mac

            if (!Directory.Exists(emojiFolder))
                TwitchHelper.CreateDirectory(emojiFolder);

            string[] emojiFiles = Directory.GetFiles(emojiFolder).Where(i => emojiExtensions.IsMatch(i)).ToArray();

            // Twemoji 14 has 3689 emoji images
            if (emojiFiles.Length < 3689)
            {
                string emojiZipPath = Path.Combine(emojiFolder, Path.GetRandomFileName());
                try
                {
                    byte[] emojiZipData = Resources.twemoji_14_0_0;
                    await File.WriteAllBytesAsync(emojiZipPath, emojiZipData);

                    using ZipArchive archive = ZipFile.OpenRead(emojiZipPath);
                    var emojiAssetsPath = Path.Combine("twemoji-14.0.0", "assets", "72x72");
                    var emojis = archive.Entries.Where(x => !string.IsNullOrWhiteSpace(x.Name) && Path.GetDirectoryName(x.FullName) == emojiAssetsPath);
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
                finally
                {
                    if (File.Exists(emojiZipPath))
                    {
                        File.Delete(emojiZipPath);
                    }
                }
            }

            foreach (var emojiPath in emojiFiles)
            {
                byte[] emojiBytes = await File.ReadAllBytesAsync(emojiPath);
                SKBitmap emojiImage = SKBitmap.Decode(emojiBytes);
                returnCache.Add(Path.GetFileNameWithoutExtension(emojiPath), emojiImage);
            }

            return returnCache;
        }

        public static async Task<List<CheerEmote>> GetBits(string cacheFolder, string channel_id = "", EmbeddedData embeddedData = null, bool offline = false)
        {
            List<CheerEmote> returnList = new List<CheerEmote>();
            List<string> alreadyAdded = new List<string>();

            // Load our embedded data from file
            if (embeddedData != null && embeddedData.twitchBits != null)
            {
                foreach (EmbedCheerEmote data in embeddedData.twitchBits)
                {
                    List<KeyValuePair<int, TwitchEmote>> tierList = new List<KeyValuePair<int, TwitchEmote>>();
                    CheerEmote newEmote = new CheerEmote() { prefix = data.prefix, tierList = tierList };
                    foreach (KeyValuePair<int, EmbedEmoteData> tier in data.tierList)
                    {
                        TwitchEmote tierEmote = new TwitchEmote(tier.Value.data, EmoteProvider.FirstParty, tier.Value.imageScale, tier.Value.id, tier.Value.name);
                        tierList.Add(new KeyValuePair<int, TwitchEmote>(tier.Key, tierEmote));
                    }
                    returnList.Add(newEmote);
                    alreadyAdded.Add(data.prefix);
                }
            }

            // Directly return if we are in offline, no need for a network request
            if (offline)
            {
                return returnList;
            }

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
                        if (alreadyAdded.Contains(prefix))
                            continue;
                        try
                        {
                            List<KeyValuePair<int, TwitchEmote>> tierList = new List<KeyValuePair<int, TwitchEmote>>();
                            CheerEmote newEmote = new CheerEmote() { prefix = prefix, tierList = tierList };
                            foreach (Tier tier in node.tiers)
                            {
                                int minBits = tier.bits;
                                string url = templateURL.Replace("PREFIX", node.prefix.ToLower()).Replace("BACKGROUND", "dark").Replace("ANIMATION", "animated").Replace("TIER", tier.bits.ToString()).Replace("SCALE.EXTENSION", "2.gif");
                                TwitchEmote emote = new TwitchEmote(await GetImage(bitFolder, url, node.id + tier.bits, "2", "gif"), EmoteProvider.FirstParty, 2, prefix + minBits, prefix + minBits);
                                tierList.Add(new KeyValuePair<int, TwitchEmote>(minBits, emote));
                            }
                            returnList.Add(newEmote);
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
                    }
                }
            }

            return returnList;
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

        /// <summary>
        /// Cleans up any unmanaged cache files from previous runs that were interrupted before cleaning up
        /// </summary>
        public static void CleanupUnmanagedCacheFiles(string cacheFolder)
        {
            // Let's delete any video download cache folders older than 24 hours
            // Or that have been inactive for 2 hours
            var videoFolderRegex = new Regex(@"\d+_(\d+)$", RegexOptions.RightToLeft); // Matches "...###_###" and captures the 2nd ###
            var directories = Directory.GetDirectories(cacheFolder);
            foreach (var directory in directories)
            {
                var videoFolderMatch = videoFolderRegex.Match(directory);
                if (videoFolderMatch.Success)
                {
                    bool wasDeleted = DeleteOldDirectory(directory, videoFolderMatch.Groups[1].ToString());

                    if (wasDeleted) continue;

                    wasDeleted = DeleteColdDirectory(directory);

                    if (wasDeleted) continue;
                }
            }
        }

        private static bool DeleteOldDirectory(string directory, string directoryCreationMillis)
        {
            var downloadTime = long.Parse(directoryCreationMillis);
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (currentTime - downloadTime > 86_400_000) // 24 hours in millis
            {
                try
                {
                    Directory.Delete(directory, true);
                    return true;
                }
                catch { }
            }
            return false;
        }

        private static bool DeleteColdDirectory(string directory)
        {
            // Directory.GetLastWriteTimeUtc() works as expected on both Windows and MacOS. Assuming it does on Linux too
            var directoryWriteTimeMillis = Directory.GetLastWriteTimeUtc(directory).Ticks / TimeSpan.TicksPerMillisecond;
            var currentTimeMillis = DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

            if (currentTimeMillis - directoryWriteTimeMillis > 14_400_000) // 4 hours in millis
            {
                try
                {
                    Directory.Delete(directory, true);
                    return true;
                }
                catch { }
            }
            return false;
        }

        public static int TimestampToSeconds(string input)
        {
            // Gets total seconds from timestamp in the format of 0h0m0s
            input = input.Replace('h', ':').Replace('m', ':').Replace("s", "");
            TimeSpan returnSpan = TimeSpan.Parse(input);

            return (int)returnSpan.TotalSeconds;
        }

        public static async Task<string> GetStreamerName(int id)
        {
            try
            {
                GqlUserInfoResponse info = await GetUserInfo(new List<string> { id.ToString() });
                return info.data.users[0].login;
            }
            catch { return ""; }
        }

        public static async Task<GqlUserInfoResponse> GetUserInfo(List<string> idList)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{users(ids:[" + String.Join(",", idList.Select(x => "\\\"" + x + "\\\"").ToArray()) + "]){id,login,createdAt,updatedAt,description,profileImageURL(width:300)}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            string response = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
            GqlUserInfoResponse userInfo = JsonConvert.DeserializeObject<GqlUserInfoResponse>(response);
            return userInfo;
        }

        public static async Task<byte[]> GetImage(string cachePath, string imageUrl, string imageId, string imageScale, string imageType, CancellationToken cancellationToken = new())
        {
            byte[] imageBytes = null;

            string filePath = Path.Combine(cachePath, imageId + "_" + imageScale + "." + imageType);
            if (File.Exists(filePath))
            {
                try
                {
                    using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    byte[] bytes = new byte[stream.Length];
                    stream.Seek(0, SeekOrigin.Begin);
                    await stream.ReadAsync(bytes, cancellationToken);

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
                                await stream.DisposeAsync();
                                File.Delete(filePath);
                            }
                            catch { }
                        }
                    }
                }
                catch (IOException)
                {
                    //File being written to by parallel process? Maybe. Can just fallback to HTTP request.
                }
            }

            // If fetching from cache failed
            if (imageBytes != null)
                return imageBytes;

            // Fallback to HTTP request
            imageBytes = await httpClient.GetByteArrayAsync(imageUrl, cancellationToken);

            //Let's save this image to the cache
            try
            {
                using FileStream stream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                await stream.WriteAsync(imageBytes, cancellationToken);
            }
            catch { }

            return imageBytes;
        }

        public static async Task<GqlVideoChapterResponse> GetVideoChapters(int videoId)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"extensions\":{\"persistedQuery\":{\"sha256Hash\":\"8d2793384aac3773beab5e59bd5d6f585aedb923d292800119e03d40cd0f9b41\",\"version\":1}},\"operationName\":\"VideoPlayer_ChapterSelectButtonVideo\",\"variables\":{\"videoID\":\""+ videoId + "\"}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            string response = await (await httpClient.SendAsync(request)).Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<GqlVideoChapterResponse>(response);
        }
    }
}
