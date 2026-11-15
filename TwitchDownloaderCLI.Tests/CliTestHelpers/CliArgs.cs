using CommandLine;
using TwitchDownloaderCLI.Tools;

namespace TwitchDownloaderCLI.Tests.CliTestHelpers
{
    /// <summary>
    /// Parses a command line exactly the way <c>Program.Main</c> does (legacy-syntax rewrite, then <see cref="Program.ParseVerbs"/>),
    /// so tests exercise the real option attributes and parser settings instead of hand-built args objects.
    /// </summary>
    internal static class CliArgs
    {
        public static ParserResult<object> ParseResult(params string[] args) =>
            Program.ParseVerbs(PreParseArgs.Parse(args, "TwitchDownloaderCLI"), out _);

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
