using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tests.ExtensionTests
{
    // Tests StreamExtensions.ProgressCopyToAsync, which copies clip downloads and CLI self-updates to disk while reporting progress.
    public class StreamExtensionsTests
    {
        // Records reports synchronously (Progress<T> would post them asynchronously and make assertions racy).
        private sealed class ListProgress : IProgress<StreamCopyProgress>
        {
            public List<StreamCopyProgress> Reports { get; } = [];
            public void Report(StreamCopyProgress value) => Reports.Add(value);
        }

        private static byte[] Data(int length) => Enumerable.Range(0, length).Select(i => (byte)i).ToArray();

        /// <summary>
        /// With a known length and a progress sink, all bytes are copied and progress is reported once per chunk with a strictly
        /// increasing running total that ends at the source length. Guards the clip download progress bar.
        /// (Chunk size is whatever ArrayPool hands out for 81,920 bytes, so only the shape of the reports is asserted.)
        /// </summary>
        [Fact]
        public async Task CopiesAllBytesAndReportsCumulativeProgress()
        {
            var data = Data(200_000);
            using var source = new MemoryStream(data);
            using var destination = new MemoryStream();
            var progress = new ListProgress();

            await source.ProgressCopyToAsync(destination, data.Length, progress, TestContext.Current.CancellationToken);

            Assert.Equal(data, destination.ToArray());
            var copied = progress.Reports.Select(x => x.BytesCopied).ToArray();
            Assert.True(copied.Length >= 2, "Expected the 200 KB source to be copied in multiple chunks");
            Assert.Equal(copied.Order(), copied);
            Assert.Equal(copied.Length, copied.Distinct().Count());
            Assert.Equal(data.Length, copied[^1]);
            Assert.All(progress.Reports, x => Assert.Equal(data.Length, x.SourceLength));
        }

        /// <summary>
        /// Without a length (e.g. no Content-Length header) or without a progress sink, it falls back to a plain copy
        /// and must still copy every byte.
        /// </summary>
        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public async Task FallsBackToPlainCopy_WhenLengthOrProgressMissing(bool hasLength, bool hasProgress)
        {
            var data = Data(100_000);
            using var source = new MemoryStream(data);
            using var destination = new MemoryStream();
            var progress = new ListProgress();

            await source.ProgressCopyToAsync(destination, hasLength ? data.Length : null, hasProgress ? progress : null, TestContext.Current.CancellationToken);

            Assert.Equal(data, destination.ToArray());
            Assert.Empty(progress.Reports);
        }

        /// <summary>A cancelled token must stop the copy with an OperationCanceledException so a cancelled download does not keep writing.</summary>
        [Fact]
        public async Task ThrowsWhenCancelled()
        {
            using var source = new MemoryStream(Data(1000));
            using var destination = new MemoryStream();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                source.ProgressCopyToAsync(destination, 1000, new ListProgress(), new CancellationToken(true)));
        }
    }
}
