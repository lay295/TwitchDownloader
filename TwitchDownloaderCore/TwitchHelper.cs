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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Extensions;
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
            Timeout = TimeSpan.FromSeconds(60)
        };

        private static readonly string[] BttvZeroWidth = ["SoSnowy", "IceCold", "SantaHat", "TopHat", "ReinDeer", "CandyCane", "cvMask", "cvHazmat"];
        private const string SEVEN_TV_PROXY_HOST = "7tv-imageproxy.twitcharchives.workers.dev";

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

        public static async Task<GqlVideoTokenResponse> GetLiveStreamToken(string channelLogin, string authToken)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"operationName\":\"PlaybackAccessToken_Template\",\"query\":\"query PlaybackAccessToken_Template($login: String!, $isLive: Boolean!, $vodID: ID!, $isVod: Boolean!, $playerType: String!) {  streamPlaybackAccessToken(channelName: $login, params: {platform: \\\"web\\\", playerBackend: \\\"mediaplayer\\\", playerType: $playerType}) @include(if: $isLive) {    value    signature    __typename  }  videoPlaybackAccessToken(id: $vodID, params: {platform: \\\"web\\\", playerBackend: \\\"mediaplayer\\\", playerType: $playerType}) @include(if: $isVod) {    value    signature    __typename  }}\",\"variables\":{\"isLive\":true,\"login\":\"" + channelLogin + "\",\"isVod\":false,\"vodID\":\"\",\"playerType\":\"site\"}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            if (!string.IsNullOrWhiteSpace(authToken))
                request.Headers.Add("Authorization", $"OAuth {authToken}");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GqlVideoTokenResponse>();
        }

        public static async Task<string> GetLiveStreamPlaylist(string channelLogin, string token, string sig)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri($"https://usher.ttvnw.net/api/channel/hls/{channelLogin}.m3u8?sig={sig}&token={Uri.EscapeDataString(token)}&allow_source=true&allow_audio_only=true&platform=web&player_backend=mediaplayer&playlist_include_framerate=true&supported_codecs=av1,h265,h264&fast_bread=true"),
                Method = HttpMethod.Get
            };
            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<string> GetVideoPlaylist(long videoId, string token, string sig)
        {
            HttpRequestMessage request;
            HttpResponseMessage response;
            try
            {
                request = new HttpRequestMessage()
                {
                    RequestUri = new Uri($"https://usher.ttvnw.net/vod/{videoId}.m3u8?sig={sig}&token={token}&allow_source=true&allow_audio_only=true&include_unavailable=true&platform=web&player_backend=mediaplayer&playlist_include_framerate=true&supported_codecs=av1,h265,h264"),
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
                        RequestUri = new Uri($"https://twitch-downloader-proxy.twitcharchives.workers.dev/{videoId}.m3u8?sig={sig}&token={token}&allow_source=true&allow_audio_only=true&include_unavailable=true&platform=web&player_backend=mediaplayer&playlist_include_framerate=true&supported_codecs=av1,h265,h264"),
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

        /// <summary>
        /// Twitch VOD segments are written to public, unauthenticated CDN buckets whose path is
        /// deterministically derived from the broadcast's login, stream id, and start time — even
        /// when the channel has "Store past broadcasts" disabled. This fetches the live/most-recent
        /// broadcast metadata needed to reconstruct that path.
        /// </summary>
        public static async Task<GqlStreamMetadataResponse> GetStreamMetadata(string channelLogin)
        {
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{user(login:\\\"" + channelLogin + "\\\"){id,login,displayName,stream{id,createdAt}}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GqlStreamMetadataResponse>();
        }

        // Public Twitch VOD CDN hosts. Segments for a given broadcast live on exactly one of these,
        // so the reconstructed path must be probed against each until one responds.
        private static readonly string[] VodCdnDomains =
        {
            "https://d2e2de1etea730.cloudfront.net",
            "https://dqrpb9wgowsf5.cloudfront.net",
            "https://ds0h3roq6wcgc.cloudfront.net",
            "https://d2nvs31859zcd8.cloudfront.net",
            "https://d2aba1wr3818hz.cloudfront.net",
            "https://d3c27h4odz752x.cloudfront.net",
            "https://dgeft87wbj63p.cloudfront.net",
            "https://d1m7jfoe9zdc1j.cloudfront.net",
            "https://d3vd9lfkzbru3h.cloudfront.net",
            "https://d2vjef5jvl6bfs.cloudfront.net",
            "https://d1ymi26ma8va5x.cloudfront.net",
            "https://d1mhjrowxxagfy.cloudfront.net",
            "https://ddacn6pr5v0tl.cloudfront.net",
            "https://d3aqoihi2n8ty8.cloudfront.net",
            "https://vod-secure.twitch.tv",
            "https://vod-metro.twitch.tv",
            "https://vod-pop-secure.twitch.tv",
        };

        // Quality renditions to look for, best first. The source rendition ("chunked") is preferred,
        // but it is not always retained, so transcodes are probed too. "audio_only" is last.
        private static readonly string[] VodQualityRenditions =
        {
            "chunked", "1080p60", "1080p30", "720p60", "720p30", "480p30", "360p30", "160p30", "audio_only",
        };

        /// <summary>
        /// Friendly display name for a DVR rendition folder (e.g. <c>chunked</c> → <c>Source</c>).
        /// </summary>
        public static string DescribeVodRendition(string rendition) => rendition switch
        {
            "chunked" => "Source",
            "audio_only" => "Audio Only",
            _ => rendition,
        };

        /// <summary>
        /// Resolves the base URL (<c>{host}/{pathSegment}</c>) of the CDN host serving a hidden broadcast,
        /// then probes which quality renditions are actually available under it. Returns a null base URL
        /// and empty list if the broadcast cannot be located (e.g. its segments have been purged).
        /// </summary>
        public static async Task<(string baseUrl, IReadOnlyList<string> qualities)> RecoverHiddenVodQualities(
            string channelLogin, string streamId, DateTimeOffset startTime, ITaskLogger logger = null, CancellationToken cancellationToken = default)
        {
            var baseUrl = await ResolveHiddenVodBaseUrlAsync(channelLogin.ToLowerInvariant(), streamId, startTime, cancellationToken);
            if (baseUrl is null)
            {
                logger?.LogWarning($"Could not locate the broadcast's segments on any known Twitch CDN for '{channelLogin}' (stream {streamId}).");
                return (null, Array.Empty<string>());
            }

            var qualities = await ProbeAvailableQualitiesAsync(baseUrl, cancellationToken);
            return (baseUrl, qualities);
        }

        /// <summary>
        /// Reconstructs the public DVR playlist (<c>index-dvr.m3u8</c>) URL for a broadcast and returns the
        /// requested quality rendition if it exists, otherwise the best available one. Returns
        /// <see langword="null"/> if the broadcast cannot be located.
        /// </summary>
        /// <param name="channelLogin">The broadcaster's login (lowercase channel name).</param>
        /// <param name="streamId">The broadcast/stream id from <see cref="GetStreamMetadata"/>.</param>
        /// <param name="startTime">The broadcast start time (stream <c>createdAt</c>).</param>
        /// <param name="preferredQuality">Rendition folder to prefer (e.g. <c>chunked</c>, <c>720p60</c>); null = best available.</param>
        public static async Task<string> RecoverHiddenVodPlaylistUrl(string channelLogin, string streamId, DateTimeOffset startTime, string preferredQuality = null, ITaskLogger logger = null, CancellationToken cancellationToken = default)
        {
            var baseUrl = await ResolveHiddenVodBaseUrlAsync(channelLogin.ToLowerInvariant(), streamId, startTime, cancellationToken);
            if (baseUrl is null)
            {
                logger?.LogWarning($"Could not recover a hidden VOD for '{channelLogin}' (stream {streamId}). The broadcast may be too old — DVR segments are typically purged within a day or two.");
                return null;
            }

            // Try the requested rendition first, then fall through the priority list.
            var ordered = new List<string>();
            if (!string.IsNullOrWhiteSpace(preferredQuality))
                ordered.Add(preferredQuality);
            foreach (var q in VodQualityRenditions)
                if (!ordered.Contains(q))
                    ordered.Add(q);

            foreach (var quality in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var url = $"{baseUrl}/{quality}/index-dvr.m3u8";
                if (await UrlExistsAsync(url, cancellationToken))
                {
                    if (!string.IsNullOrWhiteSpace(preferredQuality) && quality != preferredQuality)
                        logger?.LogWarning($"Requested quality '{preferredQuality}' was unavailable; recovered '{quality}' instead: {url}");
                    else
                        logger?.LogInfo($"Recovered hidden VOD playlist ({quality}) at {url}");
                    return url;
                }
            }

            logger?.LogWarning($"Located the broadcast on the CDN but none of the known quality renditions were available for '{channelLogin}' (stream {streamId}).");
            return null;
        }

        /// <summary>
        /// Finds the CDN host serving a broadcast and returns its base URL (<c>{host}/{pathSegment}</c>),
        /// probing a small timestamp window for rounding. Source is tried first across all hosts; if it
        /// is not retained, transcode renditions are tried at the exact timestamp. Returns <see langword="null"/>
        /// if no host serves the broadcast.
        /// </summary>
        private static async Task<string> ResolveHiddenVodBaseUrlAsync(string login, string streamId, DateTimeOffset startTime, CancellationToken cancellationToken)
        {
            var startUnix = startTime.ToUnixTimeSeconds();

            // The path hash uses integer unix seconds, but the reported start time can be off by a
            // second due to sub-second rounding, so probe a tiny window around it (exact first).
            var candidateTimestamps = new List<long> { startUnix };
            for (long offset = 1; offset <= 2; offset++)
            {
                candidateTimestamps.Add(startUnix - offset);
                candidateTimestamps.Add(startUnix + offset);
            }

            foreach (var candidateUnix in candidateTimestamps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var baseUrl = await FindServingCdnHostAsync(BuildVodPathSegment(login, streamId, candidateUnix), "chunked", cancellationToken);
                if (baseUrl is not null)
                    return baseUrl;
            }

            // Source wasn't found anywhere; the broadcast may only have transcodes retained.
            var exactSegment = BuildVodPathSegment(login, streamId, startUnix);
            foreach (var quality in VodQualityRenditions)
            {
                if (quality == "chunked") continue;
                cancellationToken.ThrowIfCancellationRequested();
                var baseUrl = await FindServingCdnHostAsync(exactSegment, quality, cancellationToken);
                if (baseUrl is not null)
                    return baseUrl;
            }

            return null;
        }

        /// <summary>Probes all known renditions under <paramref name="baseUrl"/> concurrently and returns those that exist, best first.</summary>
        private static async Task<IReadOnlyList<string>> ProbeAvailableQualitiesAsync(string baseUrl, CancellationToken cancellationToken)
        {
            var checks = VodQualityRenditions
                .Select(async q => await UrlExistsAsync($"{baseUrl}/{q}/index-dvr.m3u8", cancellationToken) ? q : null)
                .ToArray();
            var results = await Task.WhenAll(checks);
            return results.Where(q => q is not null).Select(q => q!).ToList();
        }

        private static async Task<bool> UrlExistsAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildVodPathSegment(string login, string streamId, long startUnix)
        {
            var baseString = $"{login}_{streamId}_{startUnix}";
            var hash = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(baseString)))[..20];
            return $"{hash}_{baseString}";
        }

        /// <summary>
        /// Probes every known CDN host concurrently for <c>{host}/{pathSegment}/{quality}/index-dvr.m3u8</c>
        /// and returns the base URL (<c>{host}/{pathSegment}</c>) of the first host that responds, cancelling
        /// the remaining probes. Returns <see langword="null"/> if no host serves it.
        /// </summary>
        private static async Task<string> FindServingCdnHostAsync(string pathSegment, string quality, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var probes = VodCdnDomains.Select(async domain =>
            {
                var baseUrl = $"{domain}/{pathSegment}";
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head, $"{baseUrl}/{quality}/index-dvr.m3u8");
                    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    return response.IsSuccessStatusCode ? baseUrl : null;
                }
                catch
                {
                    // Host unreachable, rejected the request, or the probe was cancelled by a winner.
                    return null;
                }
            }).ToList();

            while (probes.Count > 0)
            {
                var finished = await Task.WhenAny(probes);
                probes.Remove(finished);

                var baseUrl = await finished;
                if (baseUrl is not null)
                {
                    await cts.CancelAsync(); // stop the remaining in-flight probes
                    return baseUrl;
                }
            }

            return null;
        }

        public static async Task<GqlClipResponse> GetClipInfo(object clipId, string oauth = null)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"query\":\"query{clip(slug:\\\"" + clipId + "\\\"){title,thumbnailURL,createdAt,curator{id,displayName,login},durationSeconds,broadcaster{id,displayName,login},videoOffsetSeconds,video{id},viewCount,game{id,displayName,boxArtURL}}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            if (!string.IsNullOrWhiteSpace(oauth))
                request.Headers.Add("Authorization", $"OAuth {oauth}");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GqlClipResponse>();
        }

        public static async Task<GqlClipTokenResponse> GetClipLinks(string clipId, string oauth = null)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"operationName\":\"VideoAccessToken_Clip\",\"variables\":{\"slug\":\"" + clipId + "\"},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"36b89d2507fce29e5ca551df756d27c1cfe079e2609642b4390aa4c35796eb11\"}}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            if (!string.IsNullOrWhiteSpace(oauth))
                request.Headers.Add("Authorization", $"OAuth {oauth}");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var gqlClipTokenResponses = await response.Content.ReadFromJsonAsync<GqlClipTokenResponse>();
            if (gqlClipTokenResponses.data.clip.videoQualities is { Length: > 0 })
            {
                Array.Sort(gqlClipTokenResponses.data.clip.videoQualities, new ClipQualityComparer());
            }

            return gqlClipTokenResponses;
        }

        public static async Task<GqlShareClipRenderStatusResponse> GetShareClipRenderStatus(string clipId, string oauth = null)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"operationName\":\"ShareClipRenderStatus\",\"variables\":{\"slug\":\"" + clipId + "\"},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"761bc03a4b100ec4f73fa78a5011847bb8ad7693d223d055fd013f79390acd41\"}}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            if (!string.IsNullOrWhiteSpace(oauth))
                request.Headers.Add("Authorization", $"OAuth {oauth}");
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
                    HttpRequestException { InnerException: not null } when ex.Message.Contains("SSL") => $"{ex.Message.Remove(ex.Message.Length - 1)}: {ex.InnerException.Message}",
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

                // // 7TV emotes are not available over TLS 1.2, so we need to use a proxy for now
                // emoteHost.url = Regex.Replace(
                //     emoteHost.url,
                //     @"^(//)[^/]+",
                //     m => $"{m.Groups[1].Value}{SEVEN_TV_PROXY_HOST}"
                // );

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
            // No 3rd party emotes are wanted
            if (!bttv && !ffz && !stv)
            {
                return new List<TwitchEmote>();
            }

            var emotes = new Dictionary<string, TwitchEmote>();

            // Load our embedded data from file
            if (embeddedData?.thirdParty != null)
            {
                emotes.EnsureCapacity(embeddedData.thirdParty.Count);
                foreach (EmbedEmoteData emoteData in embeddedData.thirdParty)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var newEmote = new TwitchEmote(emoteData.data, null, EmoteProvider.ThirdParty, emoteData.imageScale, emoteData.id, emoteData.name, emoteData.isZeroWidth.GetValueOrDefault());

                        if (!emotes.TryAdd(emoteData.name, newEmote))
                        {
                            newEmote.Dispose();
                            logger.LogVerbose($"Tried to add duplicate emote from embedded data: {emoteData.name}.");
                        }
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
                return emotes.Values.ToList();
            }

            DirectoryInfo bttvFolder = new DirectoryInfo(Path.Combine(cacheFolder, "bttv"));
            DirectoryInfo ffzFolder = new DirectoryInfo(Path.Combine(cacheFolder, "ffz"));
            DirectoryInfo stvFolder = new DirectoryInfo(Path.Combine(cacheFolder, "stv"));

            EmoteResponse emoteDataResponse = await GetThirdPartyEmotesMetadata(streamerId, bttv, ffz, stv, allowUnlistedEmotes, logger, cancellationToken);

            // For each provider: persist fresh metadata to disk so future sessions can fall back to it
            // when the API is unavailable. If the API call already failed (null), try loading the cached list.
            var metadataCacheAge = TimeSpan.FromHours(24);
            if (bttv)
                emoteDataResponse.BTTV = await RefreshOrLoadProviderMetadata(bttvFolder, emoteDataResponse.BTTV, "BetterTTV", metadataCacheAge, logger, cancellationToken);
            if (ffz)
                emoteDataResponse.FFZ = await RefreshOrLoadProviderMetadata(ffzFolder, emoteDataResponse.FFZ, "FFZ", metadataCacheAge, logger, cancellationToken);
            if (stv)
                emoteDataResponse.STV = await RefreshOrLoadProviderMetadata(stvFolder, emoteDataResponse.STV, "7TV", metadataCacheAge, logger, cancellationToken);

            if (bttv)
            {
                try
                {
                    await FetchEmoteImages(comments, emoteDataResponse.BTTV, emotes, bttvFolder, "BetterTTV", logger, cancellationToken);
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
                    await FetchEmoteImages(comments, emoteDataResponse.FFZ, emotes, ffzFolder, "FFZ", logger, cancellationToken);
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
                    await FetchEmoteImages(comments, emoteDataResponse.STV, emotes, stvFolder, "7TV", logger, cancellationToken);

                    // When metadata failed (STV is null), FetchEmoteImages returned early without touching
                    // the cache. Report whether previously cached images exist so the user knows what to expect.
                    if (emoteDataResponse.STV is null)
                    {
                        // Both the API and the metadata cache failed. Report image cache status for context.
                        stvFolder.Refresh();
                        var cachedCount = stvFolder.Exists ? stvFolder.GetFiles("*_2.*").Length : 0;
                        if (cachedCount > 0)
                            logger.LogWarning($"7TV API and metadata cache were both unavailable. {cachedCount} cached 7TV emote image(s) exist but cannot be matched — new 7TV emotes will not appear, embedded ones may still be present.");
                        else
                            logger.LogInfo("7TV API and metadata cache were both unavailable and no cached 7TV emotes were found — only embedded 7TV emotes will appear.");
                    }
                }
                catch (Exception ex)
                {
                    LogProviderException(ex, "7TV", logger);
                }
            }

            return emotes.Values.ToList();

            static async Task FetchEmoteImages([AllowNull] IEnumerable<Comment> comments, [AllowNull] IEnumerable<EmoteResponseItem> emoteResponse, Dictionary<string, TwitchEmote> emotes,
                DirectoryInfo cacheFolder, string providerName, ITaskLogger logger, CancellationToken cancellationToken)
            {
                if (emoteResponse is null)
                    return;

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
                        where !emotes.ContainsKey(emote.Code)
                        let regex = new Regex($@"(?<=^|\s){Regex.Escape(emote.Code)}(?=$|\s)")
                        where comments.Any(comment => regex.IsMatch(comment.message.body))
                        select emote;
                }

                int fromCache = 0, downloaded = 0;
                foreach (var emote in emoteResponseQuery)
                {
                    var emoteUrl = emote.ImageUrl.Replace("[scale]", "2");
                    var expectedCachePath = Path.Combine(cacheFolder.FullName, $"{emote.Id}_2.{emote.ImageType}");
                    bool wasInCache = File.Exists(expectedCachePath);

                    try
                    {
                        var (bytes, codec) = await GetImage(cacheFolder, emoteUrl, emote.Id, 2, emote.ImageType, false, logger, cancellationToken);
                        if (bytes is null)
                        {
                            continue;
                        }

                        var newEmote = new TwitchEmote(bytes, codec, EmoteProvider.ThirdParty, 2, emote.Id, emote.Code, emote.IsZeroWidth);

                        if (!emotes.TryAdd(emote.Code, newEmote))
                        {
                            // Should never occur, but just in case
                            newEmote.Dispose();
                        }
                        else if (wasInCache)
                        {
                            fromCache++;
                        }
                        else
                        {
                            downloaded++;
                        }
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        logger.LogWarning($"Got {(int)ex.StatusCode}: {ex.StatusCode} when fetching {emote.Code} ({emoteUrl}).");
                    }
                }

                var total = fromCache + downloaded;
                if (total > 0)
                    logger.LogInfo($"Loaded {total} {providerName} emote(s) — {fromCache} from cache, {downloaded} newly downloaded.");
                else
                    logger.LogVerbose($"No {providerName} emotes found in this chat.");
            }

            static async Task<List<EmoteResponseItem>> RefreshOrLoadProviderMetadata(
                DirectoryInfo folder, List<EmoteResponseItem> freshData, string providerName,
                TimeSpan maxAge, ITaskLogger logger, CancellationToken ct)
            {
                var cacheFile = new FileInfo(Path.Combine(folder.FullName, "metadata.json"));

                if (freshData is not null)
                {
                    // Persist fresh metadata so future sessions can fall back to it.
                    try
                    {
                        if (!folder.Exists)
                            CreateDirectory(folder.FullName);
                        await File.WriteAllBytesAsync(cacheFile.FullName, JsonSerializer.SerializeToUtf8Bytes(freshData), ct);
                    }
                    catch { /* best-effort — never block rendering on a metadata write failure */ }
                    return freshData;
                }

                // API failed — try falling back to a previously cached emote list.
                try
                {
                    if (!cacheFile.Exists)
                        return null;

                    if (DateTime.UtcNow - cacheFile.LastWriteTimeUtc > maxAge)
                        return null;

                    var bytes = await File.ReadAllBytesAsync(cacheFile.FullName, ct);
                    var cached = JsonSerializer.Deserialize<List<EmoteResponseItem>>(bytes);
                    if (cached is { Count: > 0 })
                    {
                        var ageHours = (DateTime.UtcNow - cacheFile.LastWriteTimeUtc).TotalHours;
                        logger.LogInfo($"{providerName} API unavailable. Using cached emote list from {ageHours:F1}h ago ({cached.Count} emotes).");
                        return cached;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogVerbose($"Failed to load cached {providerName} emote metadata: {ex.Message}");
                }

                return null;
            }

            static void LogProviderException(Exception ex, string providerName, ITaskLogger logger)
            {
                var message = ex switch
                {
                    HttpRequestException { StatusCode: not null } hre => $"{providerName} returned {(int)hre.StatusCode}: {hre.StatusCode}.",
                    HttpRequestException { InnerException: not null } when ex.Message.Contains("SSL") => $"{ex.Message.Remove(ex.Message.Length - 1)}: {ex.InnerException.Message}",
                    TaskCanceledException when ex.Message.Contains("HttpClient.Timeout") => $"{providerName} timed out.",
                    _ => ex.Message
                };

                logger.LogError($"{message} Some {providerName} emotes may not be present for this session.");
            }
        }

        public static async Task<List<TwitchEmote>> GetEmotes(List<Comment> comments, string cacheFolder, ITaskLogger logger, EmbeddedData embeddedData = null, bool offline = false, CancellationToken cancellationToken = default)
        {
            var emotes = new Dictionary<string, TwitchEmote>();

            DirectoryInfo emoteFolder = new DirectoryInfo(Path.Combine(cacheFolder, "emotes"));
            if (!emoteFolder.Exists)
                emoteFolder = CreateDirectory(emoteFolder.FullName);

            // Load our embedded emotes
            if (embeddedData?.firstParty != null)
            {
                emotes.EnsureCapacity(embeddedData.firstParty.Count);
                foreach (EmbedEmoteData emoteData in embeddedData.firstParty)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var newEmote = new TwitchEmote(emoteData.data, null, EmoteProvider.FirstParty, emoteData.imageScale, emoteData.id, emoteData.name);

                        if (!emotes.TryAdd(emoteData.id, newEmote))
                        {
                            newEmote.Dispose();
                            logger.LogVerbose($"Tried to add duplicate emote from embedded data: {emoteData.name}.");
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogVerbose($"An exception occurred while loading embedded emote '{emoteData.name}': {e.Message}.");
                    }
                }
            }

            var toFetch = new HashSet<string>();
            foreach (var comment in comments.Where(c => c.message.fragments != null))
            {
                foreach (var fragment in comment.message.fragments)
                {
                    var id = fragment.emoticon?.emoticon_id;
                    if (id is not null)
                    {
                        toFetch.Add(id);
                    }
                }
            }

            var failedEmotes = new HashSet<string>();
            foreach (var id in toFetch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (failedEmotes.Contains(id))
                {
                    continue;
                }

                try
                {
                    var (bytes, codec) = await GetImage(emoteFolder, $"https://static-cdn.jtvnw.net/emoticons/v2/{id}/default/dark/2.0", id, 2, "png", offline, logger, cancellationToken);
                    if (bytes is null)
                    {
                        continue;
                    }

                    var newEmote = new TwitchEmote(bytes, codec, EmoteProvider.FirstParty, 2, id, id);

                    if (!emotes.TryAdd(id, newEmote))
                    {
                        // This should never occur, but just in case
                        newEmote.Dispose();
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    failedEmotes.Add(id);
                }
            }

            return emotes.Values.ToList();
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
            var badges = new Dictionary<string, ChatBadge>();

            // Load our embedded data from file
            if (embeddedData?.twitchBadges != null)
            {
                badges.EnsureCapacity(embeddedData.twitchBadges.Count);
                foreach (EmbedChatBadge data in embeddedData.twitchBadges)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        ChatBadge newBadge = new ChatBadge(data.name, data.versions);

                        if (!badges.TryAdd(data.name, newBadge))
                        {
                            newBadge.Dispose();
                            logger.LogVerbose($"Tried to add duplicate badge from embedded data: {data.name}.");
                        }
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
                return badges.Values.ToList();
            }

            List<EmbedChatBadge> badgesData = await GetChatBadgesData(comments, streamerId, cancellationToken);

            DirectoryInfo badgeFolder = new DirectoryInfo(Path.Combine(cacheFolder, "badges"));
            if (!badgeFolder.Exists)
                badgeFolder = CreateDirectory(badgeFolder.FullName);

            foreach (var badge in badgesData.Where(badge => !badges.ContainsKey(badge.name)))
            {
                try
                {
                    Dictionary<string, ChatBadgeData> versions = new();
                    foreach (var (version, data) in badge.versions)
                    {
                        string id = data.url.GetNthOccurrence('/', ^2).ToString();
                        var (bytes, codec) = await GetImage(badgeFolder, data.url, id, 2, "png", false, logger, cancellationToken);
                        if (bytes is null)
                        {
                            continue;
                        }

                        versions.Add(version, new ChatBadgeData
                        {
                            title = data.title,
                            description = data.description,
                            bytes = bytes,
                            Codec = codec,
                        });
                    }

                    var newBadge = new ChatBadge(badge.name, versions);
                    if (!badges.TryAdd(badge.name, newBadge))
                    {
                        newBadge.Dispose();
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
            }

            return badges.Values.ToList();
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

                    await using var archive = await ZipFile.OpenReadAsync(emojiZipPath, cancellationToken);
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
                                await emoji.ExtractToFileAsync(filePath, cancellationToken);
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
            var bits = new Dictionary<string, CheerEmote>();

            // Load our embedded data from file
            if (embeddedData?.twitchBits != null)
            {
                bits.EnsureCapacity(embeddedData.twitchBits.Count);
                foreach (EmbedCheerEmote data in embeddedData.twitchBits)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        List<KeyValuePair<int, TwitchEmote>> tierList = new List<KeyValuePair<int, TwitchEmote>>();
                        CheerEmote newEmote = new CheerEmote() { prefix = data.prefix, tierList = tierList };
                        foreach (var (cost, cheermote) in data.tierList)
                        {
                            var tierEmote = new TwitchEmote(cheermote.data, null, EmoteProvider.FirstParty, cheermote.imageScale, cheermote.id, cheermote.name);
                            tierList.Add(new KeyValuePair<int, TwitchEmote>(cost, tierEmote));
                        }

                        if (!bits.TryAdd(data.prefix, newEmote))
                        {
                            newEmote.Dispose();
                            logger.LogVerbose($"Tried to add duplicate cheermote from embedded data: {data.prefix}.");
                        }
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
                return bits.Values.ToList();
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
                        where !bits.ContainsKey(node.prefix)
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
                                var url = new StringBuilder(templateURL)
                                    .Replace("PREFIX", node.prefix.ToLower())
                                    .Replace("BACKGROUND", "dark")
                                    .Replace("ANIMATION", "animated")
                                    .Replace("TIER", tier.bits.ToString())
                                    .Replace("SCALE.EXTENSION", "2.gif")
                                    .ToString();

                                var (bytes, codec) = await GetImage(bitFolder, url, node.id + tier.bits, 2, "gif", false, logger, cancellationToken);
                                if (bytes is null)
                                {
                                    continue;
                                }

                                var emote = new TwitchEmote(bytes, codec, EmoteProvider.FirstParty, 2, prefix + minBits, prefix + minBits);
                                tierList.Add(new KeyValuePair<int, TwitchEmote>(minBits, emote));
                            }

                            if (!bits.TryAdd(node.prefix, newEmote))
                            {
                                newEmote.Dispose();
                            }
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
                    }
                }
            }

            return bits.Values.ToList();
        }

        public static async Task<Dictionary<string, SKBitmap>> GetAvatars(List<Comment> comments, string[] defaultAvatars, string cacheFolder, ITaskLogger logger, bool offline = false, CancellationToken cancellationToken = default)
        {
            var urls = new HashSet<string>(defaultAvatars ?? []);
            foreach (var comment in comments)
            {
                var logo = comment.commenter.logo;
                if (string.IsNullOrWhiteSpace(logo))
                    continue;

                urls.Add(logo);
            }

            var avatarFolder = new DirectoryInfo(Path.Combine(cacheFolder, "avatars"));
            if (!avatarFolder.Exists)
                avatarFolder = CreateDirectory(avatarFolder.FullName);

            var avatars = new Dictionary<string, SKBitmap>();
            foreach (var url in urls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var avatarId = Path.GetFileNameWithoutExtension(url);

                byte[] bytes;
                SKCodec codec;
                try
                {
                    (bytes, codec) = await GetImage(avatarFolder, url, avatarId, 2, "jpg", offline, logger, cancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogVerbose($"Error while fetching {url}: {ex.Message}");
                    continue;
                }

                if (bytes is null)
                {
                    continue;
                }

                try
                {
                    var bitmap = codec is null
                        ? SKBitmap.Decode(bytes)
                        : SKBitmap.Decode(codec);

                    if (bitmap is null)
                    {
                        logger.LogWarning($"Skia was unable to decode {avatarId}.");
                        continue;
                    }

                    avatars.Add(url, bitmap);
                }
                finally
                {
                    codec?.Dispose();
                }
            }

            return avatars;
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

        /// <inheritdoc cref="Directory.CreateDirectory(string)"/>
        public static DirectoryInfo CreateDirectory(string path, ITaskLogger logger = null)
        {
            DirectoryInfo directoryInfo = Directory.CreateDirectory(path);

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Set777UnixFilePermissions(directoryInfo);
                }
            }
            catch (Exception e)
            {
                logger?.LogVerbose($"Failed to set unix file mode for {directoryInfo.FullName}: {e.Message}");
            }

            return directoryInfo;
        }

        [UnsupportedOSPlatform("windows")]
        public static FileSystemInfo Set777UnixFilePermissions(FileSystemInfo fileSystemInfo)
        {
            fileSystemInfo.UnixFileMode = UnixFileMode.OtherExecute | UnixFileMode.OtherWrite | UnixFileMode.OtherRead
                                          | UnixFileMode.GroupExecute | UnixFileMode.GroupWrite | UnixFileMode.GroupRead
                                          | UnixFileMode.UserExecute | UnixFileMode.UserWrite | UnixFileMode.UserRead;

            fileSystemInfo.Refresh();
            return fileSystemInfo;
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
                Content = new StringContent("{\"query\":\"query{users(ids:[" + string.Join(",", idList.Select(x => "\\\"" + x + "\\\"").ToArray()) + "]){id,displayName,login,createdAt,updatedAt,description,profileImageURL(width:70)}}\",\"variables\":{}}", Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GqlUserInfoResponse>();
        }

        public static async Task<(byte[], SKCodec)> GetImage(DirectoryInfo cacheDir, string url, string imageId, int imageScale, string imageType, bool offline, ITaskLogger logger, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            cacheDir.Refresh();
            if (!cacheDir.Exists)
            {
                CreateDirectory(cacheDir.FullName);
                cacheDir.Refresh();
            }

            var filePath = Path.Combine(cacheDir.FullName, $"{imageId}_{imageScale}.{imageType}");
            var file = new FileInfo(filePath);

            if (file.Exists)
            {
                try
                {
                    var fileBytes = await File.ReadAllBytesAsync(file.FullName, cancellationToken).ConfigureAwait(false);

                    if (fileBytes.Length > 0)
                    {
                        var ms = new MemoryStream(fileBytes);
                        var codec = SKCodec.Create(ms, out var result);

                        if (codec is not null)
                        {
                            return (fileBytes, codec);
                        }

                        logger.LogVerbose($"Failed to decode {file.Name} from cache: {result}");
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

            // Return null if offline
            if (offline)
            {
                return (null, null);
            }

            var imageBytes = await httpClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);

            try
            {
                await File.WriteAllBytesAsync(file.FullName, imageBytes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogVerbose($"Failed to save {file.Name} to cache: {ex.Message}");
            }

            return (imageBytes, null);
        }

        /// <remarks>When a given video has only 1 chapter, data.video.moments.edges will be empty.</remarks>
        public static async Task<GqlVideoChapterResponse> GetVideoChapters(long videoId)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri("https://gql.twitch.tv/gql"),
                Method = HttpMethod.Post,
                Content = new StringContent("{\"extensions\":{\"persistedQuery\":{\"sha256Hash\":\"71835d5ef425e154bf282453a926d99b328cdc5e32f36d3a209d0f4778b41203\",\"version\":1}},\"operationName\":\"VideoPlayer_ChapterSelectButtonVideo\",\"variables\":{\"videoID\":\"" + videoId + "\"}}", Encoding.UTF8, "application/json")
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

        public static int SnapResizeHeight(int desiredHeight, int upSnapThreshold, int downSnapThreshold, int imageHeight)
        {
            if (upSnapThreshold == downSnapThreshold && upSnapThreshold != 0)
            {
                var o = (desiredHeight + upSnapThreshold) % imageHeight;
                if (o <= upSnapThreshold * 2)
                {
                    desiredHeight += upSnapThreshold - o;
                }
            }
            else
            {
                if (downSnapThreshold != 0)
                {
                    var o = desiredHeight % imageHeight;
                    if (o <= downSnapThreshold)
                    {
                        desiredHeight -= o;
                    }
                }

                if (upSnapThreshold != 0)
                {
                    var o = imageHeight - (desiredHeight % imageHeight);
                    if (o <= upSnapThreshold)
                    {
                        desiredHeight += o;
                    }
                }
            }

            return desiredHeight;
        }
    }
}