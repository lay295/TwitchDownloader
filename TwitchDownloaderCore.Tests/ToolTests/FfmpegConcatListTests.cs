using System.Globalization;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests FfmpegConcatList, which writes the ffconcat file ffmpeg uses to stitch downloaded VOD parts into one video.
    // A malformed line here makes ffmpeg fail or drop streams at the very end of a long download.
    public class FfmpegConcatListTests
    {
        private static M3U8.Stream Part(string path, decimal duration) => new(new M3U8.Stream.ExtPartInfo(duration, false), default, default, path);

        /// <summary>
        /// Each part gets a quoted file line (query string stripped), one stream/exact_stream_id pair per requested stream id and its
        /// duration. Lines end with LF on every OS, and durations use '.' even under a comma-decimal culture (ffmpeg rejects "2,5").
        /// </summary>
        [Fact]
        public async Task WritesFfconcatFile()
        {
            using var temp = new TempDirectory();
            var path = temp.Combine("concat.txt");
            var oldCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                await FfmpegConcatList.SerializeAsync(path, [Part("0.ts?token=abc", 10.010m), Part("1-muted.ts", 2.5m)], FfmpegConcatList.StreamIds.Mp4,
                    TestContext.Current.CancellationToken);
            }
            finally
            {
                CultureInfo.CurrentCulture = oldCulture;
            }

            Assert.Equal(
                "ffconcat version 1.0\n" +
                "file '0.ts'\nstream\nexact_stream_id 0x1\nstream\nexact_stream_id 0x2\nduration 10.010\n" +
                "file '1-muted.ts'\nstream\nexact_stream_id 0x1\nstream\nexact_stream_id 0x2\nduration 2.5\n",
                await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        }

        /// <summary>
        /// Transport stream VODs map the three MPEG-TS PIDs, while StreamIds.None writes no stream directives at all
        /// (ffmpeg then takes every stream from each file).
        /// </summary>
        [Fact]
        public async Task WritesStreamIdsPerContainer()
        {
            using var temp = new TempDirectory();
            var tsPath = temp.Combine("ts.txt");
            var nonePath = temp.Combine("none.txt");

            await FfmpegConcatList.SerializeAsync(tsPath, [Part("0.ts", 2)], FfmpegConcatList.StreamIds.TransportStream, TestContext.Current.CancellationToken);
            await FfmpegConcatList.SerializeAsync(nonePath, [Part("0.ts", 2)], FfmpegConcatList.StreamIds.None, TestContext.Current.CancellationToken);

            Assert.Equal("ffconcat version 1.0\nfile '0.ts'\nstream\nexact_stream_id 0x100\nstream\nexact_stream_id 0x101\nstream\nexact_stream_id 0x102\nduration 2\n",
                await File.ReadAllTextAsync(tsPath, TestContext.Current.CancellationToken));
            Assert.Equal("ffconcat version 1.0\nfile '0.ts'\nduration 2\n", await File.ReadAllTextAsync(nonePath, TestContext.Current.CancellationToken));
        }

        /// <summary>A cancelled token stops writing with an OperationCanceledException instead of finishing a list nobody will use.</summary>
        [Fact]
        public async Task ThrowsWhenCancelled()
        {
            using var temp = new TempDirectory();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                FfmpegConcatList.SerializeAsync(temp.Combine("concat.txt"), [Part("0.ts", 2)], FfmpegConcatList.StreamIds.Mp4, new CancellationToken(true)));
        }
    }
}
