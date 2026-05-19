using System;

namespace TwitchDownloaderWPF.Models
{
    public class EnqueuePreset
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public string Quality { get; set; }
        public bool DownloadVideo { get; set; } = true;
        public bool DelayDownload { get; set; }
        public bool LiveDownload { get; set; }
        public bool DownloadChat { get; set; }
        public string ChatFormat { get; set; } = "JSON";
        public bool ChatCompressGzip { get; set; }
        public bool EmbedImages { get; set; }
        public bool EmbedBttv { get; set; } = true;
        public bool EmbedFfz { get; set; } = true;
        public bool EmbedStv { get; set; } = true;
        public bool DelayChat { get; set; }
        public bool RenderChat { get; set; }
    }
}
