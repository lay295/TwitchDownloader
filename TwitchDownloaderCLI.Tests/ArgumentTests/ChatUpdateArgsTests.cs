using CommandLine;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCLI.Tests.ArgumentTests
{
    // Tests the option declarations of the `chatupdate` verb (ChatUpdateArgs), the CLI's public contract for updating chat files.
    public class ChatUpdateArgsTests
    {
        // With only the required options, the rest must take their documented defaults. The trim times must default to -1s, which
        // UpdateChat interprets as "keep the file's current trim"; a 0 default would re-trim every updated chat to the start.
        [Fact]
        public void UsesDocumentedDefaults()
        {
            var args = CliArgs.Parse<ChatUpdateArgs>("chatupdate", "-i", "in.json", "-o", "out.json");

            Assert.Equal("in.json", args.InputFile);
            Assert.Equal("out.json", args.OutputFile);
            Assert.Equal(ChatCompression.None, args.Compression);
            Assert.False(args.EmbedMissing);
            Assert.False(args.ReplaceEmbeds);
            Assert.Equal(TimeDuration.MinusOneSeconds, args.TrimBeginningTime);
            Assert.Equal(TimeDuration.MinusOneSeconds, args.TrimEndingTime);
            Assert.True(args.BttvEmotes);
            Assert.True(args.FfzEmotes);
            Assert.True(args.StvEmotes);
            Assert.Equal(TimestampFormat.Relative, args.TimeFormat);
            Assert.Equal("", args.TempFolder);
            Assert.Equal(OverwriteBehavior.Prompt, args.OverwriteBehavior);
            Assert.True(args.ShowBanner);
            Assert.Equal(LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, args.LogLevel);
        }

        // Every option, using short aliases where available, must land in its property.
        [Fact]
        public void ParsesEveryOption()
        {
            var args = CliArgs.Parse<ChatUpdateArgs>(
                "chatupdate", "--input", "in.json.gz", "--output", "out.html", "--compression", "Gzip", "-E", "-R", "-b", "0", "-e", "1m",
                "--bttv", "false", "--ffz", "false", "--stv", "false", "--timestamp-format", "None", "--temp-path", "tmp",
                "--collision", "Rename", "--banner", "false", "--log-level", "Warning");

            Assert.Equal("in.json.gz", args.InputFile);
            Assert.Equal("out.html", args.OutputFile);
            Assert.Equal(ChatCompression.Gzip, args.Compression);
            Assert.True(args.EmbedMissing);
            Assert.True(args.ReplaceEmbeds);
            Assert.Equal(TimeSpan.Zero, (TimeSpan)args.TrimBeginningTime);
            Assert.Equal(TimeSpan.FromMinutes(1), (TimeSpan)args.TrimEndingTime);
            Assert.False(args.BttvEmotes);
            Assert.False(args.FfzEmotes);
            Assert.False(args.StvEmotes);
            Assert.Equal(TimestampFormat.None, args.TimeFormat);
            Assert.Equal("tmp", args.TempFolder);
            Assert.Equal(OverwriteBehavior.Rename, args.OverwriteBehavior);
            Assert.False(args.ShowBanner);
            Assert.Equal(LogLevel.Warning, args.LogLevel);
        }

        // --input and --output are required.
        [Theory]
        [InlineData("chatupdate", "-o", "out.json")]
        [InlineData("chatupdate", "-i", "in.json")]
        public void MissingRequiredOption_IsAnError(params string[] argv)
        {
            Assert.Contains(ErrorType.MissingRequiredOptionError, CliArgs.Errors(argv));
        }
    }
}
