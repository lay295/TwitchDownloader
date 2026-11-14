using CommandLine;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCLI.Tests.ArgumentTests
{
    // Tests the option declarations of the `chatdownload` verb (ChatDownloadArgs), the CLI's public contract for downloading chat.
    public class ChatDownloadArgsTests
    {
        // With only the required options, the rest must take their documented defaults. The third-party emote toggles must default to
        // true because DownloadChat casts them with `(bool)x!`, so a null default would throw.
        [Fact]
        public void UsesDocumentedDefaults()
        {
            var args = CliArgs.Parse<ChatDownloadArgs>("chatdownload", "--id", "123456789", "-o", "chat.json");

            Assert.Equal("123456789", args.Id);
            Assert.Equal("chat.json", args.OutputFile);
            Assert.Equal(ChatCompression.None, args.Compression);
            Assert.Equal(TimeSpan.Zero, (TimeSpan)args.TrimBeginningTime);
            Assert.Equal(TimeSpan.Zero, (TimeSpan)args.TrimEndingTime);
            Assert.False(args.EmbedData);
            Assert.True(args.BttvEmotes);
            Assert.True(args.FfzEmotes);
            Assert.True(args.StvEmotes);
            Assert.Equal(TimestampFormat.Relative, args.TimeFormat);
            Assert.Equal(4, args.DownloadThreads);
            Assert.Equal("", args.TempFolder);
            Assert.Equal(OverwriteBehavior.Prompt, args.OverwriteBehavior);
            Assert.True(args.ShowBanner);
            Assert.Equal(LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, args.LogLevel);
        }

        // Every option, using short aliases where available, must land in its property.
        [Fact]
        public void ParsesEveryOption()
        {
            var args = CliArgs.Parse<ChatDownloadArgs>(
                "chatdownload", "-u", "SomeClipSlug", "-o", "chat.txt", "--compression", "gzip", "-b", "10s", "-e", "500ms", "-E",
                "--bttv", "false", "--ffz", "false", "--stv", "false", "--timestamp-format", "utcfull", "-t", "2", "--temp-path", "tmp",
                "--collision", "Exit", "--banner", "false", "--log-level", "Verbose");

            Assert.Equal("SomeClipSlug", args.Id);
            Assert.Equal("chat.txt", args.OutputFile);
            Assert.Equal(ChatCompression.Gzip, args.Compression);
            Assert.Equal(TimeSpan.FromSeconds(10), (TimeSpan)args.TrimBeginningTime);
            Assert.Equal(TimeSpan.FromMilliseconds(500), (TimeSpan)args.TrimEndingTime);
            Assert.True(args.EmbedData);
            Assert.False(args.BttvEmotes);
            Assert.False(args.FfzEmotes);
            Assert.False(args.StvEmotes);
            Assert.Equal(TimestampFormat.UtcFull, args.TimeFormat);
            Assert.Equal(2, args.DownloadThreads);
            Assert.Equal("tmp", args.TempFolder);
            Assert.Equal(OverwriteBehavior.Exit, args.OverwriteBehavior);
            Assert.False(args.ShowBanner);
            Assert.Equal(LogLevel.Verbose, args.LogLevel);
        }

        // --id and --output are required.
        [Theory]
        [InlineData("chatdownload", "-o", "chat.json")]
        [InlineData("chatdownload", "--id", "123")]
        public void MissingRequiredOption_IsAnError(params string[] argv)
        {
            Assert.Contains(ErrorType.MissingRequiredOptionError, CliArgs.Errors(argv));
        }
    }
}
