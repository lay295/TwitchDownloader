using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;

namespace TwitchDownloaderCLI.Tests.ArgumentTests
{
    // Tests the option declarations of the `ffmpeg` verb (FfmpegArgs). The error message printed when ffmpeg is missing tells users to run
    // `ffmpeg -d`, so that exact alias must keep working.
    public class FfmpegArgsTests
    {
        // Without flags the verb must do nothing (download off) and use the standard interface defaults.
        [Fact]
        public void UsesDocumentedDefaults()
        {
            var args = CliArgs.Parse<FfmpegArgs>("ffmpeg");

            Assert.False(args.DownloadFfmpeg);
            Assert.True(args.ShowBanner);
            Assert.Equal(LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, args.LogLevel);
        }

        // Both the short and long spelling of the download switch must turn it on.
        [Theory]
        [InlineData("-d")]
        [InlineData("--download")]
        public void ParsesDownloadSwitch(string flag)
        {
            Assert.True(CliArgs.Parse<FfmpegArgs>("ffmpeg", flag).DownloadFfmpeg);
        }
    }
}
