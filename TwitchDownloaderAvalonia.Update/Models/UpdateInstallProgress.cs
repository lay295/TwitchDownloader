namespace TwitchDownloaderAvalonia.Update.Models
{
    public enum UpdateInstallPhase
    {
        Downloading,
        Extracting,
        Finishing,
    }

    public sealed record UpdateInstallProgress(double Percent, UpdateInstallPhase Phase);
}
