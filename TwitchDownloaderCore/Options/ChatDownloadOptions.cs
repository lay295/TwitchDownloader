using System;
using System.Collections.Generic;
using System.Text;

namespace TwitchDownloaderCore.Options
{
    public enum TimestampFormat { Utc, Relative, None }
    public class ChatDownloadOptions
    {
        public bool IsJson { get; set; } = true;
        public string Id { get; set; }
        public string Filename { get; set; }
        public bool CropBeginning { get; set; }
        public double CropBeginningTime { get; set; }
        public bool CropEnding { get; set; }
        public double CropEndingTime { get; set; }
        public bool Timestamp { get; set; }
        public bool EmbedEmotes { get; set; }

        public int ConnectionCount { get; set; }
        public TimestampFormat TimeFormat { get; set; }
    }
}
