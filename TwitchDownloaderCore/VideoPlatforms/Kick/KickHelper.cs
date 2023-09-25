using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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
            string response = await Task.Run(() => CurlImpersonate.GetCurlReponse($"https://kick.com/api/v2/clips/{clipId}"));
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

            if (!string.IsNullOrEmpty(latestSegment.DownloadUrl))
            {
                returnList.Add(latestSegment);
            }

            return returnList;
        }

        public static async Task<KickVideoResponse> GetVideoInfo(string videoId)
        {
            string response = await Task.Run(() => CurlImpersonate.GetCurlReponse($"https://kick.com/api/v1/video/{videoId}"));
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
                    string lastPart = playlist[i].Substring(playlist[i].IndexOf("NAME=\"") + 6);
                    string stringQuality = lastPart.Substring(0, lastPart.IndexOf('"'));

                    var bandwidthStartIndex = playlist[i + 1].IndexOf("BANDWIDTH=") + 10;
                    var bandwidthEndIndex = playlist[i + 1].Substring(bandwidthStartIndex).IndexOf(',');
                    int.TryParse(playlist[i + 1].Substring(bandwidthStartIndex, bandwidthEndIndex), out var bandwidth);

                    videoResponse.VideoQualities.Add(new VideoQuality { Quality = stringQuality, SourceUrl = baseUrl + playlist[i + 2], Bandwidth = bandwidth });
                }
            }

            return videoResponse;
        }
    }
}