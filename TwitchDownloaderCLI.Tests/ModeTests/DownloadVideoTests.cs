using System.Runtime.CompilerServices;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.ModeTests
{
    // Tests DownloadVideo.GetDownloadOptions, which turns parsed `videodownload` arguments into Core's VideoDownloadOptions: VOD id/URL
    // parsing, output extension -> quality inference, trim flags, ffmpeg path resolution and the cache/collision callbacks.
    // Download() is only tested for the failure path (Twitch faked to 404); the null/unparsable-id and missing-ffmpeg branches call
    // Environment.Exit, so they are not unit tested. In the static-HttpClient collection because that test swaps TwitchHelper.httpClient.
    [Collection(StaticHttpClientCollection.NAME)]
    public class DownloadVideoTests
    {
        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "GetDownloadOptions")]
        private static extern VideoDownloadOptions GetDownloadOptions(
            [UnsafeAccessorType("TwitchDownloaderCLI.Modes.DownloadVideo, TwitchDownloaderCLI")] object? @class,
            VideoDownloadArgs inputOptions, FileCollisionHandler collisionHandler, ITaskLogger logger);

        private static VideoDownloadOptions Map(VideoDownloadArgs args, ITaskLogger? logger = null) =>
            GetDownloadOptions(null, args, new FileCollisionHandler(args, new RecordingTaskProgress()), logger ?? new RecordingTaskProgress());

        // All plain options must be copied to the Core options unchanged, and a VOD URL must be reduced to its numeric id.
        [Fact]
        public void MapsAllOptions()
        {
            var args = CliArgs.Parse<VideoDownloadArgs>(
                "videodownload", "-u", "https://www.twitch.tv/videos/123456789", "-o", "vod.mp4", "-q", "720p60", "-t", "6", "--bandwidth", "1024",
                "--trim-mode", "Safe", "--oauth", "secret", "--temp-path", "cache");

            var options = Map(args);

            Assert.Equal(123456789, options.Id);
            Assert.Equal("vod.mp4", options.Filename);
            Assert.Equal("720p60", options.Quality);
            Assert.Equal(6, options.DownloadThreads);
            Assert.Equal(1024, options.ThrottleKib);
            Assert.Equal(VideoTrimMode.Safe, options.TrimMode);
            Assert.Equal("secret", options.Oauth);
            Assert.Equal("cache", options.TempFolder);
        }

        // The output extension decides the download type: .mp4 keeps the requested quality, .m4a (any case) forces the audio-only stream.
        // When the output has no extension, it is inferred from the quality ("audio" -> .m4a, anything else -> .mp4).
        [Theory]
        [InlineData("vod.mp4", "1080p60", "vod.mp4", "1080p60")]
        [InlineData("vod.MP4", null, "vod.MP4", null)]
        [InlineData("vod.m4a", "1080p60", "vod.m4a", "Audio")]
        [InlineData("vod.M4A", null, "vod.M4A", "Audio")]
        [InlineData("vod", "Audio Only", "vod.m4a", "Audio")]
        [InlineData("vod", "720p30", "vod.mp4", "720p30")]
        public void InfersQualityAndExtension(string output, string? quality, string expectedFilename, string? expectedQuality)
        {
            var argv = new List<string> { "videodownload", "--id", "123", "-o", output };
            if (quality is not null) argv.AddRange(["-q", quality]);

            var options = Map(CliArgs.Parse<VideoDownloadArgs>(argv.ToArray()));

            Assert.Equal(expectedFilename, options.Filename);
            Assert.Equal(expectedQuality, options.Quality);
        }

        // Anything other than .mp4/.m4a (including no extension and no quality to infer one from) must be rejected up front rather than
        // producing a file ffmpeg can't mux.
        [Theory]
        [InlineData("vod.mkv")]
        [InlineData("vod")]
        public void UnsupportedExtension_Throws(string output)
        {
            var args = CliArgs.Parse<VideoDownloadArgs>("videodownload", "--id", "123", "-o", output);

            var ex = Assert.Throws<ArgumentException>(() => Map(args));
            Assert.Contains("Only MP4 and M4A", ex.Message);
        }

        // Trimming is only enabled for strictly positive times; an omitted (zero) trim must leave the whole VOD untrimmed.
        [Fact]
        public void TrimIsEnabledOnlyForPositiveTimes()
        {
            var untrimmed = Map(CliArgs.Parse<VideoDownloadArgs>("videodownload", "--id", "123", "-o", "a.mp4"));
            var trimmed = Map(CliArgs.Parse<VideoDownloadArgs>("videodownload", "--id", "123", "-o", "a.mp4", "-b", "10s", "-e", "1:00"));

            Assert.False(untrimmed.TrimBeginning);
            Assert.False(untrimmed.TrimEnding);
            Assert.True(trimmed.TrimBeginning);
            Assert.Equal(TimeSpan.FromSeconds(10), trimmed.TrimBeginningTime);
            Assert.True(trimmed.TrimEnding);
            Assert.Equal(TimeSpan.FromMinutes(1), trimmed.TrimEndingTime);
        }

        // Without --ffmpeg-path the bare executable name is used (resolved from the working directory/PATH); with it, the path is made
        // absolute so it still works after Core changes directories.
        [Fact]
        public void ResolvesFfmpegPath()
        {
            var defaultPath = Map(CliArgs.Parse<VideoDownloadArgs>("videodownload", "--id", "123", "-o", "a.mp4"));
            var customPath = Map(CliArgs.Parse<VideoDownloadArgs>("videodownload", "--id", "123", "-o", "a.mp4", "--ffmpeg-path", Path.Combine("tools", "ffmpeg")));

            Assert.Equal(FfmpegHandler.FfmpegExecutableName, defaultPath.FfmpegPath);
            Assert.Equal(Path.GetFullPath(Path.Combine("tools", "ffmpeg")), customPath.FfmpegPath);
        }

        // The cache cleaner callback must never delete anything on its own (the CLI has no prompt there); it only tells the user where
        // the unmanaged caches are and points them at the `cache` verb.
        [Fact]
        public void CacheCleanerCallback_OnlyReportsCaches()
        {
            using var temp = new TempDirectory();
            var logger = new RecordingTaskProgress();
            var options = Map(CliArgs.Parse<VideoDownloadArgs>("videodownload", "--id", "123", "-o", "a.mp4", "--temp-path", "fallback"), logger);
            var caches = new[] { Directory.CreateDirectory(temp.Combine("a")), Directory.CreateDirectory(temp.Combine("b")) };

            Assert.Empty(options.CacheCleanerCallback(caches));
            Assert.Empty(options.CacheCleanerCallback([]));

            Assert.Equal(2, logger.Info.Count);
            Assert.StartsWith($"2 unmanaged video caches were found at '{temp.Path}'", logger.Info.First());
            Assert.StartsWith("0 unmanaged video caches were found at 'fallback'", logger.Info.Last());
            Assert.All(logger.Info, msg => Assert.Contains("cache help", msg));
        }

        // The collision callback must be the handler built from the parsed --collision behaviour, so Core asks the CLI before overwriting.
        [Fact]
        public void UsesCollisionHandler()
        {
            var args = CliArgs.Parse<VideoDownloadArgs>("videodownload", "--id", "123", "-o", "a.mp4", "--collision", "Overwrite");
            var handler = new FileCollisionHandler(args, new RecordingTaskProgress());

            var options = GetDownloadOptions(null, args, handler, new RecordingTaskProgress());

            Assert.Same(handler, options.FileCollisionCallback.Target);
            Assert.Equal(OverwriteBehavior.Overwrite, args.OverwriteBehavior);
        }

        // The verb entry point hands the parsed VOD id to Core and lets a Twitch API failure propagate (so the process exits non-zero),
        // without leaving a half-written output file behind.
        [Fact]
        public void Download_TwitchFailure_PropagatesAndLeavesNoOutput()
        {
            using var temp = new TempDirectory();
            var ffmpeg = temp.Combine(FfmpegHandler.FfmpegExecutableName);
            File.WriteAllText(ffmpeg, "");
            var output = temp.Combine("vod.mp4");
            var http = FakeHttpMessageHandler.NotFound();
            using var httpOverride = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, http);

            var ex = Assert.Throws<AggregateException>(() => DownloadVideo.Download(CliArgs.Parse<VideoDownloadArgs>(
                "videodownload", "--id", "https://twitch.tv/videos/123456789", "-o", output, "--ffmpeg-path", ffmpeg, "--temp-path", temp.Path,
                "--collision", "Overwrite", "--log-level", "None")));

            Assert.IsType<HttpRequestException>(ex.InnerException);
            Assert.Contains(http.Requests, r => r.Body!.Contains("video(id:\\\"123456789\\\")"));
            Assert.False(File.Exists(output));
        }
    }
}
