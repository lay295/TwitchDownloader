using System;
using System.Collections.Generic;
using System.Text;

namespace TwitchDownloaderCore.Options
{
    public enum TimestampFormat { Utc, Relative, None }
    public enum DownloadFormat { Json, Text, Html }
    public class ChatDownloadOptions
    {
        public DownloadFormat DownloadFormat { get; set; } = DownloadFormat.Json;
        public string Id { get; set; }
        public string Filename { get; set; }
        public bool CropBeginning { get; set; }
        public double CropBeginningTime { get; set; }
        public bool CropEnding { get; set; }
        public double CropEndingTime { get; set; }
        public bool Timestamp { get; set; }
        public bool EmbedEmotes { get; set; }
        public bool BttvEmotes { get; set; }
        public bool FfzEmotes { get; set; }
        public bool StvEmotes { get; set; }
        public int ConnectionCount { get; set; } = 1;
        public TimestampFormat TimeFormat { get; set; }
        public string FileExtension { 
            get 
            {
                if (DownloadFormat == DownloadFormat.Json)
                    return "json";
                else if (DownloadFormat == DownloadFormat.Html)
                    return "html";
                else if (DownloadFormat == DownloadFormat.Text)
                    return "txt";
                return "";
            }
        }
    }
}
