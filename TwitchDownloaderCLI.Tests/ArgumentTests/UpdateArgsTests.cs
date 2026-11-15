using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;

namespace TwitchDownloaderCLI.Tests.ArgumentTests
{
    // Tests the option declarations of the `update` verb (UpdateArgs). Self-update replaces the executable, so it must only skip the
    // confirmation prompt when explicitly forced.
    public class UpdateArgsTests
    {
        // Without flags, the update must prompt for confirmation and delete the downloaded archive afterwards.
        [Fact]
        public void UsesDocumentedDefaults()
        {
            var args = CliArgs.Parse<UpdateArgs>("update");

            Assert.False(args.ForceUpdate);
            Assert.False(args.KeepArchive);
            Assert.True(args.ShowBanner);
            Assert.Equal(LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, args.LogLevel);
        }

        // -f/--force and -k/--keep-update must each set only their own property.
        [Theory]
        [InlineData("-f", true, false)]
        [InlineData("--force", true, false)]
        [InlineData("-k", false, true)]
        [InlineData("--keep-update", false, true)]
        public void ParsesSwitches(string flag, bool force, bool keep)
        {
            var args = CliArgs.Parse<UpdateArgs>("update", flag);

            Assert.Equal(force, args.ForceUpdate);
            Assert.Equal(keep, args.KeepArchive);
        }
    }
}
