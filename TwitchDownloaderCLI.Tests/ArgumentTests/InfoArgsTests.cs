using CommandLine;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;

namespace TwitchDownloaderCLI.Tests.ArgumentTests
{
    // Tests the option declarations of the `info` verb (InfoArgs), which scripts use to scrape VOD/clip metadata and playlists.
    public class InfoArgsTests
    {
        // With only --id, the output format must default to the human-readable table and UTF-8 output must be on.
        [Fact]
        public void UsesDocumentedDefaults()
        {
            var args = CliArgs.Parse<InfoArgs>("info", "--id", "123456789");

            Assert.Equal("123456789", args.Id);
            Assert.Equal(InfoPrintFormat.Table, args.Format);
            Assert.True(args.UseUtf8);
            Assert.Null(args.Oauth);
            Assert.True(args.ShowBanner);
            Assert.Equal(LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, args.LogLevel);
        }

        // Every option must land in its property.
        [Fact]
        public void ParsesEveryOption()
        {
            var args = CliArgs.Parse<InfoArgs>("info", "-u", "SomeClipSlug", "-f", "raw", "--use-utf8", "false", "--oauth", "token", "--banner", "false", "--log-level", "Error");

            Assert.Equal("SomeClipSlug", args.Id);
            Assert.Equal(InfoPrintFormat.Raw, args.Format);
            Assert.False(args.UseUtf8);
            Assert.Equal("token", args.Oauth);
            Assert.False(args.ShowBanner);
            Assert.Equal(LogLevel.Error, args.LogLevel);
        }

        // The help text advertises "M3U/M3U8"; both spellings (any case) must select the same playlist output. Guards the enum alias
        // M3U = M3U8 that makes `-f m3u` work.
        [Theory]
        [InlineData("m3u")]
        [InlineData("M3U8")]
        [InlineData("table")]
        [InlineData("json")]
        public void ParsesFormatNames(string format)
        {
            var args = CliArgs.Parse<InfoArgs>("info", "--id", "123", "--format", format);

            var expected = Enum.Parse<InfoPrintFormat>(format, ignoreCase: true);
            Assert.Equal(expected, args.Format);
        }

        // --id is required.
        [Fact]
        public void MissingId_IsAnError()
        {
            Assert.Contains(ErrorType.MissingRequiredOptionError, CliArgs.Errors("info"));
        }
    }
}
