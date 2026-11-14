using System.ComponentModel;
using System.Net;
using System.Web;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCore.Tests.DownloaderTests
{
    // Tests ClipDownloader: ShareClipRenderStatus GQL -> quality selection -> signed CDN download (optionally throttled)
    // -> optional ffmpeg metadata remux. Clips are a single file, so a wrong URL/signature, quality or clean-up is very visible.
    [Collection(StaticHttpClientCollection.NAME)]
    public class ClipDownloaderTests
    {
        private static readonly byte[] ClipBytes = File.ReadAllBytes(TestData.Path("Downloaders", "clip.mp4"));

        private sealed class Fakes : IDisposable
        {
            public readonly FakeTwitch Twitch = new();
            private readonly HttpClientOverride _gql;
            private readonly HttpClientOverride _cdn;

            public Fakes()
            {
                Twitch.Files["https://clips.test/1080.mp4"] = ClipBytes;
                Twitch.Files["https://clips.test/720.mp4"] = ClipBytes[..^10];
                _gql = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, Twitch.Handler);
                _cdn = new HttpClientOverride(c => ClipDownloader.HttpClient = c, () => ClipDownloader.HttpClient, Twitch.Handler);
            }

            public void Dispose()
            {
                _cdn.Dispose();
                _gql.Dispose();
            }
        }

        private static ClipDownloadOptions Options(TempDirectory temp, string quality = "1080p60", bool metadata = false) => new()
        {
            Id = FakeTwitch.CLIP_ID,
            Quality = quality,
            Filename = temp.Combine("out", "clip.mp4"),
            ThrottleKib = -1,
            TempFolder = temp.Combine("temp"),
            EncodeMetadata = metadata,
            FfmpegPath = TestData.FfmpegPath ?? "ffmpeg"
        };

        private static string[] CacheFiles(TempDirectory temp) =>
            Directory.Exists(temp.Combine("temp")) ? Directory.GetFiles(temp.Combine("temp"), "*", SearchOption.AllDirectories) : [];

        /// <summary>
        /// Without metadata the clip is streamed straight to the output byte-for-byte, the CDN URL carries the playback
        /// signature and the URL-encoded token (an unsigned/garbled URL is a 403 from Twitch), and no temp files are left.
        /// </summary>
        [Fact]
        public async Task DownloadsClip_ByteForByte_WithSignedUrl()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();

            await new ClipDownloader(Options(temp), new RecordingTaskProgress()).DownloadAsync(CancellationToken.None);

            Assert.Equal(ClipBytes, await File.ReadAllBytesAsync(temp.Combine("out", "clip.mp4"), TestContext.Current.CancellationToken));
            var query = HttpUtility.ParseQueryString(Assert.Single(fakes.Twitch.RequestsTo("clips.test")).Uri.Query);
            Assert.Equal("clipsig", query["sig"]);
            Assert.Equal("{\"clip\":\"a b\"}", query["token"]);
            Assert.Empty(CacheFiles(temp));
        }

        /// <summary>
        /// The quality string selects the matching rendition; empty or unknown strings fall back to the best (source)
        /// quality instead of failing the download.
        /// </summary>
        [Theory]
        [InlineData("720p", "https://clips.test/720.mp4")]
        [InlineData("720p30", "https://clips.test/720.mp4")]
        [InlineData("1080p60", "https://clips.test/1080.mp4")]
        [InlineData("", "https://clips.test/1080.mp4")]
        [InlineData("Worst", "https://clips.test/720.mp4")]
        [InlineData("4320p999", "https://clips.test/1080.mp4")]
        public async Task Quality_SelectsRendition(string quality, string expectedUrl)
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();

            await new ClipDownloader(Options(temp, quality), new RecordingTaskProgress()).DownloadAsync(CancellationToken.None);

            Assert.Equal(expectedUrl, Assert.Single(fakes.Twitch.RequestsTo("clips.test")).Uri.GetLeftPart(UriPartial.Path));
        }

        /// <summary>The throttled path (ThrottleKib &gt; 0) must produce the identical file.</summary>
        [Fact]
        public async Task ThrottledDownload_IsIdentical()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var options = Options(temp);
            options.ThrottleKib = 10_000;

            await new ClipDownloader(options, new RecordingTaskProgress()).DownloadAsync(CancellationToken.None);

            Assert.Equal(ClipBytes, await File.ReadAllBytesAsync(options.Filename, TestContext.Current.CancellationToken));
        }

        /// <summary>
        /// With EncodeMetadata the clip is downloaded to the cache, remuxed by ffmpeg with title/streamer/clipper metadata,
        /// and the temporary clip and metadata files are deleted afterwards.
        /// </summary>
        [Fact]
        public async Task EncodeMetadata_WritesClipMetadata_AndCleansTempFiles()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            using var _ = new Fakes();

            await new ClipDownloader(Options(temp, metadata: true), new RecordingTaskProgress()).DownloadAsync(CancellationToken.None);

            var (metadata, report) = MediaProbe.Probe(temp.Combine("out", "clip.mp4"));
            Assert.Contains($"title=Fake Clip ({FakeTwitch.CLIP_ID})", metadata);
            Assert.Contains("artist=FakeStreamer", metadata);
            Assert.Contains("Clipped by: FakeClipper", metadata);
            Assert.Contains("Video: h264", report);
            Assert.Empty(CacheFiles(temp));
        }

        /// <summary>
        /// If ffmpeg cannot remux (here: the CDN served a non-mp4), the downloaded clip should still be delivered without
        /// metadata, as the error message promises. Currently the fallback File.Move throws because the claimed (0 byte)
        /// output file already exists, the downloaded clip is deleted and the whole download fails.
        /// </summary>
        [Fact(Skip = "Bug: ClipDownloader metadata fallback calls File.Move(temp, output) while the empty claimed output still exists, so it throws IOException and the clip is lost")]
        public async Task EncodeMetadata_FfmpegFailure_FallsBackToPlainClip()
        {
            TestData.RequireFfmpeg();
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            byte[] notAnMp4 = [.. Enumerable.Range(0, 3000).Select(i => (byte)(i * 7))];
            fakes.Twitch.Files["https://clips.test/1080.mp4"] = notAnMp4;
            var progress = new RecordingTaskProgress();

            await new ClipDownloader(Options(temp, metadata: true), progress).DownloadAsync(CancellationToken.None);

            Assert.Equal(notAnMp4, await File.ReadAllBytesAsync(temp.Combine("out", "clip.mp4"), TestContext.Current.CancellationToken));
            Assert.Contains(progress.Errors, x => x.StartsWith("Unable to serialize metadata"));
        }

        /// <summary>
        /// A missing ffmpeg executable fails the metadata step; the temporary clip download and the empty output are both
        /// removed so no half-finished video is left behind. Runs without ffmpeg installed.
        /// </summary>
        [Fact]
        public async Task EncodeMetadata_MissingFfmpeg_Fails_AndRemovesVideoFiles()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var options = Options(temp, metadata: true);
            options.FfmpegPath = temp.Combine("no-such-ffmpeg");

            await Assert.ThrowsAnyAsync<Exception>(() => new ClipDownloader(options, new RecordingTaskProgress()).DownloadAsync(CancellationToken.None));

            Assert.False(File.Exists(options.Filename));
            Assert.DoesNotContain(CacheFiles(temp), f => f.EndsWith(".mp4"));
        }

        /// <summary>
        /// When ffmpeg cannot be started, the user should see the real cause (Win32Exception: file not found) and the
        /// temporary ffmpeg metadata file should be deleted. Currently the finally block reads Process.HasExited on a process
        /// that never started, which throws InvalidOperationException ("No process is associated with this object"), hides
        /// the real error and skips the metadata file clean-up.
        /// </summary>
        [Fact(Skip = "Bug: ClipDownloader.EncodeClipWithMetadata reads HasExited on a never-started Process in finally, masking the start failure with InvalidOperationException and leaking the _metadata.txt file")]
        public async Task EncodeMetadata_MissingFfmpeg_ReportsStartFailure_AndRemovesMetadataFile()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var options = Options(temp, metadata: true);
            options.FfmpegPath = temp.Combine("no-such-ffmpeg");

            await Assert.ThrowsAsync<Win32Exception>(() => new ClipDownloader(options, new RecordingTaskProgress()).DownloadAsync(CancellationToken.None));

            Assert.Empty(CacheFiles(temp));
        }

        /// <summary>A clip without a playback access token (deleted) fails with a clear message and leaves no output.</summary>
        [Fact]
        public async Task DeletedClip_Throws_AndRemovesOutput()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Clip!.playbackAccessToken = null;

            var ex = await Assert.ThrowsAsync<NullReferenceException>(() => new ClipDownloader(Options(temp), new RecordingTaskProgress()).DownloadAsync(CancellationToken.None));

            Assert.Equal("Invalid Clip, deleted possibly?", ex.Message);
            Assert.False(File.Exists(temp.Combine("out", "clip.mp4")));
        }

        /// <summary>A clip whose assets carry no video qualities fails with a clear message instead of an index error.</summary>
        [Fact]
        public async Task ClipWithoutQualities_Throws()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Clip!.assets = [];

            var ex = await Assert.ThrowsAsync<NullReferenceException>(() => new ClipDownloader(Options(temp), new RecordingTaskProgress()).DownloadAsync(CancellationToken.None));

            Assert.Equal("Clip has no video qualities, deleted possibly?", ex.Message);
        }

        /// <summary>A CDN error (e.g. 403 for an expired signature) surfaces as HttpRequestException and the empty output is removed.</summary>
        [Fact]
        public async Task CdnError_Throws_AndRemovesOutput()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();
            fakes.Twitch.Intercept = (req, _) => req.RequestUri!.Host == "clips.test" ? new HttpResponseMessage(HttpStatusCode.Forbidden) : null;

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => new ClipDownloader(Options(temp), new RecordingTaskProgress()).DownloadAsync(CancellationToken.None));

            Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
            Assert.False(File.Exists(temp.Combine("out", "clip.mp4")));
        }

        /// <summary>Cancellation is honoured after fetching clip info: nothing is downloaded and no output is left behind.</summary>
        [Fact]
        public async Task Cancelled_Throws_WithoutDownloading()
        {
            using var temp = new TempDirectory();
            using var fakes = new Fakes();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ClipDownloader(Options(temp), new RecordingTaskProgress()).DownloadAsync(new CancellationToken(true)));

            Assert.Empty(fakes.Twitch.RequestsTo("clips.test"));
            Assert.False(File.Exists(temp.Combine("out", "clip.mp4")));
        }

        /// <summary>
        /// When the output already exists the collision callback picks the destination (logged as a rename) and the
        /// existing file is left untouched. Guards against silently overwriting a user's file.
        /// </summary>
        [Fact]
        public async Task FileCollisionCallback_RedirectsOutput()
        {
            using var temp = new TempDirectory();
            using var _ = new Fakes();
            var options = Options(temp);
            Directory.CreateDirectory(Path.GetDirectoryName(options.Filename)!);
            await File.WriteAllTextAsync(options.Filename, "existing", TestContext.Current.CancellationToken);
            var renamed = temp.Combine("out", "clip (1).mp4");
            options.FileCollisionCallback = _ => new FileInfo(renamed);
            var progress = new RecordingTaskProgress();

            await new ClipDownloader(options, progress).DownloadAsync(CancellationToken.None);

            Assert.Equal("existing", await File.ReadAllTextAsync(temp.Combine("out", "clip.mp4"), TestContext.Current.CancellationToken));
            Assert.Equal(ClipBytes, await File.ReadAllBytesAsync(renamed, TestContext.Current.CancellationToken));
            Assert.Equal(renamed, options.Filename);
            Assert.Contains(progress.Info, x => x.Contains("will be renamed to"));
        }
    }
}
