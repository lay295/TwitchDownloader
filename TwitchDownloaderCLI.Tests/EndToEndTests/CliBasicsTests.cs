using TwitchDownloaderCore.Tests.TestHelpers;

namespace TwitchDownloaderCLI.Tests.EndToEndTests
{
    // End-to-end tests of the CLI's command line surface (help, version, verb parsing, argument errors, banner and log levels),
    // run against the real executable. These are what scripts and users see first, and they break silently when
    // CommandLineParser is upgraded or Program.cs changes, because no unit test calls Main.
    public class CliBasicsTests : IDisposable
    {
        private readonly TempDirectory _temp = new();
        private static readonly string ChatJson = TestData.Path("E2E", "chat.json");

        public void Dispose() => _temp.Dispose();

        // --help must list every verb. Guards against a verb class being dropped from Program's ParseArguments<...> list,
        // which would make the verb silently unusable.
        [Fact]
        public void HelpListsEveryVerb()
        {
            var result = Cli.Run(_temp.Path, ["--help"]);

            foreach (var verb in new[] { "videodownload", "clipdownload", "chatdownload", "chatupdate", "chatrender", "info", "ffmpeg", "cache", "update", "tsmerge" })
            {
                Assert.Contains(verb, result.StdErr);
            }
        }

        // --version must print the assembly version. Guards against the version string (used by bug reports and the update checker)
        // drifting from the project's <Version>.
        [Fact]
        public void VersionPrintsAssemblyVersion()
        {
            var version = typeof(TwitchDownloaderCLI.Program).Assembly.GetName().Version!;

            var result = Cli.Run(_temp.Path, ["--version"]);

            Assert.Contains($"TwitchDownloaderCLI {version.Major}.{version.Minor}.{version.Build}", result.StdErr);
        }

        // Asking for help or the version is a successful run by convention (GNU/POSIX), so scripts like `TwitchDownloaderCLI --version || exit`
        // work. Currently Program.WriteHelpText always calls Environment.Exit(1), even for help/version requests.
        [Theory(Skip = "Bug: --help, --version and 'help <verb>' exit with code 1 because Program.WriteHelpText always calls Environment.Exit(1)")]
        [InlineData("--help")]
        [InlineData("--version")]
        [InlineData("help", "chatupdate")]
        public void HelpAndVersionExitWithSuccess(params string[] args)
        {
            Assert.Equal(0, Cli.Run(_temp.Path, args).ExitCode);
        }

        // Per-verb help must describe that verb's options. Guards against option attributes being lost (e.g. the shared
        // IFileCollisionArgs --collision option, which is declared on an interface).
        [Fact]
        public void VerbHelpListsItsOptions()
        {
            var result = Cli.Run(_temp.Path, ["help", "chatupdate"]);

            Assert.Contains("--input", result.StdErr);
            Assert.Contains("--output", result.StdErr);
            Assert.Contains("--compression", result.StdErr);
            Assert.Contains("--collision", result.StdErr);
        }

        // An unknown verb must fail with a clear message, not run some default action.
        [Fact]
        public void UnknownVerbFails()
        {
            var result = Cli.Run(_temp.Path, ["bogusverb"]);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Verb 'bogusverb' is not recognized", result.StdErr);
        }

        // Running with no arguments must print a short usage hint and fail. Windows is excluded because there the CLI waits for a key
        // press (for users who double-click the exe), which cannot work with redirected input.
        [Fact]
        public void NoArgumentsPrintsUsage()
        {
            Assert.SkipWhen(OperatingSystem.IsWindows(), "On Windows the no-verb path waits for a key press");

            var result = Cli.Run(_temp.Path, []);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Usage:", result.StdOut);
            Assert.Contains("help", result.StdOut);
        }

        // Missing required options must be reported by name, and nothing may run.
        [Fact]
        public void MissingRequiredOptionsFail()
        {
            var result = Cli.Run(_temp.Path, ["chatupdate"]);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Required option 'i, input' is missing", result.StdErr);
            Assert.Contains("Required option 'o, output' is missing", result.StdErr);
        }

        // Malformed option values (enums, time durations) must be rejected by the parser with the offending option named, instead of
        // falling back to a default and doing something the user did not ask for.
        [Theory]
        [InlineData("--collision", "Sometimes", "collision")]
        [InlineData("--compression", "zip", "compression")]
        [InlineData("-b", "notatime", "b, beginning")]
        [InlineData("--log-level", "Loud", "log-level")]
        public void InvalidOptionValuesFail(string option, string value, string reportedName)
        {
            var output = _temp.Combine("out.txt");

            var result = Cli.Run(_temp.Path, ["chatupdate", "-i", ChatJson, "-o", output, option, value]);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains($"Option '{reportedName}' is defined with a bad format", result.StdErr);
            Assert.False(File.Exists(output));
        }

        // An unparsable VOD/clip id must be rejected before any network request is made.
        [Fact]
        public void InfoRejectsUnparsableId()
        {
            var result = Cli.Run(_temp.Path, ["info", "--id", "not a twitch id!"]);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("[ERROR] - Unable to parse VOD/Clip ID/URL.", result.StdOut);
        }

        // Verbs are matched case-insensitively (PreParseArgs lowercases the first argument), so "ChatUpdate" from older docs keeps working.
        [Fact]
        public void VerbIsCaseInsensitive()
        {
            var output = _temp.Combine("out.txt");

            Cli.RunOk(_temp.Path, "ChatUpdate", "-i", ChatJson, "-o", output);

            Assert.True(File.Exists(output));
        }

        // The legacy "--mode <verb>" syntax must still run the verb and print a deprecation warning, because old scripts still use it.
        [Fact]
        public void LegacyModeSyntaxStillWorks()
        {
            var output = _temp.Combine("out.txt");

            var result = Cli.RunOk(_temp.Path, "--mode", "chatupdate", "-i", ChatJson, "-o", output);

            Assert.Contains("--mode <mode> has been deprecated", result.StdOut);
            Assert.True(File.Exists(output));
        }

        // The banner is printed by default and suppressed by --banner false. Scripts that parse stdout rely on being able to turn it off.
        [Fact]
        public void BannerIsShownByDefaultAndCanBeDisabled()
        {
            var withBanner = Cli.RunOk(_temp.Path, "cache");
            var withoutBanner = Cli.RunOk(_temp.Path, "cache", "--banner", "false");

            Assert.StartsWith("TwitchDownloaderCLI ", withBanner.StdOut);
            Assert.Contains("lay295", withBanner.StdOut);
            Assert.DoesNotContain("TwitchDownloaderCLI", withoutBanner.StdOut);
        }

        // --log-level None must silence everything on stdout (banner, status, info) while still doing the work. Guards the "quiet mode"
        // that the deprecated --silent flag maps to.
        [Fact]
        public void LogLevelNoneSilencesOutput()
        {
            var output = _temp.Combine("out.txt");

            var result = Cli.RunOk(_temp.Path, "chatupdate", "-i", ChatJson, "-o", output, "--log-level", "None");

            Assert.Equal("", result.StdOut);
            Assert.True(File.Exists(output));
        }
    }
}
