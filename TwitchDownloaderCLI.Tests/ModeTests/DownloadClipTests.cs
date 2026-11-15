using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.ModeTests
{
    // Tests DownloadClip.GetDownloadOptions, which turns parsed `clipdownload` arguments into Core's ClipDownloadOptions (clip slug parsing,
    // ffmpeg path resolution, metadata toggle). Download() is only tested for the failure path (Twitch faked to 404); the null/unparsable-id
    // branches call Environment.Exit. In the static-HttpClient collection because that test swaps TwitchHelper.httpClient.
    [Collection(StaticHttpClientCollection.NAME)]
    public class DownloadClipTests
    {
        private static ClipDownloadOptions Map(params string[] argv)
        {
            var args = CliArgs.Parse<ClipDownloadArgs>(argv);
            return DownloadClip.GetDownloadOptions(args, new FileCollisionHandler(args, new RecordingTaskProgress()), new RecordingTaskProgress());
        }

        // All options must be copied over, and both clip URL styles must be reduced to the bare slug Twitch's API expects.
        [Theory]
        [InlineData("https://clips.twitch.tv/FancyClipSlug-AbC123")]
        [InlineData("https://www.twitch.tv/streamer/clip/FancyClipSlug-AbC123?filter=clips")]
        [InlineData("FancyClipSlug-AbC123")]
        public void MapsAllOptions(string id)
        {
            var options = Map("clipdownload", "--id", id, "-o", "clip.mp4", "-q", "1080p60", "--bandwidth", "300", "--encode-metadata", "false", "--temp-path", "cache");

            Assert.Equal("FancyClipSlug-AbC123", options.Id);
            Assert.Equal("clip.mp4", options.Filename);
            Assert.Equal("1080p60", options.Quality);
            Assert.Equal(300, options.ThrottleKib);
            Assert.False(options.EncodeMetadata);
            Assert.Equal("cache", options.TempFolder);
        }

        // Metadata encoding is on by default (it is what makes the clip's title/date show up in media players).
        [Fact]
        public void EncodesMetadataByDefault()
        {
            Assert.True(Map("clipdownload", "--id", "Slug", "-o", "clip.mp4").EncodeMetadata);
        }

        // Without --ffmpeg-path the bare executable name is used; with it, the path is made absolute.
        [Fact]
        public void ResolvesFfmpegPath()
        {
            Assert.Equal(FfmpegHandler.FfmpegExecutableName, Map("clipdownload", "--id", "Slug", "-o", "clip.mp4").FfmpegPath);
            Assert.Equal(Path.GetFullPath("ffmpeg-custom"), Map("clipdownload", "--id", "Slug", "-o", "clip.mp4", "--ffmpeg-path", "ffmpeg-custom").FfmpegPath);
        }

        // The verb entry point checks for ffmpeg (needed for the default metadata encoding), hands the clip slug to Core and lets a Twitch
        // API failure propagate, without leaving an output file behind.
        [Fact]
        public void Download_TwitchFailure_PropagatesAndLeavesNoOutput()
        {
            using var temp = new TempDirectory();
            var ffmpeg = temp.Combine(FfmpegHandler.FfmpegExecutableName);
            File.WriteAllText(ffmpeg, "");
            var output = temp.Combine("clip.mp4");
            var http = FakeHttpMessageHandler.NotFound();
            using var httpOverride = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, http);

            var ex = Assert.Throws<AggregateException>(() => DownloadClip.Download(CliArgs.Parse<ClipDownloadArgs>(
                "clipdownload", "--id", "https://clips.twitch.tv/FancyClipSlug", "-o", output, "--ffmpeg-path", ffmpeg, "--collision", "Overwrite",
                "--log-level", "None")));

            Assert.IsType<HttpRequestException>(ex.InnerException);
            Assert.Contains(http.Requests, r => r.Body!.Contains("\"slug\":\"FancyClipSlug\""));
            Assert.False(File.Exists(output));
        }
    }
}
