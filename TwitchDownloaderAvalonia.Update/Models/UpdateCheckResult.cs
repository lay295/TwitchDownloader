namespace TwitchDownloaderAvalonia.Update.Models
{
    public sealed record UpdateCheckResult(
        Version RemoteVersion,
        bool IsNewer,
        string ChangelogUrl,
        string? AvaloniaUrlTemplate);
}
