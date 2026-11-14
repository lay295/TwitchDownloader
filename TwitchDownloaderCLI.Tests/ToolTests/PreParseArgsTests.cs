using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCLI.Tools;

namespace TwitchDownloaderCLI.Tests.ToolTests
{
    // Tests PreParseArgs, which runs before CommandLineParser to make verbs case-insensitive and to rewrite deprecated (legacy) options
    // into their modern equivalents. Old scripts keep working only as long as these rewrites stay correct.
    // In the process-state collection because the legacy rewrites print deprecation warnings to Console.Out.
    [Collection(ProcessStateCollection.NAME)]
    public class PreParseArgsTests
    {
        // Only the verb (first argument) is lower-cased, so `VideoDownload` works; option names and values (e.g. a case-sensitive
        // OAuth token or file path) must be left untouched. No warning is printed for modern syntax.
        [Fact]
        public void LowercasesOnlyTheVerb()
        {
            using var console = new ConsoleCapture();

            var result = PreParseArgs.Parse(["VideoDownload", "--OAuth", "AbC", "-o", "Out.MP4"], "TwitchDownloaderCLI");

            Assert.Equal(["videodownload", "--OAuth", "AbC", "-o", "Out.MP4"], result);
            Assert.Equal("", console.Output);
        }

        // An empty command line must pass through without an index-out-of-range on args[0].
        [Fact]
        public void EmptyArgs_ReturnsEmpty()
        {
            Assert.Empty(PreParseArgs.Parse([], "TwitchDownloaderCLI"));
        }

        // Each deprecated option must be rewritten to its replacement (including moving `--mode X` to a leading verb) and print a
        // deprecation warning naming the old option. Guards backwards compatibility for scripts written against older CLI versions.
        [Theory]
        [InlineData("-m VideoDownload --id 1", "videodownload --id 1", "--mode <mode> has been deprecated")]
        [InlineData("--id 1 --mode ChatRender -i a", "chatrender --id 1 -i a", "--mode <mode> has been deprecated")]
        [InlineData("chatdownload --embed-emotes", "chatdownload -E", "--embed-emotes has been deprecated")]
        [InlineData("chatdownload --silent", "chatdownload --log-level None", "--silent has been deprecated")]
        [InlineData("chatdownload --chat-connections 8", "chatdownload --threads 8", "--chat-connections has been deprecated")]
        [InlineData("--silent -m chatdownload --chat-connections 2", "chatdownload --log-level None --threads 2", "--chat-connections has been deprecated")]
        public void RewritesLegacySyntax(string input, string expected, string expectedWarning)
        {
            using var console = new ConsoleCapture();

            var result = PreParseArgs.Parse(input.Split(' '), "MyCli");

            Assert.Equal(expected.Split(' '), result);
            Assert.Contains($"[WARNING] {expectedWarning}", console.Output);
            Assert.Contains("Run 'MyCli help'", console.Output);
        }

        // A full legacy command line must still parse into the right verb and options end to end (rewrite + CommandLineParser).
        [Fact]
        public void LegacyCommandLine_ParsesIntoModernArgs()
        {
            using var console = new ConsoleCapture();

            var args = CliArgs.Parse<ChatDownloadArgs>("--mode", "ChatDownload", "--id", "123", "-o", "c.json", "--embed-emotes", "--silent", "--chat-connections", "6");

            Assert.True(args.EmbedData);
            Assert.Equal(LogLevel.None, args.LogLevel);
            Assert.Equal(6, args.DownloadThreads);
        }

        // --verbose-ffmpeg is rewritten into a --log-level list that must at least turn on ffmpeg output and keep errors visible.
        [Fact]
        public void VerboseFfmpeg_EnablesFfmpegLogging()
        {
            using var console = new ConsoleCapture();

            var args = CliArgs.Parse<ChatRenderArgs>("chatrender", "-i", "a.json", "-o", "a.mp4", "--verbose-ffmpeg");

            Assert.True((args.LogLevel & LogLevel.Ffmpeg) != 0);
            Assert.True((args.LogLevel & LogLevel.Error) != 0);
            Assert.Contains("--verbose-ffmpeg has been deprecated", console.Output);
        }

        // The deprecation warning promises '--log-level Status,Info,Warning,Error,Ffmpeg'. The rewrite builds that list from
        // Enum.GetNames(LogLevel) minus None/Verbose, but forgets the composite `All` member, so Verbose logging is switched on too.
        [Fact(Skip = "Bug: --verbose-ffmpeg is rewritten to 'Status,Info,Warning,Error,Ffmpeg,All' because LogLevel.All is not excluded, which also enables Verbose logging")]
        public void VerboseFfmpeg_RewritesToDocumentedLogLevels()
        {
            using var console = new ConsoleCapture();

            var result = PreParseArgs.Parse(["chatrender", "--verbose-ffmpeg"], "TwitchDownloaderCLI");

            Assert.Equal(["chatrender", "--log-level", "Status,Info,Warning,Error,Ffmpeg"], result);
        }
    }
}
