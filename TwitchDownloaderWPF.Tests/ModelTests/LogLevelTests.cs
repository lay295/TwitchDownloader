using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;
using TwitchDownloaderWPF.Tests.TestHelpers;

namespace TwitchDownloaderWPF.Tests.ModelTests
{
    // Tests LogLevel, a [Flags] enum persisted as an int bitmask in user.config (Settings.LogLevels) and tested bit-by-bit by WpfTaskProgress.
    // Changing a bit value would silently switch users' saved log levels on or off.
    public class LogLevelTests
    {
        // Verifies the persisted bit of each flag.
        // Why: guards the on-disk format; overlapping or renumbered bits make enabling one level also enable or disable another.
        [Theory]
        [InlineData(nameof(LogLevel.None), 0)]
        [InlineData(nameof(LogLevel.Verbose), 1)]
        [InlineData(nameof(LogLevel.Info), 2)]
        [InlineData(nameof(LogLevel.Warning), 4)]
        [InlineData(nameof(LogLevel.Error), 8)]
        [InlineData(nameof(LogLevel.Ffmpeg), 16)]
        public void Flag_HasStablePersistedBit(string member, int persisted)
        {
            Assert.Equal(persisted, (int)Enum.Parse<LogLevel>(member));
        }

        // Verifies the LogLevels setting defaults to Info | Warning | Error | Ffmpeg (verbose off).
        // Why: documents what a fresh install logs; a default including Verbose would flood the log box, one without Error would hide failures.
        [Fact]
        public void DefaultSetting_IsInfoWarningErrorFfmpeg()
        {
            var defaultValue = int.Parse(SettingDefaults.Of(nameof(Settings.LogLevels)));

            Assert.Equal(LogLevel.Info | LogLevel.Warning | LogLevel.Error | LogLevel.Ffmpeg, (LogLevel)defaultValue);
        }
    }
}
