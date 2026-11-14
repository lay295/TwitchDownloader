using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.DownloaderTests
{
    // Tests VideoDownloader, the VOD download pipeline: GQL info/token -> usher master playlist -> quality selection -> media
    // playlist -> parallel segment download -> verification/stubs -> ffmpeg concat with metadata. Every HTTP call is served by
    // FakeTwitch; the final ffmpeg step uses the real ffmpeg (tests that need it are skipped locally when it is missing).
    [Collection(StaticHttpClientCollection.NAME)]
    public class VideoDownloaderTests
    {
        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetStreamListTrim")]
        private static extern Range GetStreamListTrim(VideoDownloader downloader, IList<M3U8.Stream> streamList, out TimeSpan videoLength, out decimal startOffset, out decimal endDuration);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetVideoPlaylist")]
        private static extern Task<(M3U8 playlist, DateTimeOffset airDate)> GetVideoPlaylist(VideoDownloader downloader, string playlistUrl, CancellationToken cancellationToken);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "VerifyDownloadedParts")]
        private static extern Task VerifyDownloadedParts(VideoDownloader downloader, VideoDownloadState downloadState, CancellationToken cancellationToken);

        private static VideoDownloader Create(FakeTwitch twitch, TempDirectory temp, RecordingTaskProgress progress, Action<VideoDownloadOptions>? configure = null)
        {
            var options = new VideoDownloadOptions
            {
                Id = FakeTwitch.VIDEO_ID,
                Quality = "1080p60",
                Filename = temp.Combine("out", "vod.mp4"),
                DownloadThreads = 2,
                ThrottleKib = -1,
                FfmpegPath = TestData.FfmpegPath ?? "ffmpeg",
                TempFolder = temp.Combine("temp"),
                CacheCleanerCallback = dirs => dirs,
                TrimMode = VideoTrimMode.Exact
            };
            configure?.Invoke(options);
            return new VideoDownloader(options, progress) { _httpClient = new HttpClient(twitch.Handler) };
        }

        private static HttpClientOverride FakeGql(FakeTwitch twitch) => new(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, twitch.Handler);

        private static string CacheDir(TempDirectory temp) => temp.Combine("temp", "TwitchDownloader");

        private static string[] VodCacheDirs(TempDirectory temp) =>
            Directory.Exists(CacheDir(temp)) ? Directory.GetDirectories(CacheDir(temp), $"{FakeTwitch.VIDEO_ID}_*") : [];

        private static M3U8.Stream[] Parts(params decimal[] durations) =>
            durations.Select((d, i) => new M3U8.Stream(new M3U8.Stream.ExtPartInfo(d, false), default, default, $"{i}.ts")).ToArray();

        /// <summary>
        /// The happy path: a 3-part TS VOD becomes a playable mp4 with video+audio of the full length, ffmpeg metadata
        /// (title/artist/genre from the GQL video info), the OAuth token is sent with the access token request, abandoned
        /// caches older than a week are offered to the cleaner callback, and the per-download cache is deleted afterwards.
        /// </summary>
        [Fact]
        public async Task DownloadsTsVod_EndToEnd()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            twitch.AddTsVod();
            var abandoned = Directory.CreateDirectory(Path.Combine(CacheDir(temp), "111_638000000000000000"));
            abandoned.LastWriteTimeUtc = DateTime.UtcNow.AddDays(-8);
            var progress = new RecordingTaskProgress();
            using var _ = FakeGql(twitch);
            var downloader = Create(twitch, temp, progress, o => o.Oauth = "fakeoauth");

            await downloader.DownloadAsync(CancellationToken.None);

            var output = temp.Combine("out", "vod.mp4");
            var (metadata, report) = MediaProbe.Probe(output);
            Assert.InRange(MediaProbe.DurationSeconds(report), 2.8, 3.5);
            Assert.Contains("Video: h264", report);
            Assert.Contains("Audio: aac", report);
            Assert.Contains($"title=Fake VOD ({FakeTwitch.VIDEO_ID})", metadata);
            Assert.Contains("artist=FakeStreamer", metadata);
            Assert.Contains("genre=Science & Technology", metadata);

            Assert.Equal("OAuth fakeoauth", Assert.Single(twitch.GqlRequests("PlaybackAccessToken_Template")).Headers["Authorization"]);
            Assert.Contains("sig=fakesig", Assert.Single(twitch.RequestsTo("usher.ttvnw.net")).Uri.Query);
            Assert.Equal(3, twitch.RequestsTo("/chunked/").Count(r => r.Uri.AbsolutePath.EndsWith(".ts")));
            Assert.False(abandoned.Exists);
            Assert.Contains(progress.Info, x => x.Contains("1 old video caches were deleted"));
            Assert.Empty(VodCacheDirs(temp));
            Assert.Contains(100, progress.Percentages);
            Assert.Empty(progress.Errors);
        }

        /// <summary>
        /// The quality string picks the matching variant of the usher master playlist: asking for "720p30" must download
        /// only the 720p30 rendition and never touch the source ("chunked") one.
        /// </summary>
        [Fact]
        public async Task Quality_SelectsMatchingRendition()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            twitch.AddTsVod("720p30");
            using var _ = FakeGql(twitch);

            await Create(twitch, temp, new RecordingTaskProgress(), o => o.Quality = "720p30").DownloadAsync(CancellationToken.None);

            Assert.Equal(4, twitch.RequestsTo("/720p30/").Count()); // playlist + 3 parts
            Assert.Empty(twitch.RequestsTo("/chunked/"));
            Assert.True(new FileInfo(temp.Combine("out", "vod.mp4")).Length > 0);
        }

        /// <summary>
        /// Exact trim mode keeps the requested 0.5s..2.5s window: all overlapping parts are downloaded and ffmpeg cuts with
        /// -ss/-t, so the output is ~2s long instead of the full 3s.
        /// </summary>
        [Fact]
        public async Task ExactTrim_CutsInsideParts()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            twitch.AddTsVod();
            using var _ = FakeGql(twitch);
            var downloader = Create(twitch, temp, new RecordingTaskProgress(), o =>
            {
                o.TrimBeginning = true;
                o.TrimBeginningTime = TimeSpan.FromSeconds(0.5);
                o.TrimEnding = true;
                o.TrimEndingTime = TimeSpan.FromSeconds(2.5);
            });

            await downloader.DownloadAsync(CancellationToken.None);

            var (_, report) = MediaProbe.Probe(temp.Combine("out", "vod.mp4"));
            Assert.InRange(MediaProbe.DurationSeconds(report), 1.7, 2.4);
        }

        /// <summary>
        /// Safe trim mode only drops whole parts: trimming the beginning to 1.5s skips part 0 entirely (it is never
        /// requested) and keeps parts 1 and 2 uncut. Guards against wasting bandwidth on trimmed-away parts.
        /// </summary>
        [Fact]
        public async Task SafeTrim_SkipsWholePartsOnly()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            twitch.AddTsVod();
            using var _ = FakeGql(twitch);
            var downloader = Create(twitch, temp, new RecordingTaskProgress(), o =>
            {
                o.TrimMode = VideoTrimMode.Safe;
                o.TrimBeginning = true;
                o.TrimBeginningTime = TimeSpan.FromSeconds(1.5);
            });

            await downloader.DownloadAsync(CancellationToken.None);

            Assert.Empty(twitch.RequestsTo("/chunked/0.ts"));
            Assert.Single(twitch.RequestsTo("/chunked/1.ts"));
            var (_, report) = MediaProbe.Probe(temp.Combine("out", "vod.mp4"));
            Assert.InRange(MediaProbe.DurationSeconds(report), 1.8, 2.5);
        }

        /// <summary>
        /// The trim maths that maps a requested [start, end] onto playlist parts: the part range to download, the offset
        /// into the first part and the duration of the last part, and the resulting video length (used for the ffmpeg -t
        /// argument, chapter offsets and progress). Safe mode must zero the in-part offsets. Negative trim times are clamped
        /// to 0 by the constructor.
        /// </summary>
        [Theory]
        [InlineData(false, 0, false, 0, VideoTrimMode.Exact, 0, 4, 40, 0, 0)]
        [InlineData(true, 15, false, 0, VideoTrimMode.Exact, 1, 4, 25, 5, 0)]
        [InlineData(true, 20, false, 0, VideoTrimMode.Exact, 2, 4, 20, 0, 0)]
        [InlineData(false, 0, true, 25, VideoTrimMode.Exact, 0, 3, 25, 0, 5)]
        [InlineData(false, 0, true, 30, VideoTrimMode.Exact, 0, 3, 30, 0, 0)]
        [InlineData(true, 12, true, 33, VideoTrimMode.Exact, 1, 4, 21, 2, 3)]
        [InlineData(true, 12, true, 33, VideoTrimMode.Safe, 1, 4, 30, 0, 0)]
        [InlineData(true, -5, false, 0, VideoTrimMode.Exact, 0, 4, 40, 0, 0)]
        public void GetStreamListTrim_MapsTrimOntoParts(bool trimStart, double start, bool trimEnd, double end, VideoTrimMode mode,
            int expectedStartIndex, int expectedEndIndex, double expectedLength, double expectedStartOffset, double expectedEndDuration)
        {
            using var temp = new TempDirectory();
            var downloader = new VideoDownloader(new VideoDownloadOptions
            {
                Id = FakeTwitch.VIDEO_ID, TempFolder = temp.Path, TrimMode = mode,
                TrimBeginning = trimStart, TrimBeginningTime = TimeSpan.FromSeconds(start),
                TrimEnding = trimEnd, TrimEndingTime = TimeSpan.FromSeconds(end)
            }, new RecordingTaskProgress());

            var range = GetStreamListTrim(downloader, Parts(10, 10, 10, 10), out var length, out var startOffset, out var endDuration);

            Assert.Equal(new Range(expectedStartIndex, expectedEndIndex), range);
            Assert.Equal(TimeSpan.FromSeconds(expectedLength), length);
            Assert.Equal((decimal)expectedStartOffset, startOffset);
            Assert.Equal((decimal)expectedEndDuration, endDuration);
        }

        /// <summary>
        /// fMP4 VODs (AV1/HEVC) declare an EXT-X-MAP init segment: it is downloaded once and prepended to every part so each
        /// is a standalone mp4 for the concat step, the AV1 experimental warning is shown, and the result is playable.
        /// </summary>
        [Fact]
        public async Task Fmp4Vod_WithInitSegment_EndToEnd()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            var master = FakeTwitch.MasterPlaylist(sourceCodecs: "av01.0.08M.08,mp4a.40.2");
            twitch.Intercept = (req, _) => req.RequestUri!.Host == "usher.ttvnw.net" ? FakeHttpMessageHandler.Text(master) : null;
            twitch.Files[$"{FakeTwitch.VOD_BASE}chunked/index-dvr.m3u8"] = Encoding.UTF8.GetBytes(FakeTwitch.MediaPlaylist(["0.mp4", "1.mp4"], mapUri: "init-0.mp4"));
            foreach (var name in new[] { "init-0.mp4", "0.mp4", "1.mp4" })
            {
                twitch.Files[$"{FakeTwitch.VOD_BASE}chunked/{name}"] = await File.ReadAllBytesAsync(TestData.Path("Downloaders", "vod_fmp4", name), TestContext.Current.CancellationToken);
            }

            var progress = new RecordingTaskProgress();
            using var _ = FakeGql(twitch);

            await Create(twitch, temp, progress).DownloadAsync(CancellationToken.None);

            Assert.Single(twitch.RequestsTo("init-0.mp4"));
            Assert.Contains(progress.Warnings, x => x.StartsWith("AV1 VOD support is still experimental"));
            var (_, report) = MediaProbe.Probe(temp.Combine("out", "vod.mp4"));
            Assert.InRange(MediaProbe.DurationSeconds(report), 1.8, 2.5);
            Assert.Contains("Video: h264", report);
        }

        /// <summary>
        /// A connection error on one part faults its worker (logged as "1 error was encountered"), the verification pass
        /// finds the part missing and re-downloads it, and the final video is still complete.
        /// </summary>
        [Fact]
        public async Task FailedPart_IsRedownloadedDuringVerification()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            twitch.AddTsVod();
            var failures = 0;
            twitch.Intercept = (req, _) => req.RequestUri!.AbsolutePath.EndsWith("/1.ts") && Interlocked.Increment(ref failures) == 1
                ? throw new IOException("Connection reset by peer")
                : null;
            var progress = new RecordingTaskProgress();
            using var _ = FakeGql(twitch);

            await Create(twitch, temp, progress).DownloadAsync(CancellationToken.None);

            Assert.Contains(progress.Info, x => x.StartsWith("1 error was encountered while downloading: Connection reset by peer"));
            Assert.Contains(progress.Info, x => x == "1 parts were missing or corrupt and will be redownloaded.");
            Assert.Contains(progress.Info, x => x == "Successfully redownloaded missing parts");
            var (_, report) = MediaProbe.Probe(temp.Combine("out", "vod.mp4"));
            Assert.InRange(MediaProbe.DurationSeconds(report), 2.8, 3.5);
        }

        /// <summary>
        /// A part that can never be downloaded is replaced by a tiny valid TS stub so ffmpeg can still finalize the rest of
        /// the VOD, and the user is warned. Guards against one dead segment failing a multi-hour download.
        /// </summary>
        [Fact]
        public async Task PermanentlyFailingPart_IsReplacedWithStub()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            twitch.AddTsVod();
            twitch.Intercept = (req, _) => req.RequestUri!.AbsolutePath.EndsWith("/1.ts") ? throw new IOException("Connection reset by peer") : null;
            var progress = new RecordingTaskProgress();
            using var _ = FakeGql(twitch);

            await Create(twitch, temp, progress).DownloadAsync(CancellationToken.None);

            Assert.Contains(progress.Warnings, x => x == "Failed to redownload 1 parts, using stubs instead");
            var (_, report) = MediaProbe.Probe(temp.Combine("out", "vod.mp4"));
            Assert.Contains("Video: h264", report);
            Assert.InRange(MediaProbe.DurationSeconds(report), 1.8, 3.5);
        }

        /// <summary>
        /// If ffmpeg cannot finalize (here: garbage segments), it is retried once with audio re-encoding allowed, then the
        /// download fails with a message pointing at the download cache, which is deliberately kept (with ffmpegLog.txt) so
        /// the user can recover or report the VOD.
        /// </summary>
        [Fact]
        public async Task FfmpegFailure_RetriesOnce_ThenKeepsCacheForDebugging()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            twitch.AddTsVod();
            foreach (var part in new[] { "0.ts", "1.ts", "2.ts" }) twitch.Files[$"{FakeTwitch.VOD_BASE}chunked/{part}"] = new byte[376];
            var progress = new RecordingTaskProgress();
            using var _ = FakeGql(twitch);

            var ex = await Assert.ThrowsAsync<Exception>(() => Create(twitch, temp, progress).DownloadAsync(CancellationToken.None));

            Assert.StartsWith("Failed to finalize video.", ex.Message);
            Assert.Contains(progress.Errors, x => x.StartsWith("Failed to finalize video (code"));
            Assert.Contains(progress.Verbose, x => x == "Running with audio copy disabled.");
            var cache = Assert.Single(VodCacheDirs(temp));
            Assert.True(File.Exists(Path.Combine(cache, "ffmpegLog.txt")));
            Assert.False(File.Exists(temp.Combine("out", "vod.mp4")));
        }

        /// <summary>
        /// ffmpeg is run at most twice, so only the first failure may announce a retry. Currently the second (final)
        /// failure also logs "retrying in 5 seconds..." and sleeps 5s before giving up, misleading the user and delaying the error.
        /// </summary>
        [Fact(Skip = "Bug: VideoDownloader logs 'retrying in 5 seconds' and waits 5s after the final ffmpeg attempt too, although no retry follows")]
        public async Task FfmpegFailure_OnlyAnnouncesRetryThatHappens()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            twitch.AddTsVod();
            foreach (var part in new[] { "0.ts", "1.ts", "2.ts" }) twitch.Files[$"{FakeTwitch.VOD_BASE}chunked/{part}"] = new byte[376];
            var progress = new RecordingTaskProgress();
            using var _ = FakeGql(twitch);

            await Assert.ThrowsAsync<Exception>(() => Create(twitch, temp, progress).DownloadAsync(CancellationToken.None));

            Assert.Single(progress.Errors, x => x.Contains("retrying in 5 seconds"));
        }

        /// <summary>
        /// Without a usable ffmpeg executable the whole download still runs, then fails at finalization; the empty output
        /// file and the download cache are both removed. Runs without ffmpeg installed.
        /// </summary>
        [Fact]
        public async Task MissingFfmpeg_FailsAtFinalization_AndCleansUp()
        {
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            twitch.AddTsVod();
            var progress = new RecordingTaskProgress();
            using var _ = FakeGql(twitch);
            var downloader = Create(twitch, temp, progress, o => o.FfmpegPath = temp.Combine("no-such-ffmpeg"));

            await Assert.ThrowsAsync<Win32Exception>(() => downloader.DownloadAsync(CancellationToken.None));

            Assert.Equal(3, twitch.RequestsTo("/chunked/").Count(r => r.Uri.AbsolutePath.EndsWith(".ts")));
            Assert.False(File.Exists(temp.Combine("out", "vod.mp4")));
            Assert.Empty(VodCacheDirs(temp));
            Assert.Contains("Verifying Parts {0}% [3/4]", progress.Statuses);
        }

        /// <summary>A deleted/expired VOD (video: null) fails with a clear message and leaves no empty output file.</summary>
        [Fact]
        public async Task DeletedVod_Throws_AndRemovesOutput()
        {
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch { Video = null };
            using var _ = FakeGql(twitch);

            var ex = await Assert.ThrowsAsync<NullReferenceException>(() => Create(twitch, temp, new RecordingTaskProgress()).DownloadAsync(CancellationToken.None));

            Assert.Equal("Invalid VOD, deleted/expired VOD possibly?", ex.Message);
            Assert.False(File.Exists(temp.Combine("out", "vod.mp4")));
        }

        /// <summary>A null playback access token (VOD not accessible) is reported as an invalid VOD instead of crashing later.</summary>
        [Fact]
        public async Task MissingAccessToken_Throws()
        {
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch { Token = null };
            using var _ = FakeGql(twitch);

            var ex = await Assert.ThrowsAsync<NullReferenceException>(() => Create(twitch, temp, new RecordingTaskProgress()).DownloadAsync(CancellationToken.None));

            Assert.Equal("Invalid VOD, deleted/expired VOD possibly?", ex.Message);
        }

        /// <summary>
        /// Sub-only VODs make usher answer 403 with "vod_manifest_restricted"; the user gets an actionable "OAuth may be
        /// required" error instead of a generic HTTP failure, and no segment is requested.
        /// </summary>
        [Fact]
        public async Task SubOnlyVod_ReportsOauthRequired()
        {
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch { UsherOverride = (HttpStatusCode.Forbidden, "[{\"error\":\"Unauthorized\",\"error_code\":\"vod_manifest_restricted\",\"type\":\"error\"}]") };
            using var _ = FakeGql(twitch);

            var ex = await Assert.ThrowsAsync<NullReferenceException>(() => Create(twitch, temp, new RecordingTaskProgress()).DownloadAsync(CancellationToken.None));

            Assert.Equal("Insufficient access to VOD, OAuth may be required.", ex.Message);
            Assert.Empty(twitch.RequestsTo("vod.test"));
            Assert.False(File.Exists(temp.Combine("out", "vod.mp4")));
        }

        /// <summary>
        /// Cancelling while parts are downloading stops the job with OperationCanceledException, and both the partial
        /// download cache and the empty output file are removed.
        /// </summary>
        [Fact]
        public async Task CancelDuringPartDownload_StopsAndCleansUp()
        {
            using var temp = new TempDirectory();
            using var cts = new CancellationTokenSource();
            var twitch = new FakeTwitch();
            twitch.AddTsVod();
            twitch.Intercept = (req, _) =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith(".ts")) cts.Cancel();
                return null;
            };
            using var _ = FakeGql(twitch);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(twitch, temp, new RecordingTaskProgress(), o => o.DownloadThreads = 1).DownloadAsync(cts.Token));

            Assert.Empty(VodCacheDirs(temp));
            Assert.False(File.Exists(temp.Combine("out", "vod.mp4")));
        }

        /// <summary>
        /// Old highlights reference "-muted-XXXX" playlists that now 403; the downloader retries the same URL without the
        /// muted suffix (logging an error). The playlist's #ID3-EQUIV-TDTG tag provides the air date used for un-muting.
        /// </summary>
        [Fact]
        public async Task MutedHighlightPlaylist_FallsBackToUnmutedUrl()
        {
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            var unmutedUrl = $"{FakeTwitch.VOD_BASE}chunked/highlight-{FakeTwitch.VIDEO_ID}.m3u8";
            twitch.Files[unmutedUrl] = Encoding.UTF8.GetBytes(FakeTwitch.MediaPlaylist(["0.ts"], airDate: new DateTimeOffset(2023, 9, 23, 17, 37, 6, TimeSpan.Zero)));
            twitch.Intercept = (req, _) => req.RequestUri!.AbsolutePath.Contains("-muted-") ? new HttpResponseMessage(HttpStatusCode.Forbidden) : null;
            var progress = new RecordingTaskProgress();
            var downloader = Create(twitch, temp, progress);

            var (playlist, airDate) = await GetVideoPlaylist(downloader, $"{FakeTwitch.VOD_BASE}chunked/highlight-{FakeTwitch.VIDEO_ID}-muted-ABCDEF1234.m3u8", CancellationToken.None);

            Assert.Equal("0.ts", Assert.Single(playlist.Streams).Path);
            Assert.Equal(new DateTime(2023, 9, 23, 17, 37, 6), airDate.DateTime);
            Assert.Contains(progress.Errors, x => x.StartsWith("Received 403: Forbidden when fetching playlist"));
        }

        /// <summary>A non-highlight playlist that fails is not retried: the HTTP error propagates unchanged.</summary>
        [Fact]
        public async Task FailingRegularPlaylist_IsNotRetried()
        {
            using var temp = new TempDirectory();
            var twitch = new FakeTwitch();
            var downloader = Create(twitch, temp, new RecordingTaskProgress());

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => GetVideoPlaylist(downloader, $"{FakeTwitch.VOD_BASE}chunked/index-dvr.m3u8", CancellationToken.None));

            Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
            Assert.Single(twitch.Handler.Requests);
        }

        /// <summary>
        /// When (almost) every part of a large VOD is missing after downloading (over 20 parts, at least 95% invalid),
        /// verification aborts instead of emitting hundreds of stubs into a useless video.
        /// </summary>
        [Fact]
        public async Task Verification_AbortsWhenNearlyAllPartsAreMissing()
        {
            using var temp = new TempDirectory();
            var downloader = Create(new FakeTwitch(), temp, new RecordingTaskProgress());
            var parts = Enumerable.Range(0, 21).Select(i => new M3U8.Stream(new M3U8.Stream.ExtPartInfo(1, false), default, default, $"{i}.ts")).ToArray();
            var state = new VideoDownloadState(["chunked"], parts, Range.All, new Uri($"{FakeTwitch.VOD_BASE}chunked/"), null, DateTimeOffset.UtcNow.AddDays(-30));

            var ex = await Assert.ThrowsAsync<Exception>(() => VerifyDownloadedParts(downloader, state, CancellationToken.None));

            Assert.Equal("Too many parts are missing or corrupt (21/21), aborting.", ex.Message);
        }
    }
}
