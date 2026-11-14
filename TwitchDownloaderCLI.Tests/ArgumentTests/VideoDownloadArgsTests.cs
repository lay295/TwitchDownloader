using CommandLine;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCLI.Tests.ArgumentTests
{
    // Tests the option declarations of the `videodownload` verb (VideoDownloadArgs). These attributes are the CLI's public contract:
    // a renamed flag, lost short alias or changed default silently breaks every script that calls the CLI.
    public class VideoDownloadArgsTests
    {
        // With only the required options, every other option must take its documented default (e.g. 4 threads, unlimited bandwidth,
        // exact trimming, prompt on collision). Guards against defaults drifting away from the help text/README.
        [Fact]
        public void UsesDocumentedDefaults()
        {
            var args = CliArgs.Parse<VideoDownloadArgs>("videodownload", "--id", "123456789", "-o", "out.mp4");

            Assert.Equal("123456789", args.Id);
            Assert.Equal("out.mp4", args.OutputFile);
            Assert.Null(args.Quality);
            Assert.Equal(TimeSpan.Zero, (TimeSpan)args.TrimBeginningTime);
            Assert.Equal(TimeSpan.Zero, (TimeSpan)args.TrimEndingTime);
            Assert.Equal(4, args.DownloadThreads);
            Assert.Equal(-1, args.ThrottleKib);
            Assert.Equal(VideoTrimMode.Exact, args.TrimMode);
            Assert.Null(args.Oauth);
            Assert.Null(args.FfmpegPath);
            Assert.Equal("", args.TempFolder);
            Assert.Equal(OverwriteBehavior.Prompt, args.OverwriteBehavior);
            Assert.True(args.ShowBanner);
            Assert.Equal(LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, args.LogLevel);
        }

        // Every option, using the short aliases where they exist and lower-case enum values, must land in the matching property.
        // Guards against swapped/missing aliases (-q, -b, -e, -t) and against losing CaseInsensitiveEnumValues.
        [Fact]
        public void ParsesEveryOption()
        {
            var args = CliArgs.Parse<VideoDownloadArgs>(
                "videodownload", "-u", "https://twitch.tv/videos/123456789", "-o", "vod.m4a", "-q", "Audio", "-b", "1:30", "-e", "2h",
                "-t", "8", "--bandwidth", "512", "--trim-mode", "safe", "--oauth", "token", "--ffmpeg-path", "ff", "--temp-path", "tmp",
                "--collision", "rename", "--banner", "false", "--log-level", "Error,Ffmpeg");

            Assert.Equal("https://twitch.tv/videos/123456789", args.Id);
            Assert.Equal("vod.m4a", args.OutputFile);
            Assert.Equal("Audio", args.Quality);
            Assert.Equal(TimeSpan.FromSeconds(90), (TimeSpan)args.TrimBeginningTime);
            Assert.Equal(TimeSpan.FromHours(2), (TimeSpan)args.TrimEndingTime);
            Assert.Equal(8, args.DownloadThreads);
            Assert.Equal(512, args.ThrottleKib);
            Assert.Equal(VideoTrimMode.Safe, args.TrimMode);
            Assert.Equal("token", args.Oauth);
            Assert.Equal("ff", args.FfmpegPath);
            Assert.Equal("tmp", args.TempFolder);
            Assert.Equal(OverwriteBehavior.Rename, args.OverwriteBehavior);
            Assert.False(args.ShowBanner);
            Assert.Equal(LogLevel.Error | LogLevel.Ffmpeg, args.LogLevel);
        }

        // --id and --output are required; omitting either must be a parse error (which Program turns into the help text)
        // rather than a null reaching the downloader.
        [Theory]
        [InlineData("videodownload", "-o", "out.mp4")]
        [InlineData("videodownload", "--id", "123")]
        public void MissingRequiredOption_IsAnError(params string[] argv)
        {
            Assert.Contains(ErrorType.MissingRequiredOptionError, CliArgs.Errors(argv));
        }

        // Invalid values for typed options (enum, int, time duration) must be reported as parse errors instead of being
        // silently replaced with a default or throwing out of the parser.
        [Theory]
        [InlineData("--trim-mode", "sloppy")]
        [InlineData("-t", "many")]
        [InlineData("-b", "soon")]
        public void InvalidOptionValue_IsAnError(string option, string value)
        {
            Assert.Contains(ErrorType.BadFormatConversionError, CliArgs.Errors("videodownload", "--id", "123", "-o", "out.mp4", option, value));
        }
    }
}
