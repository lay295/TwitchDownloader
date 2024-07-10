using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.Tools
{
    // https://www.ffmpeg.org/ffmpeg-formats.html#concat-1
    public static class FfmpegConcatList
    {
        private const string LINE_FEED = "\u000A";

        public static async Task SerializeAsync(string filePath, M3U8 playlist, Range videoListCrop, CancellationToken cancellationToken = default)
        {
            await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var sw = new StreamWriter(fs) { NewLine = LINE_FEED };

            await sw.WriteLineAsync("ffconcat version 1.0");

            foreach (var stream in playlist.Streams.Take(videoListCrop))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await sw.WriteAsync("file '");
                await sw.WriteAsync(DownloadTools.RemoveQueryString(stream.Path));
                await sw.WriteLineAsync('\'');

                await sw.WriteAsync("duration ");
                await sw.WriteLineAsync(stream.PartInfo.Duration.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}