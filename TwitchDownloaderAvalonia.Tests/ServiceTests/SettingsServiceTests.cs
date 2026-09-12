using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class SettingsServiceTests
    {
        [Fact]
        public void SaveReplacesFileAtomically()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "avalonia-settings.json");
            var service = new SettingsService(path) { Current = { OAuth = "token-one" } };
            service.Save();
            service.Current.OAuth = "token-two";
            service.Save();

            var json = File.ReadAllText(path);
            Assert.Contains("token-two", json);
            Assert.DoesNotContain("token-one", json);
            Assert.False(File.Exists(path + ".tmp"));
        }
    }
}
