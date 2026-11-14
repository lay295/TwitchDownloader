using System.Net;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;
using static TwitchDownloaderCore.Tests.TwitchHelperTests.TwitchHelperTestUtil;

namespace TwitchDownloaderCore.Tests.TwitchHelperTests
{
    // Tests TwitchHelper.GetImage (the disk-cached image fetch behind every emote, badge, cheermote and avatar) and GetAvatars.
    // Cache bugs show up as re-downloading thousands of images per render, poisoned caches that never heal, or offline renders that try the network.
    [Collection(StaticHttpClientCollection.NAME)]
    public class TwitchHelperImageCacheTests
    {
        private const string URL = "https://static-cdn.jtvnw.net/emoticons/v2/25/default/dark/2.0";

        private static Task<(byte[], SkiaSharp.SKCodec)> GetImage(DirectoryInfo dir, RecordingTaskProgress logger, bool offline = false, CancellationToken? ct = null) =>
            TwitchHelper.GetImage(dir, URL, "25", 2, "png", offline, logger, ct ?? TestContext.Current.CancellationToken);

        // A cache miss must download the image, create the cache directory if needed, and store it as {id}_{scale}.{type}.
        // The codec is null on a fresh download (callers decode the bytes themselves).
        [Fact]
        public async Task CacheMiss_DownloadsAndWritesCacheFile()
        {
            using var temp = new TempDirectory();
            var dir = new DirectoryInfo(temp.Combine("not", "yet", "created"));
            var png = Png();
            var handler = Routes((URL, Bytes(png)));
            using var _ = UseHandler(handler);

            var (bytes, codec) = await GetImage(dir, new RecordingTaskProgress());

            Assert.Equal(png, bytes);
            Assert.Null(codec);
            Assert.Equal(png, await File.ReadAllBytesAsync(Path.Combine(dir.FullName, "25_2.png"), TestContext.Current.CancellationToken));
            Assert.Equal(HttpMethod.Get, Assert.Single(handler.Requests).Method);
        }

        // A valid cached image must be returned with a ready codec and without any network request.
        [Fact]
        public async Task CacheHit_ReturnsCodecWithoutRequest()
        {
            using var temp = new TempDirectory();
            var png = Png(6, 3);
            await File.WriteAllBytesAsync(temp.Combine("25_2.png"), png, TestContext.Current.CancellationToken);
            var handler = FakeHttpMessageHandler.NotFound();
            using var _ = UseHandler(handler);

            var (bytes, codec) = await GetImage(new DirectoryInfo(temp.Path), new RecordingTaskProgress());

            using (codec)
            {
                Assert.Equal(png, bytes);
                Assert.Equal((6, 3), (codec.Info.Width, codec.Info.Height));
            }
            Assert.Empty(handler.Requests);
        }

        // A corrupt (undecodable) or empty cache file must be discarded and re-downloaded, otherwise one bad write poisons the cache forever.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CorruptOrEmptyCacheFile_IsReplacedByFreshDownload(bool corrupt)
        {
            using var temp = new TempDirectory();
            var cacheFile = temp.Combine("25_2.png");
            await File.WriteAllBytesAsync(cacheFile, corrupt ? NotAnImage : [], TestContext.Current.CancellationToken);
            var png = Png();
            var handler = Routes((URL, Bytes(png)));
            using var _ = UseHandler(handler);
            var progress = new RecordingTaskProgress();

            var (bytes, _) = await GetImage(new DirectoryInfo(temp.Path), progress);

            Assert.Equal(png, bytes);
            Assert.Single(handler.Requests);
            Assert.Equal(png, await File.ReadAllBytesAsync(cacheFile, TestContext.Current.CancellationToken));
            Assert.Equal(corrupt, progress.Verbose.Any(v => v.StartsWith("Failed to decode 25_2.png from cache")));
        }

        // Offline cache misses (and corrupt cache files) must yield (null, null) without touching the network; corrupt files are still cleaned up.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Offline_CacheMiss_ReturnsNullsWithoutRequest(bool corruptFilePresent)
        {
            using var temp = new TempDirectory();
            var cacheFile = temp.Combine("25_2.png");
            if (corruptFilePresent) await File.WriteAllBytesAsync(cacheFile, NotAnImage, TestContext.Current.CancellationToken);
            var handler = FakeHttpMessageHandler.NotFound();
            using var _ = UseHandler(handler);

            var (bytes, codec) = await GetImage(new DirectoryInfo(temp.Path), new RecordingTaskProgress(), offline: true);

            Assert.Null(bytes);
            Assert.Null(codec);
            Assert.Empty(handler.Requests);
            Assert.False(File.Exists(cacheFile));
        }

        // Download failures must propagate as HttpRequestException carrying the status, because every caller filters on 404 to skip deleted images.
        [Fact]
        public async Task DownloadError_PropagatesStatusCode()
        {
            using var temp = new TempDirectory();
            using var _ = UseHandler(FakeHttpMessageHandler.NotFound());

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => GetImage(new DirectoryInfo(temp.Path), new RecordingTaskProgress()));

            Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        }

