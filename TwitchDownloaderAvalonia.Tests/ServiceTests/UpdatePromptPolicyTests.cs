using TwitchDownloaderAvalonia.Update;
using TwitchDownloaderAvalonia.Update.Models;
using TwitchDownloaderAvalonia.Update.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class UpdatePromptPolicyTests
    {
        private static readonly Version Remote = new(1, 56, 5);
        private static readonly DateTimeOffset Now = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void PromptsWhenNoPreferences()
        {
            Assert.True(UpdatePromptPolicy.ShouldPrompt(Remote, UpdatePreferences.Empty, Now));
        }

        [Fact]
        public void SkipHidesCurrentAndOlderVersions()
        {
            var skipped = UpdatePromptPolicy.Skip(UpdatePreferences.Empty, Remote);

            Assert.False(UpdatePromptPolicy.ShouldPrompt(Remote, skipped, Now));
            Assert.False(UpdatePromptPolicy.ShouldPrompt(new Version(1, 56, 4), skipped, Now));
            Assert.True(UpdatePromptPolicy.ShouldPrompt(new Version(1, 56, 6), skipped, Now));
        }

        [Fact]
        public void RemindLaterHidesUntilDelayElapses()
        {
            var reminded = UpdatePromptPolicy.RemindLater(UpdatePreferences.Empty, Now);

            Assert.False(UpdatePromptPolicy.ShouldPrompt(Remote, reminded, Now.AddDays(1)));
            Assert.True(UpdatePromptPolicy.ShouldPrompt(Remote, reminded, Now.Add(UpdatePromptPolicy.RemindLaterDelay)));
        }

        [Fact]
        public void OfferOnStartupFalseSuppressesPrompt()
        {
            var prefs = UpdatePromptPolicy.WithOfferOnStartup(UpdatePreferences.Empty, false);
            Assert.False(UpdatePromptPolicy.ShouldPrompt(Remote, prefs, Now));
        }
    }

    public class UpdatePreferencesStoreTests
    {
        [Fact]
        public void RoundTripsPreferences()
        {
            var path = Path.Combine(Path.GetTempPath(), "td-update-prefs-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var store = new UpdatePreferencesStore(path);
                var until = new DateTimeOffset(2026, 9, 19, 8, 0, 0, TimeSpan.Zero);
                store.Save(new UpdatePreferences("1.56.5", until, true));

                var loaded = store.Load();
                Assert.Equal("1.56.5", loaded.SkippedVersion);
                Assert.Equal(until, loaded.RemindLaterUntil);
                Assert.True(loaded.OfferOnStartup);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Fact]
        public void MigratesLegacyAutoInstallFlag()
        {
            var path = Path.Combine(Path.GetTempPath(), "td-update-prefs-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(path, """{"AutoInstall": false}""");
                var loaded = new UpdatePreferencesStore(path).Load();
                Assert.False(loaded.OfferOnStartup);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
