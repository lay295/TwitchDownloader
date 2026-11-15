using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Tests.ModelTests
{
    // Tests the CLI enums in Models/Enums.cs. Their member names are user-facing option values (--log-level, --collision, --format),
    // and LogLevel is a bit-flag set that CliTaskProgress filters with bitwise AND.
    public class EnumsTests
    {
        // Every LogLevel except the composite All must be a distinct single bit, and All must be every level except None. Overlapping
        // bits would make e.g. --log-level Info also print Verbose output; None inside All would silence `--log-level All`.
        [Fact]
        public void LogLevel_IsAProperFlagSet()
        {
            var levels = Enum.GetValues<LogLevel>().Where(x => x != LogLevel.All).Select(x => (int)x).ToArray();

            Assert.All(levels, x => Assert.True(int.IsPow2(x), $"{(LogLevel)x} is not a single bit"));
            Assert.Equal(levels.Length, levels.Distinct().Count());
            Assert.Equal((int)LogLevel.All, levels.Where(x => x != (int)LogLevel.None).Sum());
        }

        // The documented option values must keep existing: --collision Overwrite/Exit/Rename/Prompt and --format Raw/Table/M3U8/M3U/Json,
        // where M3U is an alias of M3U8.
        [Fact]
        public void UserFacingNames_AreStable()
        {
            Assert.Equal(["Overwrite", "Exit", "Rename", "Prompt"], Enum.GetNames<OverwriteBehavior>());
            Assert.Equal(InfoPrintFormat.M3U8, InfoPrintFormat.M3U);
            Assert.All(["Raw", "Table", "M3U8", "M3U", "Json"], name => Assert.True(Enum.IsDefined(typeof(InfoPrintFormat), name), name));
        }
    }
}
