namespace TwitchDownloaderAvalonia.Models
{
    public sealed class EnqueueOptions
    {
        public required string Folder { get; init; }
        public required string Quality { get; init; }
        public bool DownloadChat { get; init; }
    }
}
