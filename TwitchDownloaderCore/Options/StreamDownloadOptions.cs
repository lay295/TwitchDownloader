namespace TwitchDownloaderCore.Options
{
    public class StreamDownloadOptions
    {
        public string ChannelLogin { get; set; }
        public string Quality { get; set; }
        public string Filename { get; set; }
        public int DownloadThreads { get; set; }
        public TimeSpan StreamEndWaitTime { get; set; }
        public string Oauth { get; set; }
        public string FfmpegPath { get; set; }
        public string TempFolder { get; set; }
        public Func<DirectoryInfo[], DirectoryInfo[]> CacheCleanerCallback { get; set; }
        public Func<FileInfo, FileInfo> FileCollisionCallback { get; set; } = info => info;
    }
}
