using System;
using System.IO;

namespace TwitchDownloaderCore.Options
{
    public class ClipDownloadOptions
    {
        public string Id { get; set; }
        public string Quality { get; set; }
        public string Filename { get; set; }
        public int ThrottleKib { get; set; }
        public string TempFolder { get; set; }
        public bool EncodeMetadata { get; set; }
        public string FfmpegPath { get; set; }
        public Func<FileInfo, FileInfo> FileOverwriteCallback { get; set; } = info => info;
    }
}