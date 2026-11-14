using CommandLine;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;

namespace TwitchDownloaderCLI.Tests.CliTestHelpers
{
    /// <summary>
    /// Parses a command line exactly the way <c>Program.Main</c> does (legacy-syntax rewrite, then CommandLineParser with
    /// case-insensitive enums and every verb registered), so tests exercise the real option attributes instead of hand-built args objects.
    /// </summary>
    /// <remarks>Keep the parser settings and verb list in sync with <c>Program.Main</c>.</remarks>
    internal static class CliArgs
    {
        public static ParserResult<object> ParseResult(params string[] args)
        {
            var parser = new Parser(config =>
            {
                config.CaseInsensitiveEnumValues = true;
                config.HelpWriter = null;
            });

            return parser.ParseArguments<VideoDownloadArgs, ClipDownloadArgs, ChatDownloadArgs, ChatUpdateArgs, ChatRenderArgs, InfoArgs, FfmpegArgs, CacheArgs, UpdateArgs, TsMergeArgs>(
                PreParseArgs.Parse(args, "TwitchDownloaderCLI"));
        }

        /// <summary>Parses <paramref name="args"/> and asserts that it produced a <typeparamref name="T"/>.</summary>
        public static T Parse<T>(params string[] args)
        {
            var result = ParseResult(args);
            Assert.True(result.Tag == ParserResultType.Parsed, $"Parsing failed: {string.Join(", ", Errors(result))}");
            return Assert.IsType<T>(result.Value);
        }

        /// <summary>The error tags produced by parsing <paramref name="args"/>, or an empty array if parsing succeeded.</summary>
        public static ErrorType[] Errors(params string[] args) => Errors(ParseResult(args));

        private static ErrorType[] Errors(ParserResult<object> result) => result is NotParsed<object> notParsed
            ? notParsed.Errors.Select(e => e.Tag).ToArray()
            : [];
    }
}
