namespace TwitchDownloaderAvalonia.Models
{
    public sealed class EnqueueOptions
    {
        public required string Folder { get; init; }
        public required string Quality { get; init; }
        public bool DownloadVideo { get; init; } = true;
        public bool DelayVideo { get; init; }
        public bool DownloadChat { get; init; }
        public ChatFormat ChatFormat { get; init; } = ChatFormat.Json;
        public ChatCompression ChatCompression { get; init; } = ChatCompression.None;
        public bool EmbedImages { get; init; }
        public bool Bttv { get; init; }
        public bool Ffz { get; init; }
        public bool Stv { get; init; }
        public bool DelayChat { get; init; }
        public bool RenderChat { get; init; }
    }
}
