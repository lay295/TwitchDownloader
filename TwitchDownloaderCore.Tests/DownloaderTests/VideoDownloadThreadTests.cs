using System.Net;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.DownloaderTests
{
    // Tests VideoDownloadThread, the worker that drains VideoDownloadState.PartQueue and downloads VOD segments. Its retry,
    // un-mute and attempt-limit rules decide whether a flaky CDN produces a complete VOD, a VOD with stubs, or a hang.
    public class VideoDownloadThreadTests
    {
        private static readonly Uri BaseUrl = new($"{FakeTwitch.VOD_BASE}chunked/");

        private static VideoDownloadState State(DateTimeOffset airDate, params string[] parts)
        {
            var streams = parts.Select(p => new M3U8.Stream(new M3U8.Stream.ExtPartInfo(1, false), default, default, p)).ToArray();
            return new VideoDownloadState(["chunked"], streams, Range.All, BaseUrl, null, airDate);
        }

        private static VideoDownloadState OldVodState(params string[] parts) => State(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), parts);

        private static async Task RunToCompletion(VideoDownloadState state, FakeHttpMessageHandler handler, TempDirectory temp, RecordingTaskProgress logger, int threads = 1)
        {
            using var client = new HttpClient(handler);
            var workers = Enumerable.Range(0, threads).Select(_ => new VideoDownloadThread(state, client, temp.Path, -1, logger, CancellationToken.None)).ToArray();
            await Task.WhenAll(workers.Select(w => w.ThreadTask));
        }

        private static HttpResponseMessage Serve(Uri uri, Dictionary<string, byte[]> files) =>
            files.TryGetValue(uri.Segments.Last(), out var bytes) ? FakeHttpMessageHandler.Bytes(bytes) : new HttpResponseMessage(HttpStatusCode.NotFound);

        /// <summary>
        /// Several workers share one queue: every part is downloaded exactly once, to a file named after the part (query
        /// string stripped), and its Content-Length is recorded as the expected size used later by part verification.
        /// </summary>
        [Fact]
        public async Task Workers_ShareQueue_AndDownloadEveryPartOnce()
        {
            using var temp = new TempDirectory();
            var files = new Dictionary<string, byte[]> { ["0.ts"] = FakeTwitch.TsSegment(0), ["1.ts"] = FakeTwitch.TsSegment(1), ["2.ts"] = FakeTwitch.TsSegment(2) };
            var handler = new FakeHttpMessageHandler((req, _) => Serve(req.RequestUri!, files));
            var state = OldVodState("0.ts", "1.ts?start=1", "2.ts");
            var logger = new RecordingTaskProgress();

            await RunToCompletion(state, handler, temp, logger, threads: 2);

            Assert.Equal(3, handler.Requests.Count);
            Assert.Equal(files["0.ts"], await File.ReadAllBytesAsync(temp.Combine("0.ts"), TestContext.Current.CancellationToken));
            Assert.Equal(files["1.ts"], await File.ReadAllBytesAsync(temp.Combine("1.ts"), TestContext.Current.CancellationToken));
            Assert.Equal(files["1.ts"].Length, state.PartStates["1.ts?start=1"].ExpectedFileSize);
            Assert.Empty(logger.Warnings);
        }

        /// <summary>
        /// Transient CDN failures (a 500, then an empty 200) are retried until the part arrives, and each is logged.
        /// Guards against a single hiccup dropping a part or killing the worker.
        /// </summary>
        [Fact]
        public async Task TransientFailures_AreRetried()
        {
            using var temp = new TempDirectory();
            var calls = 0;
            var handler = new FakeHttpMessageHandler((_, _) => Interlocked.Increment(ref calls) switch
            {
                1 => new HttpResponseMessage(HttpStatusCode.InternalServerError),
                2 => FakeHttpMessageHandler.Bytes([]),
                _ => FakeHttpMessageHandler.Bytes(FakeTwitch.TsSegment(0))
            });
            var state = OldVodState("0.ts");
            var logger = new RecordingTaskProgress();

            await RunToCompletion(state, handler, temp, logger);

            Assert.Equal(3, calls);
            Assert.Equal(FakeTwitch.TsSegment(0), await File.ReadAllBytesAsync(temp.Combine("0.ts"), TestContext.Current.CancellationToken));
            Assert.Contains(logger.Verbose, x => x.Contains("Received 500"));
            Assert.Contains(logger.Warnings, x => x.Contains("Got file size of 0B for 0.ts"));
            Assert.Equal(3, state.PartStates["0.ts"].DownloadAttempts);
        }

        /// <summary>
        /// A body shorter than its Content-Length (connection dropped mid-part) fails the on-disk size check and the part is
        /// downloaded again, so a truncated segment never reaches the concat step.
        /// </summary>
        [Fact]
        public async Task TruncatedBody_FailsSizeCheck_AndIsRetried()
        {
            using var temp = new TempDirectory();
            var full = FakeTwitch.TsSegment(0);
            var calls = 0;
            var handler = new FakeHttpMessageHandler((_, _) =>
            {
                if (Interlocked.Increment(ref calls) > 1) return FakeHttpMessageHandler.Bytes(full);
                var truncated = FakeHttpMessageHandler.Bytes(full[..1000]);
                truncated.Content.Headers.ContentLength = full.Length;
                return truncated;
            });
            var logger = new RecordingTaskProgress();

            await RunToCompletion(OldVodState("0.ts"), handler, temp, logger);

            Assert.Equal(2, calls);
            Assert.Equal(full, await File.ReadAllBytesAsync(temp.Combine("0.ts"), TestContext.Current.CancellationToken));
            Assert.Contains(logger.Verbose, x => x.Contains($"failed to verify: expected {full.Length:N0}B, got {1000:N0}B")); // Same (current) culture as the production log
        }

        /// <summary>
        /// For VODs under 24h old, "-muted" parts are first requested under their unmuted name (Twitch may still have the
        /// original audio). A 403 disables un-muting for that part without consuming a download attempt, and the muted part
        /// is fetched instead. Muted parts are queued first so the slow path starts early.
        /// </summary>
        [Fact]
        public async Task RecentVod_TriesUnmutedPartFirst_FallsBackToMutedOn403()
        {
            using var temp = new TempDirectory();
            var muted = FakeTwitch.TsSegment(1);
            var handler = new FakeHttpMessageHandler((req, _) => req.RequestUri!.Segments.Last() switch
            {
                "5-muted.ts" => FakeHttpMessageHandler.Bytes(muted),
                "4.ts" => FakeHttpMessageHandler.Bytes(FakeTwitch.TsSegment(0)),
                _ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            });
            var state = State(DateTimeOffset.UtcNow.AddHours(-1), "4.ts", "5-muted.ts");
            Assert.Equal("5-muted.ts", state.PartQueue.First());

            await RunToCompletion(state, handler, temp, new RecordingTaskProgress());

            var requested = handler.Requests.Select(r => r.Uri.Segments.Last()).ToArray();
            Assert.Equal(["5.ts", "4.ts", "5-muted.ts"], requested); // The muted part is re-queued behind 4.ts after the 403
            Assert.Equal(muted, await File.ReadAllBytesAsync(temp.Combine("5-muted.ts"), TestContext.Current.CancellationToken));
            Assert.False(state.PartStates["5-muted.ts"].TryUnmute);
            Assert.Equal(1, state.PartStates["5-muted.ts"].DownloadAttempts);
        }

        /// <summary>When the unmuted copy of a recent VOD's part exists, it is saved under the part's original (muted) name.</summary>
        [Fact]
        public async Task RecentVod_SavesUnmutedAudioUnderMutedName()
        {
            using var temp = new TempDirectory();
            var unmuted = FakeTwitch.TsSegment(2);
            var handler = new FakeHttpMessageHandler((req, _) => req.RequestUri!.Segments.Last() == "5.ts"
                ? FakeHttpMessageHandler.Bytes(unmuted)
                : new HttpResponseMessage(HttpStatusCode.NotFound));
            var state = State(DateTimeOffset.UtcNow, "5-muted.ts");

            await RunToCompletion(state, handler, temp, new RecordingTaskProgress());

            Assert.Equal(unmuted, await File.ReadAllBytesAsync(temp.Combine("5-muted.ts"), TestContext.Current.CancellationToken));
            Assert.Single(handler.Requests);
        }

        /// <summary>
        /// A part that has already used its 5 attempts makes the worker fault (so VideoDownloader can count/restart threads)
        /// without another HTTP request and without re-queueing the part, which would otherwise loop forever.
        /// </summary>
        [Fact]
        public async Task ExhaustedAttempts_FaultWorker_WithoutRequeue()
        {
            using var temp = new TempDirectory();
            var handler = FakeHttpMessageHandler.NotFound();
            var state = OldVodState("0.ts");
            state.PartStates["0.ts"].DownloadAttempts = 5;
            var logger = new RecordingTaskProgress();

            var ex = await Assert.ThrowsAsync<Exception>(() => RunToCompletion(state, handler, temp, logger));

            Assert.Contains("failed to download after 5 attempts", ex.Message);
            Assert.Empty(handler.Requests);
            Assert.Empty(state.PartQueue);
            Assert.Contains(logger.Verbose, x => x.StartsWith("Error while downloading 0.ts"));
        }

        /// <summary>
        /// A part already on disk with its known expected size is skipped (with a warning) instead of re-downloaded, which
        /// is what makes re-queued "missing part" passes cheap.
        /// </summary>
        [Fact]
        public async Task AlreadyDownloadedPart_IsNotRequestedAgain()
        {
            using var temp = new TempDirectory();
            await File.WriteAllBytesAsync(temp.Combine("0.ts"), new byte[376], TestContext.Current.CancellationToken);
            var handler = FakeHttpMessageHandler.NotFound();
            var state = OldVodState("0.ts");
            state.PartStates["0.ts"].ExpectedFileSize = 376;
            var logger = new RecordingTaskProgress();

            await RunToCompletion(state, handler, temp, logger);

            Assert.Empty(handler.Requests);
            Assert.Contains(logger.Warnings, x => x.Contains("Tried to redownload already downloaded part: 0.ts"));
        }

        /// <summary>
        /// If a re-download reports a different Content-Length than an earlier attempt, the larger size wins and a warning
        /// is logged; a TS part whose size is not a multiple of 188 bytes is flagged as malformed. Both are diagnostics users
        /// rely on when a VOD has glitches.
        /// </summary>
        [Fact]
        public async Task SizeDisagreementAndMalformedPackets_AreWarned()
        {
            using var temp = new TempDirectory();
            var handler = new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Bytes(new byte[200]));
            var state = OldVodState("0.ts");
            state.PartStates["0.ts"].ExpectedFileSize = 100;
            var logger = new RecordingTaskProgress();

            await RunToCompletion(state, handler, temp, logger);

            Assert.Equal(200, state.PartStates["0.ts"].ExpectedFileSize);
            Assert.Contains(logger.Warnings, x => x.Contains("Got two different file sizes for 0.ts"));
            Assert.Contains(logger.Warnings, x => x.Contains("0.ts contains malformed packets"));
        }

        /// <summary>
        /// Restarting a worker that is still running is refused; VideoDownloader only restarts faulted workers, and a
        /// double start would let two tasks share one CTS watchdog.
        /// </summary>
        [Fact]
        public async Task StartDownload_WhileRunning_Throws()
        {
            using var temp = new TempDirectory();
            using var release = new SemaphoreSlim(0);
            var handler = new FakeHttpMessageHandler((_, _) =>
            {
                release.Wait(TimeSpan.FromSeconds(10));
                return FakeHttpMessageHandler.Bytes(FakeTwitch.TsSegment(0));
            });
            using var client = new HttpClient(handler);
            var worker = new VideoDownloadThread(OldVodState("0.ts"), client, temp.Path, -1, new RecordingTaskProgress(), CancellationToken.None);

            Assert.Throws<InvalidOperationException>(() => worker.StartDownload());

            release.Release();
            await worker.ThreadTask;
        }

        /// <summary>
        /// An HttpClient.Timeout (surfaced by HttpClient as a TaskCanceledException mentioning the timeout) is a transient
        /// failure: it is logged as "timed out" and the part is retried after a back-off, instead of being treated as a
        /// cancellation that kills the worker. The back-off is 5s, so the test cancels the worker once the timeout has been
        /// classified and asserts on the log/attempt count rather than waiting for the retry itself.
        /// </summary>
        [Fact]
        public async Task HttpClientTimeout_IsLoggedAndRetried_NotFatal()
        {
            using var temp = new TempDirectory();
            using var cts = new CancellationTokenSource();
            var handler = new FakeHttpMessageHandler((_, _) =>
            {
                cts.CancelAfter(200); // Cancel the retry back-off that follows the timeout so the test stays fast
                throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.");
            });
            using var client = new HttpClient(handler);
            var state = OldVodState("0.ts");
            var logger = new RecordingTaskProgress();

            var worker = new VideoDownloadThread(state, client, temp.Path, -1, logger, cts.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.ThreadTask);
            Assert.Single(handler.Requests);
            Assert.Contains(logger.Verbose, x => x == "0.ts timed out.");
            Assert.Equal(1, state.PartStates["0.ts"].DownloadAttempts); // The timeout consumed an attempt but did not fault the worker by itself
        }

        /// <summary>A cancelled token stops the worker before it downloads anything.</summary>
        [Fact]
        public async Task CancelledToken_StopsWorker()
        {
            using var temp = new TempDirectory();
            var handler = new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Bytes(FakeTwitch.TsSegment(0)));
            using var client = new HttpClient(handler);

            var worker = new VideoDownloadThread(OldVodState("0.ts"), client, temp.Path, -1, new RecordingTaskProgress(), new CancellationToken(true));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.ThreadTask);
            Assert.Empty(handler.Requests);
        }
    }
}
