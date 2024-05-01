using System;
using System.IO;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Options
{
    public class VideoDownloadOptions
    {
        public int Id { get; set; }
        public string Quality { get; set; }
        public string Filename { get; set; }
        public bool TrimBeginning { get; set; }
        public TimeSpan TrimBeginningTime { get; set; }
        public bool TrimEnding { get; set; }
        public TimeSpan TrimEndingTime { get; set; }
        public int DownloadThreads { get; set; }
        public int ThrottleKib { get; set; }
        public string Oauth { get; set; }
        public CaptionsStyle Captions { get; set; }
        public string FfmpegPath { get; set; }
        public string TempFolder { get; set; }
        public Func<DirectoryInfo[], DirectoryInfo[]> CacheCleanerCallback { get; set; }
    }
}
