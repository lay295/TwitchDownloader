using System.Configuration;
using System.Reflection;
using TwitchDownloaderWPF.Properties;

namespace TwitchDownloaderWPF.Tests.TestHelpers
{
    internal static class SettingDefaults
    {
        /// <summary>The fresh-install default of a user setting, read from its [DefaultSettingValue] attribute.</summary>
        /// <remarks>
        /// Settings.Default.Properties is not used because ApplicationSettingsBase builds it lazily without a lock, so a test reading it while
        /// another parallel test triggers that initialization can see it half-populated.
        /// </remarks>
        public static string Of(string settingName) =>
            typeof(Settings).GetProperty(settingName)!.GetCustomAttribute<DefaultSettingValueAttribute>()!.Value;
    }
}