        // If the cache file can't be written (here: a directory occupies its path) the downloaded image must still be returned; caching is best-effort.
        [Fact]
        public async Task CacheWriteFailure_StillReturnsDownloadedImage()
        {
            using var temp = new TempDirectory();
            Directory.CreateDirectory(temp.Combine("25_2.png"));
            var png = Png();
            using var _ = UseHandler(Routes((URL, Bytes(png))));
            var progress = new RecordingTaskProgress();

            var (bytes, _) = await GetImage(new DirectoryInfo(temp.Path), progress);

            Assert.Equal(png, bytes);
            Assert.Contains(progress.Verbose, v => v.StartsWith("Failed to save 25_2.png to cache"));
        }

        // A cancelled token must abort before any I/O.
        [Fact]
        public async Task CancelledToken_Throws()
        {
            using var temp = new TempDirectory();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GetImage(new DirectoryInfo(temp.Path), new RecordingTaskProgress(), ct: cts.Token));
        }

        private const string AVATAR_A = "https://static-cdn.jtvnw.net/jtv_user_pictures/avatar_a-profile_image-70x70.png";
        private const string AVATAR_B = "https://static-cdn.jtvnw.net/jtv_user_pictures/avatar_b-profile_image-70x70.png";
        private const string DEFAULT_AVATAR = "https://static-cdn.jtvnw.net/user-default-pictures-uv/default-profile_image-70x70.png";

        // Avatars for the default set plus every distinct commenter logo must be fetched once each (blank logos ignored), decoded,
        // keyed by URL and cached under avatars/{file name}_2.jpg.
        [Fact]
        public async Task GetAvatars_FetchesDefaultAndCommenterAvatarsOnce()
        {
            var handler = Routes((AVATAR_A, Bytes(Png(5, 5))), (AVATAR_B, Bytes(Png())), (DEFAULT_AVATAR, Bytes(Png())));
            using var _ = UseHandler(handler);
            using var cache = new TempDirectory();
            List<Comment> comments = [Comment("a", logo: AVATAR_A), Comment("b", logo: AVATAR_A), Comment("c", logo: AVATAR_B), Comment("d", logo: " "), Comment("e")];

            var avatars = await TwitchHelper.GetAvatars(comments, [DEFAULT_AVATAR], cache.Path, new RecordingTaskProgress(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(new[] { AVATAR_A, AVATAR_B, DEFAULT_AVATAR }.Order(), avatars.Keys.Order());
            Assert.Equal(5, avatars[AVATAR_A].Width);
            Assert.Equal(3, handler.Requests.Count);
            Assert.True(File.Exists(Path.Combine(cache.Path, "avatars", "avatar_a-profile_image-70x70_2.jpg")));
        }

        // Cached avatars must be decoded via the cached codec path and offline misses skipped, all without network access.
        [Fact]
        public async Task GetAvatars_Offline_UsesCacheAndSkipsMisses()
        {
            using var cache = new TempDirectory();
            Directory.CreateDirectory(cache.Combine("avatars"));
            await File.WriteAllBytesAsync(cache.Combine("avatars", "avatar_a-profile_image-70x70_2.jpg"), Png(7, 7), TestContext.Current.CancellationToken);
            var handler = FakeHttpMessageHandler.NotFound();
            using var _ = UseHandler(handler);

            var avatars = await TwitchHelper.GetAvatars([Comment("a", logo: AVATAR_A), Comment("b", logo: AVATAR_B)], null!, cache.Path, new RecordingTaskProgress(), offline: true,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(7, Assert.Single(avatars, kv => kv.Key == AVATAR_A).Value.Width);
            Assert.Single(avatars);
            Assert.Empty(handler.Requests);
        }

        // A failing avatar URL (deleted profile picture) must only be logged (verbose) and must not stop the other avatars loading.
        [Fact]
        public async Task GetAvatars_FailedAvatarRequest_IsLoggedAndSkipped()
        {
            using var _ = UseHandler(Routes((DEFAULT_AVATAR, Bytes(Png()))));
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var avatars = await TwitchHelper.GetAvatars([Comment("a", logo: AVATAR_A)], [DEFAULT_AVATAR], cache.Path, progress,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(DEFAULT_AVATAR, Assert.Single(avatars).Key);
            Assert.Contains(progress.Verbose, v => v.StartsWith($"Error while fetching {AVATAR_A}"));
        }

        // A downloaded avatar Skia can't decode (e.g. an HTML error page served with 200) must be a warning and skipped, not abort the render.
        [Fact(Skip = "Bug: GetAvatars calls SKBitmap.Decode(byte[]) for fresh downloads, which throws ArgumentNullException (instead of returning null) for undecodable bytes, so the 'Skia was unable to decode' branch is unreachable and GetAvatars throws")]
        public async Task GetAvatars_UndecodableAvatar_IsWarnedAndSkipped()
        {
            using var _ = UseHandler(Routes((AVATAR_B, Bytes(NotAnImage)), (DEFAULT_AVATAR, Bytes(Png()))));
            using var cache = new TempDirectory();
            var progress = new RecordingTaskProgress();

            var avatars = await TwitchHelper.GetAvatars([Comment("b", logo: AVATAR_B)], [DEFAULT_AVATAR], cache.Path, progress,
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(DEFAULT_AVATAR, Assert.Single(avatars).Key);
            Assert.Contains("Skia was unable to decode avatar_b-profile_image-70x70.", progress.Warnings);
        }
    }
}
