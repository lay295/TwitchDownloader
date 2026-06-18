using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tools
{
    // https://www.ffmpeg.org/ffmpeg-formats.html#concat-1
    public static class FfmpegConcatList
    {
        private const string LINE_FEED = "\u000A";

        public static async Task SerializeAsync(string filePath, IEnumerable<M3U8.Stream> playlist, StreamIds streamIds, CancellationToken cancellationToken = default)
        {
            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var sw = new StreamWriter(fs) { NewLine = LINE_FEED };

            await sw.WriteLineAsync("ffconcat version 1.0");

            foreach (var stream in playlist)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await sw.WriteAsync("file '");
                await sw.WriteAsync(DownloadTools.RemoveQueryString(stream.Path));
                await sw.WriteLineAsync('\'');

                foreach (var id in streamIds.Ids)
                {
                    await sw.WriteLineAsync("stream");
                    await sw.WriteLineAsync($"exact_stream_id {id}");
                }

                await sw.WriteAsync("duration ");
                await sw.WriteLineAsync(stream.PartInfo.Duration.ToString(CultureInfo.InvariantCulture));
            }
        }

        public record StreamIds
        {
            public static readonly StreamIds TransportStream = new("0x100", "0x101", "0x102");
            public static readonly StreamIds Mp4 = new("0x1", "0x2");
            public static readonly StreamIds None = new();

            private StreamIds(params string[] ids)
            {
                Ids = ids;
            }

            public IEnumerable<string> Ids { get; }
        }
    }
}