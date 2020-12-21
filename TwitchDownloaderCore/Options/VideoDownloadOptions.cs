using System;
using System.Collections.Generic;
using System.Text;

namespace TwitchDownloaderCore.Options
{
    public class VideoDownloadOptions
    {
        public int Id { get; set; }
        public string PlaylistUrl { get; set; }
        public string Quality { get; set; }
        public string Filename { get; set; }
        public bool CropBeginning { get; set; }
        public double CropBeginningTime { get; set; }
        public bool CropEnding { get; set; }
        public double CropEndingTime { get; set; }
        public int DownloadThreads { get; set; }
        public string Oauth { get; set; }
        public string FfmpegPath { get; set; }
        public string TempFolder { get; set; }
    }
}
