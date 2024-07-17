using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCLI.Modes
{
    internal static class StreamInfo
    {
        public static void PrintInfo(StreamInfoArgs inputOptions)
        {
            var vodClipIdMatch = TwitchRegex.MatchVideoOrClipId(inputOptions.Id);
            if (vodClipIdMatch is not { Success: true })
            {
                Console.WriteLine("[ERROR] - Unable to parse VOD/Clip ID/URL.");
                Environment.Exit(1);
            }

            var videoId = vodClipIdMatch.Value;
            if (videoId.All(char.IsDigit))
            {
                HandleVod(inputOptions);
            }
            else
            {
                HandleClip(inputOptions);
            }
        }

        private static void HandleVod(StreamInfoArgs inputOptions)
        {
            var videoId = int.Parse(inputOptions.Id);
            var (videoInfo, playlistString) = GetPlaylistInfo(videoId, inputOptions.Oauth, inputOptions.Format != StreamInfoPrintFormat.Raw).GetAwaiter().GetResult();

            switch (inputOptions.Format)
            {
                case StreamInfoPrintFormat.Raw:
                {
                    var stdOut = Console.OpenStandardOutput();
                    JsonSerializer.Serialize(stdOut, videoInfo);
                    Console.WriteLine();
                    Console.Write(playlistString);
                    break;
                }
                case StreamInfoPrintFormat.Table:
                {
                    var m3u8 = M3U8.Parse(playlistString);
                    m3u8.SortStreamsByQuality();

                    const string DEFAULT_STRING = "-";
                    var videoLength = TimeSpan.FromSeconds(videoInfo.data.video.lengthSeconds);

                    var streams = m3u8.Streams;
                    var table = new Table(streams.Length, DEFAULT_STRING)
                        .AddColumn("Name", Table.TextAlign.Left, streams.Select(x => x.GetResolutionFramerateString()))
                        .AddSeparator()
                        .AddColumn("Resolution", Table.TextAlign.Left, streams.Select(x => StringifyOrDefault(x.StreamInfo.Resolution, r => r.ToString(), DEFAULT_STRING)))
                        .AddColumn("FPS", Table.TextAlign.Right, streams.Select(x => StringifyOrDefault(x.StreamInfo.Framerate, f => f.ToString(CultureInfo.CurrentCulture), DEFAULT_STRING)))
                        .AddColumn("Codecs", Table.TextAlign.Right, streams.Select(x => StringifyOrDefault(x.StreamInfo.Codecs, c => c, DEFAULT_STRING)));

                    if (streams.Any(x => x.StreamInfo.Bandwidth != default))
                    {
                        table.AddSeparator()
                            .AddColumn("File size", Table.TextAlign.Right, streams.Select(x => StringifyOrDefault(x.StreamInfo.Bandwidth,
                                b => $"~{VideoSizeEstimator.StringifyByteCount(VideoSizeEstimator.EstimateVideoSize(b, TimeSpan.Zero, videoLength))}", DEFAULT_STRING)))
                            .AddColumn("Bitrate", Table.TextAlign.Right, streams.Select(x => StringifyOrDefault(x.StreamInfo.Bandwidth, b => $"{b / 1000}kbps", DEFAULT_STRING)));
                    }

                    var bestQuality = m3u8.BestQualityStream();
                    table.AddSeparator()
                        .AddColumn("Source", Table.TextAlign.Left, streams.Select(x => ReferenceEquals(x, bestQuality).ToString()));

                    foreach (var row in table.GetRows())
                    {
                        Console.WriteLine(row);
                    }

                    break;
                }
                case StreamInfoPrintFormat.M3U8:
                {
                    // Parse as m3u8 to verify that it is a valid playlist
                    var m3u8 = M3U8.Parse(playlistString);
                    Console.Write(m3u8.ToString());
                    break;
                }
                case StreamInfoPrintFormat.Json:
                {
                    var m3u8 = M3U8.Parse(playlistString);
                    throw new NotImplementedException("JSON format is not yet supported");
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static async Task<(GqlVideoResponse videoInfo, string playlistString)> GetPlaylistInfo(int videoId, string oauth, bool canThrow)
        {
            Console.WriteLine("[INFO] Fetching Video Info [1/1]");

            var videoInfo = await TwitchHelper.GetVideoInfo(videoId);
            var accessToken = await TwitchHelper.GetVideoToken(videoId, oauth);

            if (accessToken.data.videoPlaybackAccessToken is null)
            {
                if (canThrow)
                {
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
                }

                return (videoInfo, null);
            }

            var playlistString = await TwitchHelper.GetVideoPlaylist(videoId, accessToken.data.videoPlaybackAccessToken.value, accessToken.data.videoPlaybackAccessToken.signature);
            if (canThrow && (playlistString.Contains("vod_manifest_restricted") || playlistString.Contains("unauthorized_entitlements")))
            {
                throw new NullReferenceException("Insufficient access to VOD, OAuth may be required.");
            }

            return (videoInfo, playlistString);
        }

        private static void HandleClip(StreamInfoArgs inputOptions)
        {
            var (clipInfo, clipQualities) = GetClipInfo(inputOptions.Id, inputOptions.Format != StreamInfoPrintFormat.Raw).GetAwaiter().GetResult();

            switch (inputOptions.Format)
            {
                case StreamInfoPrintFormat.Raw:
                {
                    var stdOut = Console.OpenStandardOutput();
                    JsonSerializer.Serialize(stdOut, clipInfo);
                    Console.WriteLine();
                    JsonSerializer.Serialize(stdOut, clipQualities);
                    break;
                }
                case StreamInfoPrintFormat.Table:
                {
                    const string DEFAULT_STRING = "-";
                    var clip = clipQualities.data.clip;
                    var qualities = clip.videoQualities;

                    var qualityTable = new Table(qualities.Length, DEFAULT_STRING)
                        .AddColumn("Name", Table.TextAlign.Left, qualities.Select(x => $"{x.quality}p{(Math.Round(x.frameRate) == 30 ? "" : Math.Round(x.frameRate).ToString(CultureInfo.CurrentCulture))}"))
                        .AddSeparator()
                        .AddColumn("Height", Table.TextAlign.Left, qualities.Select(x => $"{x.quality}"))
                        .AddColumn("FPS", Table.TextAlign.Right, qualities.Select(x => StringifyOrDefault(x.frameRate, f => Math.Round(f, 2).ToString(CultureInfo.CurrentCulture), DEFAULT_STRING)));

                    var wroteFileSizeColumn = false;
                    if (clip.videoQualities.FirstOrDefault(x => clip.playbackAccessToken.value.Contains(x.sourceURL)) is { } sourceQuality)
                    {
                        // Get the file size of the highest quality, since it is most likely to be downloaded.
                        // Don't bother with the other qualities to avoid making too many requests.
                        var sourceUrl = $"{sourceQuality.sourceURL}?sig={clip.playbackAccessToken.signature}&token={HttpUtility.UrlEncode(clip.playbackAccessToken.value)}";
                        using var httpClient = new HttpClient();
                        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUrl);
                        using var response = httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
                        if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength.HasValue)
                        {
                            wroteFileSizeColumn = true;
                            var sourceFileSize = VideoSizeEstimator.StringifyByteCount(response.Content.Headers.ContentLength.Value);
                            qualityTable.AddSeparator()
                                .AddColumn("File size", Table.TextAlign.Right,
                                    qualities.Select(x => ReferenceEquals(sourceQuality, x) && !string.IsNullOrEmpty(sourceFileSize) ? sourceFileSize : DEFAULT_STRING));
                        }

                        qualityTable.AddSeparator()
                            .AddColumn("Source", Table.TextAlign.Left, qualities.Select(x => ReferenceEquals(sourceQuality, x).ToString()));
                    }

                    foreach (var row in qualityTable.GetRows())
                    {
                        Console.WriteLine(row);
                    }

                    if (wroteFileSizeColumn)
                    {
                        Console.WriteLine("NOTE: Only the source quality file size was checked. This does not mean it is the only available quality.");
                    }

                    break;
                }
                case StreamInfoPrintFormat.M3U8:
                {
                    var clip = clipQualities.data.clip;

                    var metadata = new M3U8.Metadata
                    {
                        Version = default,
                        MediaSequence = 0,
                        StreamTargetDuration = (uint)clipInfo.data.clip.durationSeconds,
                        TwitchElapsedSeconds = 0,
                        TwitchLiveSequence = default,
                        TwitchTotalSeconds = (uint)clipInfo.data.clip.durationSeconds,
                        Type = M3U8.Metadata.PlaylistType.Event,
                    };

                    var streams = clip.videoQualities.Select(x => new M3U8.Stream(
                        new M3U8.Stream.ExtMediaInfo(M3U8.Stream.ExtMediaInfo.MediaType.Video, x.quality, x.quality, true, true),
                        new M3U8.Stream.ExtStreamInfo(default, default, default, default, x.quality, x.frameRate),
                        $"{x.sourceURL}?sig={clip.playbackAccessToken.signature}&token={HttpUtility.UrlEncode(clip.playbackAccessToken.value)}"
                    )).ToArray();

                    var m3u8 = new M3U8(metadata, streams);
                    Console.Write(m3u8.ToString());
                    break;
                }
                case StreamInfoPrintFormat.Json:
                {
                    throw new NotImplementedException("JSON format is not yet supported");
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static async Task<(GqlClipResponse clipInfo, GqlClipTokenResponse listLinks)> GetClipInfo(string clipId, bool canThrow)
        {
            Console.WriteLine("[INFO] Fetching Clip Info [1/1]");

            var clipInfo = await TwitchHelper.GetClipInfo(clipId);
            var listLinks = await TwitchHelper.GetClipLinks(clipId);

            if (!canThrow)
            {
                return (clipInfo, listLinks);
            }

            var clip = listLinks.data.clip;
            if (clip.playbackAccessToken is null)
            {
                throw new NullReferenceException("Invalid Clip, deleted possibly?");
            }

            if (clip.videoQualities is null || clip.videoQualities.Length == 0)
            {
                throw new NullReferenceException("Clip has no video qualities, deleted possibly?");
            }

            return (clipInfo, listLinks);
        }

        private static string StringifyOrDefault<T>(T value, Func<T, string> stringify, string defaultString) where T : IEquatable<T>
        {
            if (!value.Equals(default))
            {
                return stringify(value);
            }

            return defaultString;
        }
    }
}