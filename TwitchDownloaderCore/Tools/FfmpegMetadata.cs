using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore.Tools
{
    // https://ffmpeg.org/ffmpeg-formats.html#Metadata-2
    public static class FfmpegMetadata
    {
        private const string LINE_FEED = "\u000A";

        public static async Task SerializeAsync(string filePath, string videoId, VideoInfo videoInfo, TimeSpan startOffset, TimeSpan videoLength, IEnumerable<VideoMomentEdge> videoMomentEdges)
        {
            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var sw = new StreamWriter(fs) { NewLine = LINE_FEED };

            var streamer = GetUserName(videoInfo.owner?.displayName, videoInfo.owner?.login);
            var description = videoInfo.description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd();
            await SerializeGlobalMetadata(sw, streamer, videoId, videoInfo.title, videoInfo.createdAt, videoInfo.viewCount, description, videoInfo.game?.displayName);

            await SerializeChapters(sw, videoMomentEdges, startOffset, videoLength);
        }

        public static async Task SerializeAsync(string filePath, string videoId, ShareClipRenderStatusClip clip, IEnumerable<VideoMomentEdge> videoMomentEdges)
        {
            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var sw = new StreamWriter(fs) { NewLine = LINE_FEED };

            var streamer = GetUserName(clip.broadcaster?.displayName, clip.broadcaster?.login);
            var clipper = GetUserName(clip.curator?.displayName, clip.curator?.login);
            await SerializeGlobalMetadata(sw, streamer, videoId, clip.title, clip.createdAt, clip.viewCount, game: clip.game?.displayName, clipper: clipper);

            await SerializeChapters(sw, videoMomentEdges);
        }

        private static async Task SerializeGlobalMetadata(StreamWriter sw, [AllowNull] string streamer, string id, string title, DateTime createdAt, int viewCount, [AllowNull] string description = null, [AllowNull] string game = null,
            [AllowNull] string clipper = null)
        {
            // ReSharper disable once StringLiteralTypo
            await sw.WriteLineAsync(";FFMETADATA1");
            await sw.WriteLineAsync($"title={EscapeMetadataValue(title)} ({EscapeMetadataValue(id)})");
            if (!string.IsNullOrWhiteSpace(streamer))
                await sw.WriteLineAsync($"artist={EscapeMetadataValue(streamer)}");
            await sw.WriteLineAsync($"date={createdAt:yyyy}"); // The 'date' key becomes 'year' in most formats
            if (!string.IsNullOrWhiteSpace(game))
                await sw.WriteLineAsync($"genre={game}");
            await sw.WriteAsync(@"comment=");
            if (!string.IsNullOrWhiteSpace(description))
            {
                // We could use the 'description' key, but so few media players support mp4 descriptions that users would probably think it was missing
                await sw.WriteLineAsync(@$"{EscapeMetadataValue(description.TrimEnd())}\");
                await sw.WriteLineAsync(@"------------------------\");
            }
            if (!string.IsNullOrWhiteSpace(clipper))
                await sw.WriteLineAsync($@"Clipped by: {EscapeMetadataValue(clipper)}\");
            await sw.WriteLineAsync(@$"Created at: {EscapeMetadataValue(createdAt.ToString("u"))}\");
            await sw.WriteLineAsync(@$"Video id: {EscapeMetadataValue(id)}\");
            await sw.WriteLineAsync(@$"Views: {viewCount}");
        }

        private static async Task SerializeChapters(StreamWriter sw, IEnumerable<VideoMomentEdge> videoMomentEdges, TimeSpan startOffset = default, TimeSpan videoLength = default)
        {
            if (videoMomentEdges is null)
            {
                return;
            }

            var startOffsetMillis = (int)startOffset.TotalMilliseconds;
            foreach (var momentEdge in videoMomentEdges)
            {
                if (momentEdge.node._type != "GAME_CHANGE")
                {
                    continue;
                }

                var startMillis = momentEdge.node.positionMilliseconds - startOffsetMillis;
                var lengthMillis = momentEdge.node.durationMilliseconds;
                var gameName = momentEdge.node.details.game?.displayName ?? momentEdge.node.description;

                // videoLength may be 0 if it is not passed as an arg
                if (videoLength > TimeSpan.Zero)
                {
                    var chapterStart = TimeSpan.FromMilliseconds(startMillis);
                    if (chapterStart >= videoLength)
                    {
                        continue;
                    }

                    var chapterEnd = chapterStart + TimeSpan.FromMilliseconds(lengthMillis);
                    if (chapterEnd > videoLength)
                    {
                        lengthMillis = (int)(videoLength - chapterStart).TotalMilliseconds;
                    }
                }

                await sw.WriteLineAsync("[CHAPTER]");
                await sw.WriteLineAsync("TIMEBASE=1/1000");
                await sw.WriteLineAsync($"START={startMillis}");
                await sw.WriteLineAsync($"END={startMillis + lengthMillis}");
                await sw.WriteLineAsync($"title={EscapeMetadataValue(gameName)}");
            }
        }

        [return: MaybeNull]
        private static string GetUserName([AllowNull] string displayName, [AllowNull] string login)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return string.IsNullOrWhiteSpace(login) ? null : login;
            }

            if (string.IsNullOrWhiteSpace(login))
            {
                return displayName;
            }

            if (displayName.All(char.IsAscii))
            {
                return displayName;
            }

            return $"{displayName} ({login})";
        }

        // https://trac.ffmpeg.org/ticket/11096 The Ffmpeg documentation is outdated and =;# do not need to be escaped.
        // TODO: Use nameof(filename) when C# 11+
        [return: NotNullIfNotNull("str")]
        private static string EscapeMetadataValue([AllowNull] string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return str;
            }

            if (str.AsSpan().IndexOfAny(@$"\'{LINE_FEED}") == -1)
            {
                return str;
            }

            return new StringBuilder(str)
                .Replace(@"\", @"\\")
                .Replace("'", @"\'")
                .Replace(LINE_FEED, $@"\{LINE_FEED}")
                .ToString();
        }
    }
}
