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

        public static async Task<string> GetString(string url)
        {
            return await HttpClient.GetStringAsync(url);
        }

        public static async Task<KickVideoResponse> GetVideoInfo(string videoId)
        {
            string response = await Task.Run(() => CurlImpersonate.GetCurlResponse($"https://kick.com/api/v1/video/{videoId}"));
            KickVideoResponse videoResponse = JsonSerializer.Deserialize<KickVideoResponse>(response);

            if (videoResponse.id == 0)
            {
                throw new Exception("Unable to get video info");
            }

            return videoResponse;
        }

        public static async Task<M3U8> GetQualitiesPlaylist(KickVideoResponse videoResponse)
        {
            var playlist = await Task.Run(() => CurlImpersonate.GetCurlResponse(videoResponse.source));
            return M3U8.Parse(playlist);
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