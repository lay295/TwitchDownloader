using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;

namespace TwitchDownloaderCLI.Tests.ArgumentTests
{
    // Tests the option declarations of the `cache` verb (CacheArgs). Clearing the cache is destructive, so both switches must default to off.
    public class CacheArgsTests
    {
        // Without flags neither clear mode may be enabled.
        [Fact]
        public void UsesDocumentedDefaults()
        {
            var args = CliArgs.Parse<CacheArgs>("cache");

            Assert.False(args.ClearCache);
            Assert.False(args.ForceClearCache);
            Assert.True(args.ShowBanner);
            Assert.Equal(LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, args.LogLevel);
        }

        // -c/--clear (prompting) and --force-clear (no prompt) must map to their own properties and not to each other.
        [Theory]
        [InlineData("-c", true, false)]
        [InlineData("--clear", true, false)]
        [InlineData("--force-clear", false, true)]
        public void ParsesClearSwitches(string flag, bool clear, bool forceClear)
        {
            var args = CliArgs.Parse<CacheArgs>("cache", flag);

            Assert.Equal(clear, args.ClearCache);
            Assert.Equal(forceClear, args.ForceClearCache);
        }
    }
}
