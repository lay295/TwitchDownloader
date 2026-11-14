using CommandLine;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;

namespace TwitchDownloaderCLI.Tests.ArgumentTests
{
    // Tests the option declarations of the `clipdownload` verb (ClipDownloadArgs), the CLI's public contract for downloading clips.
    public class ClipDownloadArgsTests
    {
        // With only the required options, the rest must take their documented defaults. In particular --encode-metadata must default
        // to true (DownloadClip dereferences it with `!.Value`, so a null default would crash).
        [Fact]
        public void UsesDocumentedDefaults()
        {
            var args = CliArgs.Parse<ClipDownloadArgs>("clipdownload", "--id", "SomeClipSlug", "-o", "clip.mp4");

            Assert.Equal("SomeClipSlug", args.Id);
            Assert.Equal("clip.mp4", args.OutputFile);
            Assert.Null(args.Quality);
            Assert.Equal(-1, args.ThrottleKib);
            Assert.True(args.EncodeMetadata);
            Assert.Null(args.FfmpegPath);
            Assert.Equal("", args.TempFolder);
            Assert.Equal(OverwriteBehavior.Prompt, args.OverwriteBehavior);
            Assert.True(args.ShowBanner);
            Assert.Equal(LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, args.LogLevel);
        }

        // Every option must land in its property, including the nullable bool --encode-metadata which takes an explicit value.
        [Fact]
        public void ParsesEveryOption()
        {
            var args = CliArgs.Parse<ClipDownloadArgs>(
                "clipdownload", "-u", "https://clips.twitch.tv/SomeClipSlug", "-o", "clip.mp4", "-q", "720p60", "--bandwidth", "100",
                "--encode-metadata", "false", "--ffmpeg-path", "ff", "--temp-path", "tmp", "--collision", "Overwrite", "--banner", "false",
                "--log-level", "None");

            Assert.Equal("https://clips.twitch.tv/SomeClipSlug", args.Id);
            Assert.Equal("720p60", args.Quality);
            Assert.Equal(100, args.ThrottleKib);
            Assert.False(args.EncodeMetadata);
            Assert.Equal("ff", args.FfmpegPath);
            Assert.Equal("tmp", args.TempFolder);
            Assert.Equal(OverwriteBehavior.Overwrite, args.OverwriteBehavior);
            Assert.False(args.ShowBanner);
            Assert.Equal(LogLevel.None, args.LogLevel);
        }

        // --id and --output are required.
        [Theory]
        [InlineData("clipdownload", "-o", "clip.mp4")]
        [InlineData("clipdownload", "--id", "SomeClipSlug")]
        public void MissingRequiredOption_IsAnError(params string[] argv)
        {
            Assert.Contains(ErrorType.MissingRequiredOptionError, CliArgs.Errors(argv));
        }
    }
}
