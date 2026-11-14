using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CommandLine;
using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests
{
    // Tests Program, the CLI entry point: verb registration and parsing, dispatch from Main to each mode, and the version banner.
    // Not covered: WriteHelpText (every parse error/help request ends in Environment.Exit(1), which would kill the test host); the
    // end-to-end process tests exercise it by running the real binary.
    // Main also writes COPYRIGHT.txt/THIRD-PARTY-LICENSES.txt next to the process (as it does in production).
    // In the static-HttpClient collection (non-parallel) because the dispatch tests fake Twitch/the update feed and capture the console.
    [Collection(StaticHttpClientCollection.NAME)]
    public class ProgramTests
    {
        private const string PROGRAM = "TwitchDownloaderCLI.Program, TwitchDownloaderCLI";

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "Main")]
        private static extern void RunMain([UnsafeAccessorType(PROGRAM)] object? @class, string[] args);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "WriteApplicationBanner")]
        private static extern void WriteApplicationBanner([UnsafeAccessorType(PROGRAM)] object? @class, ITwitchDownloaderArgs args);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_GitHashRegex")]
        private static extern Regex GitHashRegex([UnsafeAccessorType(PROGRAM)] object? @class);

        private static readonly Type[] VerbTypes = typeof(ITwitchDownloaderArgs).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<VerbAttribute>() is not null)
            .ToArray();

        // Each verb name (in any case, since PreParseArgs lower-cases it) must select its own argument type.
        [Theory]
        [InlineData("videodownload", typeof(VideoDownloadArgs))]
        [InlineData("ClipDownload", typeof(ClipDownloadArgs))]
        [InlineData("chatdownload", typeof(ChatDownloadArgs))]
        [InlineData("chatupdate", typeof(ChatUpdateArgs))]
        [InlineData("CHATRENDER", typeof(ChatRenderArgs))]
        [InlineData("info", typeof(InfoArgs))]
        [InlineData("ffmpeg", typeof(FfmpegArgs))]
        [InlineData("cache", typeof(CacheArgs))]
        [InlineData("update", typeof(UpdateArgs))]
        [InlineData("tsmerge", typeof(TsMergeArgs))]
        public void VerbSelectsArgumentType(string verb, Type expectedType)
        {
            var result = CliArgs.ParseResult(verb, "--id", "1", "-i", "a", "-o", "b");

            Assert.Equal(expectedType, result.TypeInfo.Current);
        }

        // Main casts every parsed verb to ITwitchDownloaderArgs for the banner, and the file-writing verbs must offer --collision.
        // A new verb that forgets either interface would crash at startup or silently overwrite files.
        [Fact]
        public void EveryVerbImplementsTheSharedInterfaces()
        {
            Assert.Equal(10, VerbTypes.Length);
            Assert.All(VerbTypes, t => Assert.True(typeof(ITwitchDownloaderArgs).IsAssignableFrom(t), t.Name));

            var collisionVerbs = VerbTypes.Where(t => typeof(IFileCollisionArgs).IsAssignableFrom(t)).Select(t => t.GetCustomAttribute<VerbAttribute>()!.Name).Order();
            Assert.Equal(["chatdownload", "chatrender", "chatupdate", "clipdownload", "tsmerge", "videodownload"], collisionVerbs);
        }

        // Command lines that are not a runnable verb must produce the matching parser error, which Main turns into the right help text.
        [Theory]
        [InlineData(ErrorType.NoVerbSelectedError)]
        [InlineData(ErrorType.BadVerbSelectedError, "download")]
        [InlineData(ErrorType.HelpVerbRequestedError, "help")]
        [InlineData(ErrorType.VersionRequestedError, "--version")]
        [InlineData(ErrorType.HelpRequestedError, "info", "--help")]
        [InlineData(ErrorType.UnknownOptionError, "cache", "--nope")]
        public void InvalidCommandLines_ProduceParserErrors(ErrorType expected, params string[] argv)
        {
            Assert.Contains(expected, CliArgs.Errors(argv));
        }

        // Main dispatches `info` to InfoHandler: with a faked Twitch API the VOD playlist is printed, and --banner false keeps stdout
        // clean for piping.
        [Fact]
        public void Main_DispatchesInfo()
        {
            var http = new FakeHttpMessageHandler((request, body) =>
                request.RequestUri!.Host == "usher.ttvnw.net" ? FakeHttpMessageHandler.Text(TestData.ReadText("CLI", "info_video_playlist.m3u8"))
                : body!.Contains("PlaybackAccessToken_Template") ? FakeHttpMessageHandler.Json(TestData.ReadText("CLI", "info_video_token.json"))
                : body.Contains("VideoPlayer_ChapterSelectButtonVideo") ? FakeHttpMessageHandler.Json(TestData.ReadText("CLI", "info_video_chapters.json"))
                : body.Contains("query{video(id:") ? FakeHttpMessageHandler.Json(TestData.ReadText("CLI", "info_video.json"))
                : new HttpResponseMessage(HttpStatusCode.NotFound));
            using var httpOverride = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, http);
            using var console = new ConsoleCapture();

            RunMain(null, ["info", "--id", "123456789", "--format", "m3u8", "--banner", "false", "--log-level", "None", "--use-utf8", "false"]);

            Assert.StartsWith("#EXTM3U", console.Output);
            Assert.Contains("https://example.invalid/vod/chunked/index-dvr.m3u8", console.Output);
        }

        // Main dispatches `tsmerge` (here with an upper-case verb) to MergeTs, producing the merged file.
        [Fact]
        public void Main_DispatchesTsMerge()
        {
            using var temp = new TempDirectory();
            var part = temp.Combine("1.ts");
            File.WriteAllBytes(part, new byte[188]);
            var list = temp.Combine("list.txt");
            File.WriteAllText(list, part);
            using var console = new ConsoleCapture();

            RunMain(null, ["TSMERGE", "-i", list, "-o", temp.Combine("out.ts"), "--log-level", "None"]);

            Assert.Equal(188, new FileInfo(temp.Combine("out.ts")).Length);
            Assert.Equal("", console.Output);
        }

        // Main prints the banner before running the verb: `cache` with no flags does nothing else, `update` (debug build) only logs that
        // self-update is unavailable; the feed is faked in case the tests run as a release build.
        [Fact]
        public void Main_PrintsBannerThenRunsVerb()
        {
            using var httpOverride = new HttpClientOverride(c => UpdateHandler.HttpClient = c, () => UpdateHandler.HttpClient,
                new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Text("<item><version>1.0.0</version></item>")));
            using var console = new ConsoleCapture();

            RunMain(null, ["cache"]);
            RunMain(null, ["ffmpeg", "--banner", "false"]);
            RunMain(null, ["update", "--banner", "false"]);

            var lines = console.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            Assert.False(string.IsNullOrWhiteSpace(lines[0])); // banner of `cache`
            Assert.StartsWith("[INFO] - ", lines[1]);
        }

        // The banner is shown by default but suppressed by --banner false and by --log-level None (which must keep stdout empty).
        [Theory]
        [InlineData(true, "cache")]
        [InlineData(false, "cache", "--banner", "false")]
        [InlineData(false, "cache", "--log-level", "None")]
        [InlineData(false, "cache", "--log-level", "Error,None")]
        public void Banner_IsSuppressible(bool expectBanner, params string[] argv)
        {
            using var console = new ConsoleCapture();

            WriteApplicationBanner(null, CliArgs.Parse<CacheArgs>(argv));

            Assert.Equal(expectBanner, console.Output.Length > 0);
            Assert.DoesNotContain("©", console.Output); // the copyright sign is replaced for consoles without Unicode support
        }

        // Release builds strip the "+<git hash>" suffix from the version in the banner, but nothing else.
        [Theory]
        [InlineData("TwitchDownloaderCLI 1.56.5+0123abcdef", "TwitchDownloaderCLI 1.56.5")]
        [InlineData("TwitchDownloaderCLI 1.56.5", "TwitchDownloaderCLI 1.56.5")]
        [InlineData("C++ 1.0", "C++ 1.0")]
        public void GitHashRegex_StripsOnlyTheCommitSuffix(string input, string expected)
        {
            Assert.Equal(expected, GitHashRegex(null).Replace(input, ""));
        }
    }
}
