namespace TwitchDownloaderAvalonia.Update.Models
{
    public sealed record UpdatePreferences(string? SkippedVersion, DateTimeOffset? RemindLaterUntil, bool OfferOnStartup)
    {
        public static UpdatePreferences Empty { get; } = new(null, null, true);
    }
}
