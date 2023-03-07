﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore.Tools
{
    // https://ffmpeg.org/ffmpeg-formats.html#Metadata-1
    public static class FfmpegMetadata
    {
        private const string LINE_FEED = "\u000A";

        public static async Task SerializeAsync(string filePath, string streamerName, double startOffsetSeconds, int videoId, string videoTitle, DateTime videoCreation, List<VideoMomentEdge> videoMomentEdges = default, CancellationToken cancellationToken = default)
        {
            await using var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            await using var sw = new StreamWriter(fs) { NewLine = LINE_FEED };

            await SerializeGlobalMetadata(sw, streamerName, videoId, videoTitle, videoCreation);
            await fs.FlushAsync(cancellationToken);

            await SerializeChapters(sw, videoMomentEdges, startOffsetSeconds);
            await fs.FlushAsync(cancellationToken);
        }

        private static async Task SerializeGlobalMetadata(StreamWriter sw, string streamerName, int videoId, string videoTitle, DateTime videoCreation)
        {
            await sw.WriteLineAsync(";FFMETADATA1");
            await sw.WriteLineAsync($"title={SanitizeKeyValue(videoTitle)} ({videoId})");
            await sw.WriteLineAsync($"artist={SanitizeKeyValue(streamerName)}");
            await sw.WriteLineAsync($"date={videoCreation:yyyy}"); // The 'date' key becomes 'year' in most formats
            await sw.WriteLineAsync(@$"comment=Originally aired: {SanitizeKeyValue(videoCreation.ToString("u"))}\");
            await sw.WriteLineAsync($"Video id: {videoId}");
        }

        private static async Task SerializeChapters(StreamWriter sw, List<VideoMomentEdge> videoMomentEdges, double startOffsetSeconds)
        {
            // Note: Ffmpeg automatically handles out of range chapters for us
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

            return str
                .Replace("=", @"\=")
                .Replace(";", @"\;")
                .Replace("#", @"\#")
                .Replace(@"\", @"\\");
        }
    }
}
