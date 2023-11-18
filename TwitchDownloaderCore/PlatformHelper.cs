using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Kick;
using TwitchDownloaderCore.VideoPlatforms.Twitch;
using TwitchDownloaderCore.VideoPlatforms.Twitch.Api;

namespace TwitchDownloaderCore
{
    public static class PlatformHelper
    {
        private static readonly HttpClient HttpClient = new();
        public static async Task<IVideoInfo> GetClipInfo(VideoPlatform videoPlatform, string clipId)
        {
            if (videoPlatform == VideoPlatform.Twitch)
            {
                return await TwitchHelper.GetClipInfo(clipId);
            }

            if (videoPlatform == VideoPlatform.Kick)
            {
                return await KickHelper.GetClipInfo(clipId);
            }

            throw new NotImplementedException();
        }

        public static async Task<IVideoInfo> GetVideoInfo(VideoPlatform videoPlatform, string videoId, string oauth = "")
        {
            if (videoPlatform == VideoPlatform.Twitch)
            {
                return await TwitchHelper.GetVideoInfo(int.Parse(videoId), oauth);
            }

            if (videoPlatform == VideoPlatform.Kick)
            {
                return await KickHelper.GetVideoInfo(videoId);
            }

            throw new NotImplementedException();
        }

        public static Task<M3U8> GetQualitiesPlaylist(VideoPlatform videoPlatform, IVideoInfo videoInfo)
        {
            if (videoPlatform == VideoPlatform.Twitch)
            {
                return TwitchHelper.GetVideoQualitiesPlaylist((TwitchVideoInfo)videoInfo);
            }

            if (videoPlatform == VideoPlatform.Kick)
            {
                return KickHelper.GetQualitiesPlaylist((KickVideoResponse)videoInfo);
            }

            throw new NotImplementedException();
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

        public static async Task<byte[]> GetImage(string cachePath, string imageUrl, string imageId, string imageScale, string imageType, CancellationToken cancellationToken = new())
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] imageBytes = null;

            if (!Directory.Exists(cachePath))
                PlatformHelper.CreateDirectory(cachePath);

            string filePath = Path.Combine(cachePath!, $"{imageId}_{imageScale}.{imageType}");
            if (File.Exists(filePath))
            {
                try
                {
                    await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    byte[] bytes = new byte[stream.Length];
                    stream.Seek(0, SeekOrigin.Begin);
                    _ = await stream.ReadAsync(bytes, cancellationToken);

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
            if (imageUrl.Contains("kick.com"))
            {
                imageBytes = CurlImpersonate.GetCurlResponseBytes(imageUrl);
            }
            else
            {
                imageBytes = await HttpClient.GetByteArrayAsync(imageUrl, cancellationToken);
            }

            //Let's save this image to the cache
            try
            {
                await using var fs = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                await fs.WriteAsync(imageBytes, cancellationToken);
            }
            catch { }

            return imageBytes;
        }

        public static async Task<List<EmoteResponseItem>> GetStvEmotesMetadata(int streamerId, bool allowUnlistedEmotes, VideoPlatform videoPlatform, CancellationToken cancellationToken)
        {
            var globalEmoteRequest = new HttpRequestMessage(HttpMethod.Get, new Uri("https://7tv.io/v3/emote-sets/global", UriKind.Absolute));
            using var globalEmoteResponse = await HttpClient.SendAsync(globalEmoteRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            globalEmoteResponse.EnsureSuccessStatusCode();
            var globalEmoteObject = await globalEmoteResponse.Content.ReadFromJsonAsync<STVGlobalEmoteResponse>(cancellationToken: cancellationToken);
            var stvEmotes = globalEmoteObject.emotes;

            // Channel might not be registered on 7tv
            try
            {
                var streamerEmoteRequest = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://7tv.io/v3/users/{Enum.GetName(videoPlatform)!.ToLower()}/{streamerId}", UriKind.Absolute));
                using var streamerEmoteResponse = await HttpClient.SendAsync(streamerEmoteRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
                    continue;
                }
                string emoteUrl = $"https:{emoteHost.url}/[scale]x.{emoteFormat}";
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
                    returnList.Add(emoteResponse);
                }
            }

            return returnList;
        }
    }
}
