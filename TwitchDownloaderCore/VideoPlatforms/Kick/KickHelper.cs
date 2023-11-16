using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Twitch;

namespace TwitchDownloaderCore.VideoPlatforms.Kick
{
    public class KickHelper
    {
        private static readonly HttpClient HttpClient = new();
        public static async Task<KickClipResponse> GetClipInfo(string clipId)
        {
            string response = await Task.Run(() => CurlImpersonate.GetCurlResponse($"https://kick.com/api/v2/clips/{clipId}"));
            KickClipResponse clipResponse = JsonSerializer.Deserialize<KickClipResponse>(response);

            if (clipResponse.clip == null)
            {
                throw new Exception(clipResponse?.message ?? "Unable to get clip info");
            }

            return clipResponse;
        }

        public static async Task<string> GetPlaylistData(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public static List<KickClipSegment> GetDownloadUrls(string url, string playlistData)
        {
            string baseUrl = url.Substring(0, url.LastIndexOf("/")) + "/";
            List<string> playlistLines = new List<string>(playlistData.Split('\n'));

            List<KickClipSegment> returnList = new List<KickClipSegment>();
            KickClipSegment latestSegment = null;

            for (int i = 0; i < playlistLines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(playlistLines[i]))
                    continue;

                if (!playlistLines[i].StartsWith('#'))
                {
                    if (latestSegment?.DownloadUrl == baseUrl + playlistLines[i])
                    {
                        for (int k = i - 1; k >= 0; k--)
                        {
                            if (playlistLines[k].StartsWith("#EXT-X-BYTERANGE:"))
                            {
                                string byteRange = playlistLines[k].Substring("#EXT-X-BYTERANGE:".Length);
                                latestSegment.ByteRangeLength += int.Parse(byteRange.Split('@')[0]);
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (latestSegment != null) returnList.Add(latestSegment);
                        latestSegment = new KickClipSegment();
                        latestSegment.DownloadUrl = baseUrl + playlistLines[i];

                        for (int k = i - 1; k >= 0; k--)
                        {
                            if (playlistLines[k].StartsWith("#EXT-X-BYTERANGE:"))
                            {
                                string byteRange = playlistLines[k].Substring("#EXT-X-BYTERANGE:".Length);
                                latestSegment.StartByteOffset = int.Parse(byteRange.Split('@')[1]);
                                latestSegment.ByteRangeLength = int.Parse(byteRange.Split('@')[0]);
                                break;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(latestSegment?.DownloadUrl))
            {
                returnList.Add(latestSegment);
            }

            return returnList;
        }

        public static async Task<KickVideoResponse> GetVideoInfo(string videoId)
        {
            string response = await Task.Run(() => CurlImpersonate.GetCurlResponse($"https://kick.com/api/v1/video/{videoId}"));
            KickVideoResponse videoResponse = JsonSerializer.Deserialize<KickVideoResponse>(response);

            if (videoResponse.id == 0)
            {
                throw new Exception("Unable to get video info");
            }

            string baseUrl = videoResponse.source.Substring(0, videoResponse.source.LastIndexOf('/') + 1);

            string[] playlist = (await HttpClient.GetStringAsync(videoResponse.source)).Split('\n');
            videoResponse.VideoQualities = new List<VideoQuality>();

            for (int i = 0; i < playlist.Length; i++)
            {
                if (playlist[i].Contains("#EXT-X-MEDIA"))
                {
                    string lastPart = playlist[i].Substring(playlist[i].IndexOf("NAME=\"", StringComparison.Ordinal) + 6);
                    string stringQuality = lastPart.Substring(0, lastPart.IndexOf('"'));

                    var bandwidthStartIndex = playlist[i + 1].IndexOf("BANDWIDTH=", StringComparison.Ordinal) + 10;
                    var bandwidthEndIndex = playlist[i + 1].AsSpan(bandwidthStartIndex).IndexOf(',');
                    int.TryParse(playlist[i + 1].AsSpan(bandwidthStartIndex, bandwidthEndIndex), out var bandwidth);

                    videoResponse.VideoQualities.Add(new VideoQuality { Quality = stringQuality, SourceUrl = baseUrl + playlist[i + 2], Bandwidth = bandwidth });
                }
            }

            return videoResponse;
        }

        public static async Task<List<TwitchEmote>> GetEmotes(List<Comment> comments, string cacheFolder, EmbeddedData embeddedData, bool offline, CancellationToken cancellationToken)
        {
            List<TwitchEmote> returnList = new List<TwitchEmote>();
            List<string> alreadyAdded = new List<string>();
            List<string> failedEmotes = new List<string>();

            string emoteFolder = Path.Combine(cacheFolder, "kick_emotes");
            if (!Directory.Exists(emoteFolder))
                PlatformHelper.CreateDirectory(emoteFolder);

            // Load our embedded emotes
            if (embeddedData?.firstParty != null)
            {
                foreach (EmbedEmoteData emoteData in embeddedData.firstParty)
                {
                    try
                    {
                        TwitchEmote newEmote = new TwitchEmote(emoteData.data, EmoteProvider.TwitchFirstParty, emoteData.imageScale, emoteData.id, emoteData.name);
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

            foreach (var comment in comments.Where(c => c.message.fragments != null))
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var id in comment.message.fragments
                             .Select(f => f.emoticon?.emoticon_id)
                             .Where(id => id != null && !alreadyAdded.Contains(id) && !failedEmotes.Contains(id)))
                {
                    try
                    {
                        byte[] bytes = await PlatformHelper.GetImage(emoteFolder, $"https://files.kick.com/emotes/{id}/fullsize", id, "2.5", "png", cancellationToken);
                        TwitchEmote newEmote = new TwitchEmote(bytes, EmoteProvider.TwitchFirstParty, 2.5, id, id);
                        alreadyAdded.Add(id);
                        returnList.Add(newEmote);
                    }
                    catch (Exception)
                    {
                        failedEmotes.Add(id);
                    }
                }
            }

            return returnList;
        }

        public static async Task<List<TwitchEmote>> GetThirdPartyEmotes(List<Comment> comments, int streamerId, string cacheFolder, EmbeddedData embeddedData = null, bool bttv = true, bool ffz = true, bool stv = true, bool allowUnlistedEmotes = true, bool offline = false, CancellationToken cancellationToken = new())
        {
            List<TwitchEmote> returnList = new List<TwitchEmote>();
            List<string> alreadyAdded = new List<string>();

            // No 3rd party emotes are wanted
            if (!stv)
            {
                return returnList;
            }

            // Load our embedded data from file
            if (embeddedData?.thirdParty != null)
            {
                foreach (EmbedEmoteData emoteData in embeddedData.thirdParty)
                {
                    try
                    {
                        TwitchEmote newEmote = new TwitchEmote(emoteData.data, EmoteProvider.TwitchThirdParty, emoteData.imageScale, emoteData.id, emoteData.name);
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

            string stvFolder = Path.Combine(cacheFolder, "stv");

            EmoteResponse emoteDataResponse = await GetThirdPartyEmoteData(streamerId, stv, allowUnlistedEmotes, cancellationToken);

            if (stv)
            {
                if (!Directory.Exists(stvFolder))
                    PlatformHelper.CreateDirectory(stvFolder);

                var emoteResponseItemsQuery = from emote in emoteDataResponse.STV
                                              where !alreadyAdded.Contains(emote.Code)
                                              let pattern = $@"(?<=^|\s){Regex.Escape(emote.Code)}(?=$|\s)"
                                              where comments.Any(comment => Regex.IsMatch(comment.message.body, pattern))
                                              select emote;

                foreach (var emote in emoteResponseItemsQuery)
                {
                    try
                    {
                        TwitchEmote newEmote = new TwitchEmote(await PlatformHelper.GetImage(stvFolder, emote.ImageUrl.Replace("[scale]", "2"), emote.Id, "2", emote.ImageType, cancellationToken), EmoteProvider.TwitchThirdParty, 2, emote.Id, emote.Code);
                        if (emote.IsZeroWidth)
                            newEmote.IsZeroWidth = true;
                        returnList.Add(newEmote);
                        alreadyAdded.Add(emote.Code);
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return returnList;
        }

        public static async Task<EmoteResponse> GetThirdPartyEmoteData(int streamerId, bool getStv, bool allowUnlistedEmotes, CancellationToken cancellationToken = new())
        {
            cancellationToken.ThrowIfCancellationRequested();

            EmoteResponse emoteResponse = new();

            if (getStv)
            {
                emoteResponse.STV = await PlatformHelper.GetStvEmotesMetadata(streamerId, allowUnlistedEmotes, VideoPlatform.Kick, cancellationToken);
            }

            return emoteResponse;
        }
    }
}