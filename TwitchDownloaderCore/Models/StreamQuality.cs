namespace TwitchDownloaderCore.Models
{
    public sealed record StreamQuality
    {
        public IReadOnlyList<string> Codecs { get; set; }
        public int Bandwidth { get; set; }
        public Resolution Resolution { get; set; }
        public decimal Framerate { get; set; }
        public string Video { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsAudio => Video.Contains("audio_only", StringComparison.OrdinalIgnoreCase) || (Resolution.Width == 0 && Resolution.Height == 0);
    }
}
