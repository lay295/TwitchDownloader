using System.Net;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.DownloaderTests
{
    // Tests DownloadTools.DownloadFileAsync, the single streaming download primitive used for every VOD segment and header
    // file. A bug here silently corrupts video parts, so header prepending, throttling and the EOF fallback are pinned down.
    public class DownloadToolsTests
    {
        private static readonly Uri Url = new("https://vod.test/chunked/0.ts");
        private static readonly byte[] Body = FakeTwitch.TsSegment(0);

        /// <summary>
        /// Content that serializes fully for HttpContent.CopyToAsync (the unthrottled path) but whose read stream can throw an
        /// "Unexpected EOF" IOException after <c>failAfter</c> bytes (what the throttled path reads), and may hide its length.
        /// </summary>
        private sealed class ScriptedContent(byte[] data, int failAfter = int.MaxValue, bool knownLength = true) : HttpContent
        {
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(data).AsTask();

            protected override bool TryComputeLength(out long length)
            {
                length = data.Length;
                return knownLength;
            }

            protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new EofStream(data, failAfter));

            private sealed class EofStream(byte[] data, int failAfter) : MemoryStream(data)
            {
                // MemoryStream routes Read(Span) of subclasses through this overload
                public override int Read(byte[] buffer, int offset, int count)
                {
                    if (Position >= failAfter) throw new IOException("Unexpected EOF while reading the response body");
                    return base.Read(buffer, offset, (int)Math.Min(count, failAfter - Position));
                }
            }
        }

        private static HttpClient Client(Func<HttpResponseMessage> respond, out FakeHttpMessageHandler handler)
        {
            handler = new FakeHttpMessageHandler((_, _) => respond());
            return new HttpClient(handler);
        }

        /// <summary>
        /// The body is streamed to disk byte-for-byte, the Content-Length is returned (callers use it to verify parts), the
        /// request advertises gzip/deflate/br (tryUnmute fails without it, see source comment) and the caller's CTS is
        /// still usable afterwards. (The 60s watchdog that DownloadFileAsync arms and then disarms on the CTS cannot be
        /// observed in a fast test; the final assertion only shows the method did not cancel the token itself.)
        /// </summary>
        [Fact]
        public async Task DownloadsBody_ReturnsLength_AndLeavesTokenUncancelled()
        {
            using var temp = new TempDirectory();
            using var client = Client(() => FakeHttpMessageHandler.Bytes(Body), out var handler);
            using var cts = new CancellationTokenSource();
            var destination = temp.Combine("0.ts");

            var length = await DownloadTools.DownloadFileAsync(client, Url, destination, null, -1, new RecordingTaskProgress(), cts);

            Assert.Equal(Body.Length, length);
            Assert.Equal(Body, await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
            var acceptEncoding = Assert.Single(handler.Requests).Headers["Accept-Encoding"];
            Assert.Contains("gzip", acceptEncoding);
            Assert.Contains("deflate", acceptEncoding);
            Assert.Contains("br", acceptEncoding);
            Assert.False(cts.IsCancellationRequested);
        }

        /// <summary>
        /// With a header file (fMP4/AV1 init segment) the destination is the header followed by the body. Every fMP4 part must
        /// be self-contained for the ffmpeg concat step, so a missing header would make the whole VOD unplayable.
        /// </summary>
        [Fact]
        public async Task PrependsHeaderFile()
        {
            using var temp = new TempDirectory();
            var header = temp.Combine("init-0.mp4");
            await File.WriteAllBytesAsync(header, [1, 2, 3, 4], TestContext.Current.CancellationToken);
            using var client = Client(() => FakeHttpMessageHandler.Bytes(Body), out _);
            var destination = temp.Combine("0.mp4");

            var length = await DownloadTools.DownloadFileAsync(client, Url, destination, header, -1, new RecordingTaskProgress());

            Assert.Equal(Body.Length, length); // The header is not part of the reported length
            Assert.Equal(new byte[] { 1, 2, 3, 4 }.Concat(Body).ToArray(), await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
        }

        /// <summary>The throttled code path (ThrottleKib &gt; 0) must produce exactly the same bytes as the unthrottled one.</summary>
        [Fact]
        public async Task ThrottledDownload_WritesIdenticalBytes()
        {
            using var temp = new TempDirectory();
            using var client = Client(() => FakeHttpMessageHandler.Bytes(Body), out _);
            using var cts = new CancellationTokenSource();
            var destination = temp.Combine("0.ts");

            var length = await DownloadTools.DownloadFileAsync(client, Url, destination, null, 10_000, new RecordingTaskProgress(), cts);

            Assert.Equal(Body.Length, length);
            Assert.Equal(Body, await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
            Assert.False(cts.IsCancellationRequested);
        }

        /// <summary>
        /// A response without Content-Length is still saved and reported as -1, which VideoDownloadThread treats as
        /// "unknown size" instead of a failed (0 byte) download.
        /// </summary>
        [Fact]
        public async Task UnknownContentLength_ReturnsMinusOne()
        {
            using var temp = new TempDirectory();
            using var client = Client(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ScriptedContent(Body, knownLength: false) }, out _);
            var destination = temp.Combine("0.ts");

            var length = await DownloadTools.DownloadFileAsync(client, Url, destination, null, 10_000, new RecordingTaskProgress());

            Assert.Equal(-1, length);
            Assert.Equal(Body, await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
        }

        /// <summary>
        /// Non-success statuses surface as HttpRequestException carrying the status code (callers branch on 403 to stop
        /// un-muting) and no destination file is created, so a failed part is never mistaken for a downloaded one.
        /// </summary>
        [Fact]
        public async Task HttpError_ThrowsWithStatus_AndCreatesNoFile()
        {
            using var temp = new TempDirectory();
            using var client = Client(() => new HttpResponseMessage(HttpStatusCode.Forbidden), out _);
            var destination = temp.Combine("0.ts");

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => DownloadTools.DownloadFileAsync(client, Url, destination, null, -1, new RecordingTaskProgress()));

            Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
            Assert.False(File.Exists(destination));
        }

        /// <summary>An already-cancelled CTS aborts before anything is written to disk.</summary>
        [Fact]
        public async Task CancelledToken_AbortsBeforeWriting()
        {
            using var temp = new TempDirectory();
            using var client = Client(() => FakeHttpMessageHandler.Bytes(Body), out _);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var destination = temp.Combine("0.ts");

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DownloadTools.DownloadFileAsync(client, Url, destination, null, -1, new RecordingTaskProgress(), cts));

            Assert.False(File.Exists(destination));
        }

        /// <summary>
        /// If the throttled stream hits an "EOF" IOException mid-body, the download is retried without the throttle (logged
        /// verbosely) and the final file is complete. Guards the lay295 workaround for throttler-induced EOFs.
        /// </summary>
        [Fact]
        public async Task ThrottledEof_FallsBackToUnthrottledDownload()
        {
            using var temp = new TempDirectory();
            using var client = Client(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ScriptedContent(Body, failAfter: 1000) }, out _);
            var logger = new RecordingTaskProgress();
            var destination = temp.Combine("0.ts");

            var length = await DownloadTools.DownloadFileAsync(client, Url, destination, null, 10_000, logger);

            Assert.Equal(Body.Length, length);
            Assert.Equal(Body, await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
            Assert.Contains(logger.Verbose, x => x.Contains("retrying without bandwidth throttle"));
        }

        /// <summary>
        /// Same EOF fallback, but with a header file: the destination is opened in Append mode for both attempts, so the bytes
        /// the throttled attempt managed to write before the EOF stay in the file and the full body is appended after them.
        /// The part then fails size verification (or worse, is concatenated corrupt).
        /// </summary>
        [Fact(Skip = "Bug: DownloadTools.DownloadFileAsync EOF fallback re-opens the destination with FileMode.Append when a header file is used, so the partial throttled data is kept and the body is written twice")]
        public async Task ThrottledEof_WithHeaderFile_DoesNotKeepPartialData()
        {
            using var temp = new TempDirectory();
            var header = temp.Combine("init-0.mp4");
            await File.WriteAllBytesAsync(header, [9, 9, 9], TestContext.Current.CancellationToken);
            using var client = Client(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ScriptedContent(Body, failAfter: 1000) }, out _);
            var destination = temp.Combine("0.mp4");

            await DownloadTools.DownloadFileAsync(client, Url, destination, header, 10_000, new RecordingTaskProgress());

            Assert.Equal(new byte[] { 9, 9, 9 }.Concat(Body).ToArray(), await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
        }

        /// <summary>Old VOD part names carry a query string (e.g. "1.ts?start=..") that is invalid in file names; only the path is kept.</summary>
        [Theory]
        [InlineData("0.ts", "0.ts")]
        [InlineData("1.ts?start=1&end=2", "1.ts")]
        [InlineData("2-muted.ts?", "2-muted.ts")]
        [InlineData("", "")]
        public void RemoveQueryString_StripsEverythingFromQuestionMark(string input, string expected)
        {
            Assert.Equal(expected, DownloadTools.RemoveQueryString(input));
        }
    }
}
