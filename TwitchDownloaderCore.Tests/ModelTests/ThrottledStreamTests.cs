using System.Diagnostics;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    // Tests ThrottledStream, the read-only wrapper that implements the --bandwidth limit for clip and VOD downloads.
    public class ThrottledStreamTests
    {
        private static byte[] Data(int length) => Enumerable.Range(0, length).Select(i => (byte)i).ToArray();

        /// <summary>The configured KiB/s limit is converted to bytes/s and capped at one gigabit, so absurd values cannot overflow.</summary>
        [Theory]
        [InlineData(100, 100 * 1024)]
        [InlineData(122_070, 122_070 * 1024)]
        [InlineData(int.MaxValue, 122_070 * 1024)]
        public void MaximumBytesPerSecond_IsKibibytesCappedAtGigabit(int throttleKib, int expected)
        {
            using var stream = new ThrottledStream(new MemoryStream(), throttleKib);

            Assert.Equal(expected, stream.MaximumBytesPerSecond);
        }

        /// <summary>
        /// With throttling disabled (0 or negative), reads pass straight through: every byte arrives unchanged via both
        /// the array and the span overloads.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Unthrottled_ReadsEverything(int throttleKib)
        {
            var data = Data(100_000);
            using var stream = new ThrottledStream(new MemoryStream(data), throttleKib);

            var buffer = new byte[data.Length];
            var read = stream.Read(buffer, 0, 60_000);
            read += stream.Read(buffer.AsSpan(read));

            Assert.Equal(data.Length, read);
            Assert.Equal(data, buffer);
        }

        /// <summary>
        /// A throttled read hands out at most 8 KiB before any time has passed, and the next read waits until the byte budget allows it.
        /// At 64 KiB/s, 16 KiB needs about 125 ms; a generous lower bound keeps the test stable while proving the limiter actually sleeps.
        /// </summary>
        [Fact]
        public void Throttled_LimitsChunkSizeAndWaits()
        {
            var data = Data(16 * 1024);
            using var stream = new ThrottledStream(new MemoryStream(data), 64);
            var buffer = new byte[data.Length];

            var sw = Stopwatch.StartNew();
            var first = stream.Read(buffer, 0, buffer.Length);
            var total = first;
            while (total < data.Length)
            {
                var read = stream.Read(buffer.AsSpan(total));
                Assert.NotEqual(0, read);
                total += read;
            }

            sw.Stop();

            Assert.Equal(8 * 1024, first);
            Assert.Equal(data, buffer);
            Assert.True(sw.ElapsedMilliseconds >= 60, $"Expected throttling to delay the second read, took {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Once enough time has passed, the whole requested count is granted in one read (the limiter is a budget, not a fixed chunk size).
        /// </summary>
        [Fact]
        public void Throttled_GrantsFullReadWhenBudgetAllows()
        {
            var data = Data(20_000);
            using var stream = new ThrottledStream(new MemoryStream(data), 122_070); // ~125 MB/s
            var buffer = new byte[data.Length];

            var first = stream.Read(buffer, 0, 1_000); // Starts the clock
            Thread.Sleep(50); // ~6 MB of budget
            var second = stream.Read(buffer, first, buffer.Length - first);

            Assert.Equal(1_000, first);
            Assert.Equal(data.Length - first, second);
        }

        /// <summary>
        /// The wrapper is read-only: CanWrite is false and writes/SetLength are silently ignored, while seeking and length
        /// are delegated to the base stream so callers can still report progress.
        /// </summary>
        [Fact]
        public void IsReadOnlyAndDelegatesSeeking()
        {
            var data = Data(100);
            var baseStream = new MemoryStream(data);
            using var stream = new ThrottledStream(baseStream, 0);

            stream.Write([1, 2, 3], 0, 3);
            stream.Write([1, 2, 3]);
            stream.SetLength(0);
            stream.Flush();

            Assert.True(stream.CanRead);
            Assert.True(stream.CanSeek);
            Assert.False(stream.CanWrite);
            Assert.Equal(100, stream.Length);
            Assert.Equal(data, baseStream.ToArray());

            stream.Position = 10;
            Assert.Equal(10, baseStream.Position);
            Assert.Equal(50, stream.Seek(40, SeekOrigin.Current));
            Assert.Equal(50, stream.Position);
            Assert.Same(baseStream, stream.BaseStream);
        }
    }
}
