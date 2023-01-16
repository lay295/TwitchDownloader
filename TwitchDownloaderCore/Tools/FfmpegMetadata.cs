using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore.Tools
{
    public class FfmpegMetadata
    {
        public static async Task SerializeAsync(string filePath, string streamerName, double startOffsetSeconds, int videoId, string videoTitle, DateTime videoCreation, List<VideoMomentEdge> videoMomentEdges = default, CancellationToken cancellationToken = default)
        {
            using var fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using var sw = new StreamWriter(fs)
            {
                AutoFlush = true
            };

            await SerializeGlobalMetadata(sw, streamerName, videoId, videoTitle, videoCreation);
            await fs.FlushAsync(cancellationToken);

            await SerializeChapters(sw, videoMomentEdges, startOffsetSeconds);
            await fs.FlushAsync(cancellationToken);
        }

        private static async Task SerializeGlobalMetadata(StreamWriter sw, string streamerName, int videoId, string videoTitle, DateTime videoCreation)
        {
            await sw.WriteAsync(
                GenerateSerializedGlobalMetadata(streamerName, videoId, videoTitle, videoCreation)
                );
        }

        private static string GenerateSerializedGlobalMetadata(string streamerName, int videoId, string videoTitle, DateTime videoCreation)
        {
            StringBuilder builder = new();
            builder.AppendLine(";FFMETADATA1");
            builder.AppendLine("title=" + SanatizeString(videoTitle + $" ({videoId})"));
            builder.AppendLine("artist=" + SanatizeString(streamerName));
            builder.AppendLine("date=" + SanatizeString(videoCreation.ToString("yyyy"))); // The 'date' key becomes 'year' in most formats
            builder.AppendLine("comment=" + "Originally aired " + SanatizeString(videoCreation.ToString("u")));
            builder.AppendLine();
            return builder.ToString();
        }

        private static async Task SerializeChapters(StreamWriter sw, List<VideoMomentEdge> videoMomentEdges, double startOffsetSeconds)
        {
            foreach (var momentEdge in videoMomentEdges)
            {
                if (momentEdge.node._type != "GAME_CHANGE")
                {
                    continue;
                }

                int startMillis = momentEdge.node.positionMilliseconds - (int)(startOffsetSeconds * 1000);
                int lengthMillis = momentEdge.node.durationMilliseconds;
                string gameName = momentEdge.node.details.game.displayName;

                await sw.WriteAsync(
                    GenerateSerializedChapter(startMillis, lengthMillis, gameName)
                    );
            }
        }

        private static string GenerateSerializedChapter(int startMillies, int lengthMillis, string gameName)
        {
            StringBuilder builder = new();
            builder.AppendLine("[CHAPTER]");
            builder.AppendLine("TIMEBASE=1/1000");
            builder.AppendLine("START=" + startMillies.ToString());
            builder.AppendLine("END=" + (startMillies + lengthMillis - 1).ToString());
            builder.AppendLine("title=" + SanatizeString(gameName));
            builder.AppendLine();
            return builder.ToString();
        }

        private static string SanatizeString(string str)
        {
            return str
                .Replace("=", @"\=")
                .Replace(";", @"\;")
                .Replace("#", @"\#")
                .Replace(@"\", @"\\");
        }
    }
}
