using TwitchDownloaderAvalonia.Update.Models;

namespace TwitchDownloaderAvalonia.Update.Services
{
    public static class UpdatePromptPolicy
    {
        public static readonly TimeSpan RemindLaterDelay = TimeSpan.FromDays(2);

        public static bool ShouldPrompt(Version remoteVersion, UpdatePreferences preferences, DateTimeOffset now)
        {
            if (!preferences.OfferOnStartup)
                return false;

            if (IsSkipped(remoteVersion, preferences.SkippedVersion))
                return false;

            return preferences.RemindLaterUntil is not { } until || now >= until;
        }

        public static bool IsSkipped(Version remoteVersion, string? skippedVersion)
        {
            return Version.TryParse(skippedVersion, out var skipped) && remoteVersion <= skipped;
        }

        public static UpdatePreferences Skip(UpdatePreferences current, Version remoteVersion)
        {
            return current with { SkippedVersion = remoteVersion.ToString(), RemindLaterUntil = null };
        }

        public static UpdatePreferences RemindLater(UpdatePreferences current, DateTimeOffset now)
        {
            return current with { RemindLaterUntil = now.Add(RemindLaterDelay) };
        }

        public static UpdatePreferences WithOfferOnStartup(UpdatePreferences current, bool offerOnStartup)
        {
            return current with { OfferOnStartup = offerOnStartup };
        }
    }
}
