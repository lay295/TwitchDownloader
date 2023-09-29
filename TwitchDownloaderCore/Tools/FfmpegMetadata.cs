using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore.Tools
{
    // https://ffmpeg.org/ffmpeg-formats.html#Metadata-1
    public static class FfmpegMetadata
    {
        private const string LINE_FEED = "\u000A";

        public static async Task SerializeAsync(string filePath, string streamerName, string videoId, string videoTitle, DateTime videoCreation, int viewCount, string videoDescription = null,
            double startOffsetSeconds = 0, List<VideoMomentEdge> videoMomentEdges = null, CancellationToken cancellationToken = default)
        {
            await using var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            await using var sw = new StreamWriter(fs) { NewLine = LINE_FEED };

            await SerializeGlobalMetadata(sw, streamerName, videoId, videoTitle, videoCreation, viewCount, videoDescription);
            await fs.FlushAsync(cancellationToken);

            await SerializeChapters(sw, videoMomentEdges, startOffsetSeconds);
            await fs.FlushAsync(cancellationToken);
        }

        private static async Task SerializeGlobalMetadata(StreamWriter sw, string streamerName, string videoId, string videoTitle, DateTime videoCreation, int viewCount, string videoDescription)
        {
            await sw.WriteLineAsync(";FFMETADATA1");
            await sw.WriteLineAsync($"title={SanitizeKeyValue(videoTitle)} ({SanitizeKeyValue(videoId)})");
            await sw.WriteLineAsync($"artist={SanitizeKeyValue(streamerName)}");
            await sw.WriteLineAsync($"date={videoCreation:yyyy}"); // The 'date' key becomes 'year' in most formats
            await sw.WriteAsync(@"comment=");
            if (!string.IsNullOrWhiteSpace(videoDescription))
            {
                await sw.WriteLineAsync(@$"{SanitizeKeyValue(videoDescription.TrimEnd())}\");
                await sw.WriteLineAsync(@"------------------------\");
            }
            await sw.WriteLineAsync(@$"Originally aired: {SanitizeKeyValue(videoCreation.ToString("u"))}\");
            await sw.WriteLineAsync(@$"Video id: {SanitizeKeyValue(videoId)}\");
            await sw.WriteLineAsync(@$"Views: {viewCount}");
        }

        private static async Task SerializeChapters(StreamWriter sw, List<VideoMomentEdge> videoMomentEdges, double startOffsetSeconds)
        {
            if (videoMomentEdges is null)
            {
                return;
            }

            // Note: FFmpeg automatically handles out of range chapters for us
            var startOffsetMillis = (int)(startOffsetSeconds * 1000);
            foreach (var momentEdge in videoMomentEdges)
            {
                if (momentEdge.node._type != "GAME_CHANGE")
                {
                    continue;
                }

                var startMillis = momentEdge.node.positionMilliseconds - startOffsetMillis;
                var lengthMillis = momentEdge.node.durationMilliseconds;
                var gameName = momentEdge.node.details.game?.displayName ?? momentEdge.node.description;

                await sw.WriteLineAsync("[CHAPTER]");
                await sw.WriteLineAsync("TIMEBASE=1/1000");
                await sw.WriteLineAsync($"START={startMillis}");
                await sw.WriteLineAsync($"END={startMillis + lengthMillis}");
                await sw.WriteLineAsync($"title={SanitizeKeyValue(gameName)}");
            }
        }

        private static string SanitizeKeyValue(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            if (str.AsSpan().IndexOfAny(@$"=;#\{LINE_FEED}") == -1)
            {
                return str;
            }

            return new StringBuilder(str)
                .Replace("=", @"\=")
                .Replace(";", @"\;")
                .Replace("#", @"\#")
                .Replace(@"\", @"\\")
                .Replace(LINE_FEED, $@"\{LINE_FEED}")
                .ToString();
        }
    }
}
