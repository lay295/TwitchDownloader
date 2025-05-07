using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Mono.Unix;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Api;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public static class TwitchHelper
    {
        private static readonly HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private static readonly string[] BttvZeroWidth = { "SoSnowy", "IceCold", "SantaHat", "TopHat", "ReinDeer", "CandyCane", "cvMask", "cvHazmat" };

        public static async Task<GqlVideoResponse> GetVideoInfo(long videoId)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{video(id:\\\"" + videoId + "\\\"){title,thumbnailURLs(height:180,width:320),createdAt,lengthSeconds,owner{id,displayName,login},viewCount,game{id,displayName,boxArtURL},description,status}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GqlVideoResponse>();
        }

        public static async Task<GqlVideoTokenResponse> GetVideoToken(long videoId, string authToken)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"operationName\":\"PlaybackAccessToken_Template\",\"query\":\"query PlaybackAccessToken_Template($login: String!, $isLive: Boolean!, $vodID: ID!, $isVod: Boolean!, $playerType: String!) {  streamPlaybackAccessToken(channelName: $login, params: {platform: \\\"web\\\", playerBackend: \\\"mediaplayer\\\", playerType: $playerType}) @include(if: $isLive) {    value    signature    __typename  }  videoPlaybackAccessToken(id: $vodID, params: {platform: \\\"web\\\", playerBackend: \\\"mediaplayer\\\", playerType: $playerType}) @include(if: $isVod) {    value    signature    __typename  }}\",\"variables\":{\"isLive\":false,\"login\":\"\",\"isVod\":true,\"vodID\":\"" + videoId + "\",\"playerType\":\"embed\"}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            if (!string.IsNullOrWhiteSpace(authToken))
                request.Headers.Add("Authorization", $"OAuth {authToken}");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GqlVideoTokenResponse>();
        }

        public static async Task<string> GetVideoPlaylist(long videoId, string token, string sig)
        {
            HttpRequestMessage request;
            HttpResponseMessage response;
            try
            {
                request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"https://usher.ttvnw.net/vod/{videoId}.m3u8?sig={sig}&token={token}&allow_source=true&allow_audio_only=true&platform=web&player_backend=mediaplayer&playlist_include_framerate=true&supported_codecs=av1,h265,h264"),
                    Method = HttpMethod.Get
                };
                response = await httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                if (IsAuthException(ex))
                {
                    request = new HttpRequestMessage()
                    {
                        RequestUri = new Uri($"https://twitch-downloader-proxy.twitcharchives.workers.dev/{videoId}.m3u8?sig={sig}&token={token}&allow_source=true&allow_audio_only=true&platform=web&player_backend=mediaplayer&playlist_include_framerate=true&supported_codecs=av1,h265,h264"),
                        Method = HttpMethod.Get
                    };
                    response = await httpClient.SendAsync(request);
                }
                else
                {
                    throw;
                }
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                // Twitch returns 403 Forbidden for (some? all?) sub-only VODs when correct authorization is not provided
                var forbiddenResponse = await response.Content.ReadAsStringAsync();
                if (forbiddenResponse.Contains("vod_manifest_restricted") || forbiddenResponse.Contains("unauthorized_entitlements"))
                {
                    // Return the error string so the caller can choose their error strategy
                    // TODO: We may want to eventually return all 403 responses so the error messages can be parsed and/or logged since more potential errors exist
                    return forbiddenResponse;
                }
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        private static bool IsAuthException(Exception ex)
        {
            while (ex != null)
            {
                if (ex is System.Security.Authentication.AuthenticationException)
                {
                    return true;
                }
                ex = ex.InnerException;
            }
            return false;
        }

        public static async Task<GqlClipResponse> GetClipInfo(object clipId)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{clip(slug:\\\"" + clipId + "\\\"){title,thumbnailURL,createdAt,curator{id,displayName,login},durationSeconds,broadcaster{id,displayName,login},videoOffsetSeconds,video{id},viewCount,game{id,displayName,boxArtURL}}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GqlClipResponse>();
        }

        public static async Task<GqlClipTokenResponse> GetClipLinks(string clipId)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"operationName\":\"VideoAccessToken_Clip\",\"variables\":{\"slug\":\"" + clipId + "\"},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"36b89d2507fce29e5ca551df756d27c1cfe079e2609642b4390aa4c35796eb11\"}}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var gqlClipTokenResponses = await response.Content.ReadFromJsonAsync<GqlClipTokenResponse>();
            if (gqlClipTokenResponses.data.clip.videoQualities is { Length: > 0 })
            {
                Array.Sort(gqlClipTokenResponses.data.clip.videoQualities, new ClipQualityComparer());
            }

            return gqlClipTokenResponses;
        }

        public static async Task<GqlShareClipRenderStatusResponse> GetShareClipRenderStatus(string clipId)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"operationName\":\"ShareClipRenderStatus\",\"variables\":{\"slug\":\"" + clipId + "\"},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"f130048a462a0ac86bb54d653c968c514e9ab9ca94db52368c1179e97b0f16eb\"}}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var renderStatusResponse = await response.Content.ReadFromJsonAsync<GqlShareClipRenderStatusResponse>();
            if (renderStatusResponse.data.clip.assets is not null)
            {
                foreach (var asset in renderStatusResponse.data.clip.assets)
                {
                    Array.Sort(asset.videoQualities, new ClipVideoQualityComparer());
                }
            }

            return renderStatusResponse;
        }

        public static async Task<GqlVideoSearchResponse> GetGqlVideos(string channelName, string cursor = "", int limit = 50, string type = "")
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{user(login:\\\"" + channelName + "\\\"){videos(first: " + limit + "" + (cursor == "" ? "" : ",after:\\\"" + cursor + "\\\"") + (type == "" ? "" : ",type:" + type) + ") { edges { node { title, id, lengthSeconds, previewThumbnailURL(height: 180, width: 320), createdAt, viewCount, game { id, displayName } }, cursor }, pageInfo { hasNextPage, hasPreviousPage }, totalCount }}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kd1unb4b3q4t58fwlpcbzcbnm76a8fp");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GqlVideoSearchResponse>();
        }

        public static async Task<GqlClipSearchResponse> GetGqlClips(string channelName, string period = "LAST_WEEK", string cursor = "", int limit = 50)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{user(login:\\\"" + channelName + "\\\"){clips(first: " + limit + (cursor == "" ? "" : ", after: \\\"" + cursor + "\\\"") +", criteria: { period: " + period + " }) {  edges { cursor, node { id, slug, title, createdAt, curator, { id, displayName }, durationSeconds, thumbnailURL, viewCount, game { id, displayName } } }, pageInfo { hasNextPage, hasPreviousPage } }}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kd1unb4b3q4t58fwlpcbzcbnm76a8fp");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GqlClipSearchResponse>();
        }

        public static async Task<EmoteResponse> GetThirdPartyEmotesMetadata(int streamerId, bool getBttv, bool getFfz, bool getStv, bool allowUnlistedEmotes, ITaskLogger logger, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EmoteResponse emoteResponse = new();

            if (getBttv)
            {
                try
                {
                    emoteResponse.BTTV = await GetBttvEmotesMetadata(streamerId, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogProviderException(ex, "BetterTTV", logger);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (getFfz)
            {
                try
                {
                    emoteResponse.FFZ = await GetFfzEmotesMetadata(streamerId, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogProviderException(ex, "FFZ", logger);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (getStv)
            {
                try
                {
                    emoteResponse.STV = await GetStvEmotesMetadata(streamerId, allowUnlistedEmotes, logger, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogProviderException(ex, "7TV", logger);
                }
            }

            return emoteResponse;

            static void LogProviderException(Exception ex, string providerName, ITaskLogger logger)
            {
                var message = ex switch
                {
                    HttpRequestException { StatusCode: not null } hre => $"{providerName} returned {(int)hre.StatusCode}: {hre.StatusCode}.",
                    TaskCanceledException when ex.Message.Contains("HttpClient.Timeout") => $"{providerName} timed out.",
                    _ => ex.Message
                };

                logger.LogError($"{message} {providerName} emotes may not be present for this session.");
            }
        }

        private static async Task<List<EmoteResponseItem>> GetBttvEmotesMetadata(int streamerId, CancellationToken cancellationToken)
        {
            var globalEmoteRequest = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.betterttv.net/3/cached/emotes/global", UriKind.Absolute));
            using var globalEmoteResponse = await httpClient.SendAsync(globalEmoteRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            globalEmoteResponse.EnsureSuccessStatusCode();
            var BTTV = await globalEmoteResponse.Content.ReadFromJsonAsync<List<BTTVEmote>>(cancellationToken: cancellationToken);

            //Channel might not have BTTV emotes
            try
            {
                var channelEmoteRequest = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://api.betterttv.net/3/cached/users/twitch/{streamerId}", UriKind.Absolute));
                using var channelEmoteResponse = await httpClient.SendAsync(channelEmoteRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                channelEmoteResponse.EnsureSuccessStatusCode();

                var bttvChannel = await channelEmoteResponse.Content.ReadFromJsonAsync<BTTVChannelEmoteResponse>(cancellationToken: cancellationToken);
                BTTV.AddRange(bttvChannel.channelEmotes);
                BTTV.AddRange(bttvChannel.sharedEmotes);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }

            var returnList = new List<EmoteResponseItem>();
            foreach (var emote in BTTV)
            {
                string id = emote.id;
                string name = emote.code;
                string mime = emote.imageType;
                string url = $"https://cdn.betterttv.net/emote/{id}/[scale]x";
                returnList.Add(new EmoteResponseItem() { Id = id, Code = name, ImageType = mime, ImageUrl = url, IsZeroWidth = BttvZeroWidth.Contains(name) });
            }

            return returnList;
        }

        private static async Task<List<EmoteResponseItem>> GetFfzEmotesMetadata(int streamerId, CancellationToken cancellationToken)
        {
            var globalEmoteRequest = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.betterttv.net/3/cached/frankerfacez/emotes/global", UriKind.Absolute));
            using var globalEmoteResponse = await httpClient.SendAsync(globalEmoteRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            globalEmoteResponse.EnsureSuccessStatusCode();
            var FFZ = await globalEmoteResponse.Content.ReadFromJsonAsync<List<FFZEmote>>(cancellationToken: cancellationToken);

            //Channel might not have FFZ emotes
            try
            {
                var channelEmoteRequest = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://api.betterttv.net/3/cached/frankerfacez/users/twitch/{streamerId}", UriKind.Absolute));
                using var channelEmoteResponse = await httpClient.SendAsync(channelEmoteRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                channelEmoteResponse.EnsureSuccessStatusCode();

                var channelEmotes = await channelEmoteResponse.Content.ReadFromJsonAsync<List<FFZEmote>>(cancellationToken: cancellationToken);
                FFZ.AddRange(channelEmotes);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }

            var returnList = new List<EmoteResponseItem>();
            foreach (var emote in FFZ)
            {
                string id = emote.id.ToString();
                string name = emote.code;
                string mime = emote.imageType;
                string url = emote.animated
                    ? $"https://cdn.betterttv.net/frankerfacez_emote/{id}/animated/[scale]"
                    : $"https://cdn.betterttv.net/frankerfacez_emote/{id}/[scale]";
                returnList.Add(new EmoteResponseItem() { Id = id, Code = name, ImageType = mime, ImageUrl = url });
            }

            return returnList;
        }

        private static async Task<List<EmoteResponseItem>> GetStvEmotesMetadata(int streamerId, bool allowUnlistedEmotes, ITaskLogger logger, CancellationToken cancellationToken)
        {
            var globalEmoteRequest = new HttpRequestMessage(HttpMethod.Get, new Uri("https://7tv.io/v3/emote-sets/global", UriKind.Absolute));
            using var globalEmoteResponse = await httpClient.SendAsync(globalEmoteRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            globalEmoteResponse.EnsureSuccessStatusCode();
            var globalEmoteObject = await globalEmoteResponse.Content.ReadFromJsonAsync<STVGlobalEmoteResponse>(cancellationToken: cancellationToken);
            var stvEmotes = globalEmoteObject.emotes;

            // Channel might not be registered on 7tv
            try
            {
                var streamerEmoteRequest = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://7tv.io/v3/users/twitch/{streamerId}", UriKind.Absolute));
                using var streamerEmoteResponse = await httpClient.SendAsync(streamerEmoteRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                streamerEmoteResponse.EnsureSuccessStatusCode();

                var streamerEmoteObject = await streamerEmoteResponse.Content.ReadFromJsonAsync<STVChannelEmoteResponse>(cancellationToken: cancellationToken);
                // Channel might not have emotes setup
                if (streamerEmoteObject.emote_set?.emotes != null)
                {
                    stvEmotes.AddRange(streamerEmoteObject.emote_set.emotes);
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }

            var returnList = new List<EmoteResponseItem>();
            foreach (var stvEmote in stvEmotes)
            {
                STVData emoteData = stvEmote.data;
                STVHost emoteHost = emoteData.host;
                List<STVFile> emoteFiles = emoteHost.files;
                if (emoteFiles.Count == 0) // Sometimes there are no hosted files for the emote
                {
                    logger.LogVerbose($"{stvEmote.name} has no hosted files, skipping.");
                    continue;
                }

                // TODO: Allow and prefer avif when SkiaSharp properly supports it
                string emoteFormat = "";
                foreach (var fileItem in emoteFiles)
                {
                    if (fileItem.format.Equals("webp", StringComparison.OrdinalIgnoreCase)) // Is the emote offered in webp?
                    {
                        emoteFormat = "webp";
                        break;
                    }
                }

                if (emoteFormat is "") // SkiaSharp does not yet properly support avif, only allow webp - see issue lay295#426
                {
                    logger.LogVerbose($"{stvEmote.name} is not available in webp, skipping. Available formats: {string.Join(", ", emoteFiles.Select(x => x.format))}");
                    continue;
                }

                var emoteFlags = emoteData.flags;
                if ((emoteFlags & StvEmoteFlags.ContentTwitchDisallowed) != 0 || (emoteFlags & StvEmoteFlags.Private) != 0)
                {
                    logger.LogVerbose($"{stvEmote.name} has disallowed flags, skipping. Flags: {emoteFlags}.");
                    continue;
                }

                var emoteUrl = $"https:{emoteHost.url}/[scale]x.{emoteFormat}";
                var emoteResponse = new EmoteResponseItem { Id = stvEmote.id, Code = stvEmote.name, ImageType = emoteFormat, ImageUrl = emoteUrl };
                if ((emoteFlags & StvEmoteFlags.ZeroWidth) != 0)
                {
                    emoteResponse.IsZeroWidth = true;
                }

                if (allowUnlistedEmotes || emoteData.listed)
                {
                    returnList.Add(emoteResponse);
                }
            }

            return returnList;
        }

        public static async Task<List<TwitchEmote>> GetThirdPartyEmotes(List<Comment> comments, int streamerId, string cacheFolder, ITaskLogger logger, EmbeddedData embeddedData = null, bool bttv = true, bool ffz = true, bool stv = true, bool allowUnlistedEmotes = true, bool offline = false, CancellationToken cancellationToken = default)
        {
            List<TwitchEmote> returnList = new List<TwitchEmote>();
            HashSet<string> alreadyAdded = new HashSet<string>();

            // No 3rd party emotes are wanted
            if (!bttv && !ffz && !stv)
            {
                return returnList;
            }

            // Load our embedded data from file
            if (embeddedData?.thirdParty != null)
            {
                foreach (EmbedEmoteData emoteData in embeddedData.thirdParty)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var newEmote = new TwitchEmote(emoteData.data, EmoteProvider.ThirdParty, emoteData.imageScale, emoteData.id, emoteData.name, emoteData.isZeroWidth.GetValueOrDefault());
                        returnList.Add(newEmote);
                        alreadyAdded.Add(emoteData.name);
                    }
                    catch (Exception e)
                    {
                        logger.LogVerbose($"An exception occurred while loading embedded emote '{emoteData.name}': {e.Message}.");
                    }
                }
            }

            // Directly return if we are in offline, no need for a network request
            if (offline)
            {
                return returnList;
            }

            DirectoryInfo bttvFolder = new DirectoryInfo(Path.Combine(cacheFolder, "bttv"));
            DirectoryInfo ffzFolder = new DirectoryInfo(Path.Combine(cacheFolder, "ffz"));
            DirectoryInfo stvFolder = new DirectoryInfo(Path.Combine(cacheFolder, "stv"));

            EmoteResponse emoteDataResponse = await GetThirdPartyEmotesMetadata(streamerId, bttv, ffz, stv, allowUnlistedEmotes, logger, cancellationToken);

            if (bttv)
            {
                try
                {
                    await FetchEmoteImages(comments, emoteDataResponse.BTTV, returnList, alreadyAdded, bttvFolder, logger, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogProviderException(ex, "BetterTTV", logger);
                }
            }

            if (ffz)
            {
                try
                {
                    await FetchEmoteImages(comments, emoteDataResponse.FFZ, returnList, alreadyAdded, ffzFolder, logger, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogProviderException(ex, "FFZ", logger);
                }
            }

            if (stv)
            {
                try
                {
                    await FetchEmoteImages(comments, emoteDataResponse.STV, returnList, alreadyAdded, stvFolder, logger, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogProviderException(ex, "7TV", logger);
                }
            }

            return returnList;

            static async Task FetchEmoteImages([AllowNull] IEnumerable<Comment> comments, IEnumerable<EmoteResponseItem> emoteResponse, ICollection<TwitchEmote> returnList,
                ISet<string> alreadyAdded, DirectoryInfo cacheFolder, ITaskLogger logger, CancellationToken cancellationToken)
            {
                if (!cacheFolder.Exists)
                    cacheFolder = CreateDirectory(cacheFolder.FullName);

                IEnumerable<EmoteResponseItem> emoteResponseQuery;
                if (comments is null)
                {
                    emoteResponseQuery = emoteResponse;
                }
                else
                {
                    emoteResponseQuery = from emote in emoteResponse
                        where !alreadyAdded.Contains(emote.Code)
                        let regex = new Regex($@"(?<=^|\s){Regex.Escape(emote.Code)}(?=$|\s)")
                        where comments.Any(comment => regex.IsMatch(comment.message.body))
                        select emote;
                }

                foreach (var emote in emoteResponseQuery)
                {
                    var emoteUrl = emote.ImageUrl.Replace("[scale]", "2");

                    try
                    {
                        var imageData = await GetImage(cacheFolder, emoteUrl, emote.Id, 2, emote.ImageType, logger, cancellationToken);
                        var newEmote = new TwitchEmote(imageData, EmoteProvider.ThirdParty, 2, emote.Id, emote.Code, emote.IsZeroWidth);

                        returnList.Add(newEmote);
                        alreadyAdded.Add(emote.Code);
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        logger.LogWarning($"Got {(int)ex.StatusCode}: {ex.StatusCode} when fetching {emote.Code} ({emoteUrl}).");
                    }
                }
            }

            static void LogProviderException(Exception ex, string providerName, ITaskLogger logger)
            {
                var message = ex switch
                {
                    HttpRequestException { StatusCode: not null } hre => $"{providerName} returned {(int)hre.StatusCode}: {hre.StatusCode}.",
                    TaskCanceledException when ex.Message.Contains("HttpClient.Timeout") => $"{providerName} timed out.",
                    _ => ex.Message
                };

                logger.LogError($"{message} Some {providerName} emotes may not be present for this session.");
            }
        }

        public static async Task<List<TwitchEmote>> GetEmotes(List<Comment> comments, string cacheFolder, ITaskLogger logger, EmbeddedData embeddedData = null, bool offline = false, CancellationToken cancellationToken = default)
        {
            List<TwitchEmote> returnList = new List<TwitchEmote>();
            HashSet<string> alreadyAdded = new HashSet<string>();
            HashSet<string> failedEmotes = new HashSet<string>();

            DirectoryInfo emoteFolder = new DirectoryInfo(Path.Combine(cacheFolder, "emotes"));
            if (!emoteFolder.Exists)
                emoteFolder = CreateDirectory(emoteFolder.FullName);

            // Load our embedded emotes
            if (embeddedData?.firstParty != null)
            {
                foreach (EmbedEmoteData emoteData in embeddedData.firstParty)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        TwitchEmote newEmote = new TwitchEmote(emoteData.data, EmoteProvider.FirstParty, emoteData.imageScale, emoteData.id, emoteData.name);
                        returnList.Add(newEmote);
                        alreadyAdded.Add(emoteData.id);
                    }
                    catch (Exception e)
                    {
                        logger.LogVerbose($"An exception occurred while loading embedded emote '{emoteData.name}': {e.Message}.");
                    }
                }
            }

            // Directly return if we are in offline, no need for a network request
            if (offline)
            {
                return returnList;
            }

            foreach (var comment in comments.Where(c => c.message.fragments != null))
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var id in comment.message.fragments
                             .Select(f => f.emoticon?.emoticon_id)
                             .Where(id => id != null && !alreadyAdded.Contains(id) && !failedEmotes.Contains(id)))
                {
                    try
                    {
                        byte[] bytes = await GetImage(emoteFolder, $"https://static-cdn.jtvnw.net/emoticons/v2/{id}/default/dark/2.0", id, 2, "png", logger, cancellationToken);
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

            return returnList;
        }

        public static async Task<List<EmbedChatBadge>> GetChatBadgesData(List<Comment> comments, int streamerId, CancellationToken cancellationToken = new())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // TODO: this currently only does twitch badges, but we could also support FFZ, BTTV, 7TV, etc badges!
            // TODO: would want to make this configurable as we do for emotes though...
            var globalBadgeRequest = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{badges{imageURL(size:DOUBLE),description,title,setID,version}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            globalBadgeRequest.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var globalBadgeResponse = await httpClient.SendAsync(globalBadgeRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            globalBadgeResponse.EnsureSuccessStatusCode();
            var globalBadges = (await globalBadgeResponse.Content.ReadFromJsonAsync<GqlGlobalBadgeResponse>(cancellationToken: cancellationToken)).data.badges.GroupBy(x => x.name).ToDictionary(x => x.Key, x => x.ToList());

            var subBadgeRequest = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{user(id: " + streamerId + "){broadcastBadges{imageURL(size:DOUBLE),description,title,setID,version}}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            subBadgeRequest.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var subBadgeResponse = await httpClient.SendAsync(subBadgeRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            subBadgeResponse.EnsureSuccessStatusCode();
            var subBadges = (await subBadgeResponse.Content.ReadFromJsonAsync<GqlSubBadgeResponse>(cancellationToken: cancellationToken)).data.user.badges.GroupBy(x => x.name).ToDictionary(x => x.Key, x => x.ToList());

            List<EmbedChatBadge> badges = new List<EmbedChatBadge>();

            var nameList = comments.Where(comment => comment.message.user_badges != null)
                .SelectMany(comment => comment.message.user_badges)
                .Where(badge => !string.IsNullOrWhiteSpace(badge._id))
                .Where(badge => globalBadges.ContainsKey(badge._id) || subBadges.ContainsKey(badge._id))
                .Select(badge => badge._id).Distinct();

            foreach (var name in nameList)
            {
                Dictionary<string, ChatBadgeData> versions = new();
                if (globalBadges.TryGetValue(name, out var globalBadge))
                {
                    foreach (var badge in globalBadge)
                    {
                        versions[badge.version] = new()
                        {
                            title = badge.title,
                            description = badge.description,
                            url = badge.image_url_2x
                        };
                    }
                }

                //Prefer channel specific badges over global ones
                if (subBadges.TryGetValue(name, out var subBadge))
                {
                    foreach (var badge in subBadge)
                    {
                        versions[badge.version] = new()
                        {
                            title = badge.title,
                            description = badge.description,
                            url = badge.image_url_2x
                        };
                    }
                }

                badges.Add(new EmbedChatBadge() { name = name, versions = versions });
            }

            return badges;
        }

        public static async Task<List<ChatBadge>> GetChatBadges(List<Comment> comments, int streamerId, string cacheFolder, ITaskLogger logger, EmbeddedData embeddedData = null, bool offline = false, CancellationToken cancellationToken = default)
        {
            List<ChatBadge> returnList = new List<ChatBadge>();
            HashSet<string> alreadyAdded = new HashSet<string>();

            // Load our embedded data from file
            if (embeddedData?.twitchBadges != null)
            {
                foreach (EmbedChatBadge data in embeddedData.twitchBadges)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        ChatBadge newBadge = new ChatBadge(data.name, data.versions);
                        returnList.Add(newBadge);
                        alreadyAdded.Add(data.name);
                    }
                    catch (Exception e)
                    {
                        logger.LogVerbose($"An exception occurred while loading embedded badge '{data.name}': {e.Message}.");
                    }
                }
            }

            // Directly return if we are in offline, no need for a network request
            if (offline)
            {
                return returnList;
            }

            List<EmbedChatBadge> badgesData = await GetChatBadgesData(comments, streamerId, cancellationToken);

            DirectoryInfo badgeFolder = new DirectoryInfo(Path.Combine(cacheFolder, "badges"));
            if (!badgeFolder.Exists)
                badgeFolder = CreateDirectory(badgeFolder.FullName);

            foreach(var badge in badgesData)
            {
                try
                {
                    Dictionary<string, ChatBadgeData> versions = new();

                    if (alreadyAdded.Contains(badge.name))
                        continue;

                    foreach (var (version, data) in badge.versions)
                    {
                        string id = data.url.Split('/')[^2];
                        byte[] bytes = await GetImage(badgeFolder, data.url, id, 2, "png", logger, cancellationToken);
                        versions.Add(version, new ChatBadgeData
                        {
                            title = data.title,
                            description = data.description,
                            bytes = bytes
                        });
                    }

                    returnList.Add(new ChatBadge(badge.name, versions));
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
            }

            return returnList;
        }

        public static async Task<Dictionary<string, SKBitmap>> GetEmojis(string cacheFolder, EmojiVendor emojiVendor, ITaskLogger logger, CancellationToken cancellationToken = default)
        {
            var returnCache = new Dictionary<string, SKBitmap>();

            if (emojiVendor == EmojiVendor.None)
                return returnCache;

            var emojiFolder = Path.Combine(cacheFolder, "emojis", emojiVendor.EmojiFolder());
            if (!Directory.Exists(emojiFolder))
                CreateDirectory(emojiFolder);

            var enumerationOptions = new EnumerationOptions { MatchType = MatchType.Simple, MatchCasing = MatchCasing.CaseInsensitive };
            var emojiFiles = Directory.GetFiles(emojiFolder, "*.png", enumerationOptions);

            if (emojiFiles.Length < emojiVendor.EmojiCount())
            {
                var emojiZipPath = Path.Combine(emojiFolder, Path.GetRandomFileName());
                try
                {
                    using (var ms = emojiVendor.MemoryStream())
                    {
                        await using var fs = File.OpenWrite(emojiZipPath);
                        await ms.CopyToAsync(fs, cancellationToken);
                    }

                    using var archive = ZipFile.OpenRead(emojiZipPath);
                    var emojiAssetsPath = emojiVendor.AssetPath();
                    var emojis = archive.Entries
                        .Where(x => !string.IsNullOrWhiteSpace(x.Name) && Path.GetDirectoryName(x.FullName) == emojiAssetsPath);

                    foreach (var emoji in emojis)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var filePath = Path.Combine(emojiFolder, emoji.Name.ToUpper().Replace(emojiVendor.UnicodeSequenceSeparator(), ' '));
                        if (!File.Exists(filePath))
                        {
                            try
                            {
                                emoji.ExtractToFile(filePath);
                            }
                            catch { /* Being written by a parallel process? */ }
                        }
                    }

                    emojiFiles = Directory.GetFiles(emojiFolder, "*.png", enumerationOptions);
                }
                finally
                {
                    if (File.Exists(emojiZipPath))
                    {
                        File.Delete(emojiZipPath);
                    }
                }
            }

            var failedToDecode = 0;
            foreach (var emojiPath in emojiFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var fs = File.OpenRead(emojiPath);
                var emojiImage = SKBitmap.Decode(fs);

                if (emojiImage is null)
                {
                    failedToDecode++;
                    logger.LogVerbose($"Failed to decode emoji {Path.GetFileName(emojiPath)}, skipping.");
                    continue;
                }

                returnCache.Add(Path.GetFileNameWithoutExtension(emojiPath), emojiImage);
            }

            if (failedToDecode > 0)
            {
                logger.LogWarning($"{failedToDecode} emojis failed to decode.");
            }

            return returnCache;
        }

        public static async Task<List<CheerEmote>> GetBits(List<Comment> comments, string cacheFolder, string channelId, ITaskLogger logger, EmbeddedData embeddedData = null, bool offline = false, CancellationToken cancellationToken = default)
        {
            List<CheerEmote> returnList = new List<CheerEmote>();
            HashSet<string> alreadyAdded = new HashSet<string>();

            // Load our embedded data from file
            if (embeddedData?.twitchBits != null)
            {
                foreach (EmbedCheerEmote data in embeddedData.twitchBits)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
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
                    catch (Exception e)
                    {
                        logger.LogVerbose($"An exception occurred while loading embedded cheermote '{data.prefix}': {e.Message}.");
                    }
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
                Content = new StringContent("{\"query\":\"query{cheerConfig{groups{nodes{id, prefix, tiers{bits}}, templateURL}},user(id:\\\"" + channelId + "\\\"){cheer{cheerGroups{nodes{id,prefix,tiers{bits}},templateURL}}}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var cheerResponseMessage = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            cheerResponseMessage.EnsureSuccessStatusCode();
            var cheerResponse = await cheerResponseMessage.Content.ReadFromJsonAsync<GqlCheerResponse>(cancellationToken: cancellationToken);

            DirectoryInfo bitFolder = new DirectoryInfo(Path.Combine(cacheFolder, "bits"));
            if (!bitFolder.Exists)
                bitFolder = CreateDirectory(bitFolder.FullName);

            if (cheerResponse?.data != null)
            {
                List<CheerGroup> groupList = new List<CheerGroup>();

                foreach (CheerGroup group in cheerResponse.data.cheerConfig.groups)
                {
                    groupList.Add(group);
                }

                if (cheerResponse.data.user?.cheer?.cheerGroups != null)
                {
                    foreach (var group in cheerResponse.data.user.cheer.cheerGroups)
                    {
                        groupList.Add(group);
                    }
                }

                foreach (CheerGroup cheerGroup in groupList)
                {
                    string templateURL = cheerGroup.templateURL;

                    var cheerNodesQuery = from node in cheerGroup.nodes
                        where !alreadyAdded.Contains(node.prefix)
                        let regex = new Regex($@"(?<=^|\s){Regex.Escape(node.prefix)}(?=[1-9])")
                        where comments
                            .Where(comment => comment.message.bits_spent > 0)
                            .Any(comment => regex.IsMatch(comment.message.body))
                        select node;

                    foreach (CheerNode node in cheerNodesQuery)
                    {
                        string prefix = node.prefix;
                        try
                        {
                            List<KeyValuePair<int, TwitchEmote>> tierList = new List<KeyValuePair<int, TwitchEmote>>();
                            CheerEmote newEmote = new CheerEmote() { prefix = prefix, tierList = tierList };
                            foreach (Tier tier in node.tiers)
                            {
                                int minBits = tier.bits;
                                string url = templateURL.Replace("PREFIX", node.prefix.ToLower()).Replace("BACKGROUND", "dark").Replace("ANIMATION", "animated").Replace("TIER", tier.bits.ToString()).Replace("SCALE.EXTENSION", "2.gif");
                                var bytes = await GetImage(bitFolder, url, node.id + tier.bits, 2, "gif", logger, cancellationToken);
                                TwitchEmote emote = new TwitchEmote(bytes, EmoteProvider.FirstParty, 2, prefix + minBits, prefix + minBits);
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

        public static FileInfo ClaimFile(string path, Func<FileInfo, FileInfo> fileAlreadyExistsCallback, ITaskLogger logger)
        {
            var fullPath = Path.GetFullPath(path);
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Exists)
            {
                if (fileAlreadyExistsCallback is null)
                {
                    logger.LogWarning($"{nameof(fileAlreadyExistsCallback)} was null.");
                }
                else
                {
                    fileInfo = fileAlreadyExistsCallback(fileInfo);

                    if (fileInfo is null)
                    {
                        // I would prefer to not throw here, but the alternative is refactoring the task queue :/
                        throw new FileNotFoundException("No destination file was provided, aborting.");
                    }

                    if (fullPath != fileInfo.FullName)
                    {
                        logger.LogInfo($"'{fullPath}' will be renamed to '{fileInfo.FullName}'");
                    }
                }
            }

            var directory = fileInfo.Directory;
            if (directory is not null && !directory.Exists)
            {
                CreateDirectory(directory.FullName);
            }

            return fileInfo;
        }

        public static void CleanUpClaimedFile([AllowNull] FileInfo fileInfo, [AllowNull] FileStream fileStream, ITaskLogger logger)
        {
            if (fileInfo is null)
            {
                return;
            }

            fileInfo.Refresh();
            if (fileInfo.Exists && fileInfo.Length == 0)
            {
                try
                {
                    fileStream?.Dispose();
                }
                catch
                {
                    // Ignored
                }

                try
                {
                    fileInfo.Delete();
                }
                catch (Exception e)
                {
                    logger.LogWarning($"Failed to clean up {fileInfo.FullName}: {e.Message}");
                }
            }
        }

        /// <inheritdoc cref="Directory.CreateDirectory"/>
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

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("osx")]
        private static void SetDirectoryPermissions(string path)
        {
            var folderInfo = new UnixFileInfo(path);
            folderInfo.FileAccessPermissions = FileAccessPermissions.UserReadWriteExecute |
                                               FileAccessPermissions.GroupRead | FileAccessPermissions.GroupWrite |
                                               FileAccessPermissions.OtherRead | FileAccessPermissions.OtherWrite;
            folderInfo.Refresh();
        }

        /// <summary>
        /// Cleans up any unmanaged cache files from previous runs that were interrupted before cleaning up
        /// </summary>
        public static async Task CleanupAbandonedVideoCaches(string cacheFolder, Func<DirectoryInfo[], DirectoryInfo[]> itemsToDeleteCallback, ITaskLogger logger)
        {
            if (!Directory.Exists(cacheFolder))
            {
                return;
            }

            if (itemsToDeleteCallback == null)
            {
                logger.LogWarning($"{nameof(itemsToDeleteCallback)} was null.");
                return;
            }

            var videoFolderRegex = new Regex(@"\d+_\d+$", RegexOptions.RightToLeft);
            var allCacheDirectories = Directory.GetDirectories(cacheFolder);

            var oldVideoCaches = (from directory in allCacheDirectories
                    where videoFolderRegex.IsMatch(directory)
                    let directoryInfo = new DirectoryInfo(directory)
                    where DateTime.UtcNow.Ticks - directoryInfo.LastWriteTimeUtc.Ticks > TimeSpan.TicksPerDay * 7
                    select directoryInfo)
                .ToArray();

            if (oldVideoCaches.Length == 0)
            {
                return;
            }

            var toDelete = await Task.Run(() => itemsToDeleteCallback(oldVideoCaches));

            if (toDelete == null || toDelete.Length == 0)
            {
                return;
            }

            var wasDeleted = 0;
            foreach (var directory in toDelete)
            {
                try
                {
                    Directory.Delete(directory.FullName, true);
                    wasDeleted++;
                    logger.LogVerbose($"Deleted '{directory.FullName}' successfully.");
                }
                catch (Exception e)
                {
                    logger.LogVerbose($"Could not delete '{directory.FullName}': {e.Message}.");
                }
            }

            logger.LogInfo(toDelete.Length == wasDeleted
                ? $"{wasDeleted} old video caches were deleted."
                : $"{wasDeleted} old video caches were deleted, {toDelete.Length - wasDeleted} could not be deleted.");
        }

        public static async Task<GqlUserIdResponse> GetUserIds(IEnumerable<string> nameList)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{users(logins:[" + string.Join(",", nameList.Select(x => "\\\"" + x + "\\\"").ToArray()) + "]){id}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GqlUserIdResponse>();
        }

        public static async Task<GqlUserInfoResponse> GetUserInfo(IEnumerable<string> idList)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{users(ids:[" + string.Join(",", idList.Select(x => "\\\"" + x + "\\\"").ToArray()) + "]){id,displayName,login,createdAt,updatedAt,description,profileImageURL(width:300)}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GqlUserInfoResponse>();
        }

        public static async Task<byte[]> GetImage(DirectoryInfo cacheDir, string imageUrl, string imageId, int imageScale, string imageType, ITaskLogger logger, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            cacheDir.Refresh();
            if (!cacheDir.Exists)
            {
                CreateDirectory(cacheDir.FullName);
                cacheDir.Refresh();
            }

            byte[] imageBytes;

            var filePath = Path.Combine(cacheDir.FullName, $"{imageId}_{imageScale}.{imageType}");
            var file = new FileInfo(filePath);

            if (file.Exists)
            {
                try
                {
                    await using var fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                    imageBytes = new byte[fs.Length];
                    _ = await fs.ReadAsync(imageBytes, cancellationToken);

                    if (imageBytes.Length > 0)
                    {
                        using var ms = new MemoryStream(imageBytes);
                        using var codec = SKCodec.Create(ms, out var result);

                        if (codec is not null)
                        {
                            return imageBytes;
                        }

                        logger.LogVerbose($"Failed to decode {imageId} from cache: {result}");
                    }

                    // Delete the corrupted image
                    file.Delete();
                }
                catch (Exception e) when (e is IOException or SecurityException)
                {
                    // File being written to by parallel process? Maybe. Can just fall back to HTTP request.
                    logger.LogVerbose($"Failed to read from or delete {file.Name}: {e.Message}");
                }
            }

            imageBytes = await httpClient.GetByteArrayAsync(imageUrl, cancellationToken);

            try
            {
                await using var fs = file.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
                await fs.WriteAsync(imageBytes, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogVerbose($"Failed to open or write to {file.Name}: {e.Message}");
            }

            return imageBytes;
        }

        /// <remarks>When a given video has only 1 chapter, data.video.moments.edges will be empty.</remarks>
        public static async Task<GqlVideoChapterResponse> GetVideoChapters(long videoId)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"extensions\":{\"persistedQuery\":{\"sha256Hash\":\"8d2793384aac3773beab5e59bd5d6f585aedb923d292800119e03d40cd0f9b41\",\"version\":1}},\"operationName\":\"VideoPlayer_ChapterSelectButtonVideo\",\"variables\":{\"videoID\":\"" + videoId + "\"}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var chapterResponse = await response.Content.ReadFromJsonAsync<GqlVideoChapterResponse>();
            chapterResponse.data.video.moments ??= new VideoMomentConnection { edges = new List<VideoMomentEdge>() };

            // For some reason durations can be negative sometimes
            foreach (var edge in chapterResponse.data.video.moments.edges)
            {
                if (edge.node.durationMilliseconds < 0)
                {
                    edge.node.durationMilliseconds = 0;
                }
            }

            // When downloading VODs of currently-airing streams, the last chapter lacks a duration
            if (chapterResponse.data.video.moments.edges.LastOrDefault() is { } lastEdge && lastEdge.node.durationMilliseconds is 0)
            {
                lastEdge.node.durationMilliseconds = lastEdge.node.video.lengthSeconds * 1000 - lastEdge.node.positionMilliseconds;
            }

            return chapterResponse;
        }

        public static async Task<GqlVideoChapterResponse> GetOrGenerateVideoChapters(long videoId, VideoInfo videoInfo)
        {
            var chapterResponse = await GetVideoChapters(videoId);

            // Video has only 1 chapter, generate a bogus video chapter with the information we have available.
            if (chapterResponse.data.video.moments.edges.Count == 0)
            {
                chapterResponse.data.video.moments.edges.Add(
                    GenerateVideoMomentEdge(0, videoInfo.lengthSeconds, videoInfo.game?.id, videoInfo.game?.displayName, videoInfo.game?.displayName, videoInfo.game?.boxArtURL
                    ));
            }

            return chapterResponse;
        }

        public static VideoMomentEdge GenerateClipChapter(ShareClipRenderStatusClip clipInfo)
        {
            return GenerateVideoMomentEdge(0, clipInfo.durationSeconds, clipInfo.game?.id, clipInfo.game?.displayName, clipInfo.game?.displayName, clipInfo.game?.boxArtURL);
        }

        private static VideoMomentEdge GenerateVideoMomentEdge(int startSeconds, int lengthSeconds, string gameId = null, string gameDisplayName = null, string gameDescription = null, string gameBoxArtUrl = null)
        {
            gameId ??= "-1";
            gameDisplayName ??= "Unknown";
            gameDescription ??= "Unknown";
            gameBoxArtUrl ??= "";

            return new VideoMomentEdge
            {
                node = new VideoMoment
                {
                    id = "",
                    _type = "GAME_CHANGE",
                    positionMilliseconds = startSeconds,
                    durationMilliseconds = lengthSeconds * 1000,
                    description = gameDescription,
                    subDescription = "",
                    details = new GameChangeMomentDetails
                    {
                        game = new Game
                        {
                            id = gameId,
                            displayName = gameDisplayName,
                            boxArtURL = gameBoxArtUrl.Replace("{width}", "40").Replace("{height}", "53")
                        }
                    }
                }
            };
        }
    }
}