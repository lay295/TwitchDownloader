﻿namespace TwitchDownloaderCore.Options
{
    public class ChatUpdateOptions
    {
        public string InputFile { get; set; }
        public string OutputFile { get; set; }
        public ChatFormat FileFormat { get; set; } = ChatFormat.Json;
        public bool EmbedMissing { get; set; }
        public bool ReplaceEmbeds { get; set; }
        public bool CropBeginning { get; set; }
        public double CropBeginningTime { get; set; }
        public bool CropEnding { get; set; }
        public double CropEndingTime { get; set; }
        public bool BttvEmotes { get; set; }
        public bool FfzEmotes { get; set; }
        public bool StvEmotes { get; set; }
        public string TempFolder { get; set; }
    }
}
