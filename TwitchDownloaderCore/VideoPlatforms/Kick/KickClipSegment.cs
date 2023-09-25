namespace TwitchDownloaderCore.VideoPlatforms.Kick
{
    public class KickClipSegment
    {
        public string DownloadUrl { get; set; }
        public int StartByteOffset { get; set; }
        public int ByteRangeLength { get; set; }
    }
}