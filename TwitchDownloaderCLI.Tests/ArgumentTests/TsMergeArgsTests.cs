using CommandLine;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;

namespace TwitchDownloaderCLI.Tests.ArgumentTests
{
    // Tests the option declarations of the `tsmerge` verb (TsMergeArgs).
    public class TsMergeArgsTests
    {
        // Both required options must be read, and the shared interface options must take their defaults.
        [Fact]
        public void ParsesRequiredOptionsAndDefaults()
        {
            var args = CliArgs.Parse<TsMergeArgs>("tsmerge", "-i", "list.txt", "-o", "out.ts");

            Assert.Equal("list.txt", args.InputList);
            Assert.Equal("out.ts", args.OutputFile);
            Assert.Equal(OverwriteBehavior.Prompt, args.OverwriteBehavior);
            Assert.True(args.ShowBanner);
            Assert.Equal(LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, args.LogLevel);
        }

        // The long option names and interface options must be honoured too.
        [Fact]
        public void ParsesLongOptionNames()
        {
            var args = CliArgs.Parse<TsMergeArgs>("tsmerge", "--input", "list.m3u8", "--output", "out.ts", "--collision", "rename", "--banner", "false");

            Assert.Equal("list.m3u8", args.InputList);
            Assert.Equal("out.ts", args.OutputFile);
            Assert.Equal(OverwriteBehavior.Rename, args.OverwriteBehavior);
            Assert.False(args.ShowBanner);
        }

        // --input and --output are required.
        [Theory]
        [InlineData("tsmerge", "-o", "out.ts")]
        [InlineData("tsmerge", "-i", "list.txt")]
        public void MissingRequiredOption_IsAnError(params string[] argv)
        {
            Assert.Contains(ErrorType.MissingRequiredOptionError, CliArgs.Errors(argv));
        }
    }
}
