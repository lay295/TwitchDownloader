namespace TwitchDownloaderCore.Options
{
    // TODO: Move TimestampFormat to dedicated file
    public enum TimestampFormat
    {
        Utc,
        Relative,
        None
    }

    // TODO: Move ChatFormat to dedicated file
    public enum ChatFormat
    {
        Json,
        Text,
        Html
    }

    public class ChatDownloadOptions
    {
        public ChatFormat DownloadFormat { get; set; } = ChatFormat.Json;
        public string Id { get; set; }
        public string Filename { get; set; }
        public bool CropBeginning { get; set; }
        public double CropBeginningTime { get; set; }
        public bool CropEnding { get; set; }
        public double CropEndingTime { get; set; }
        public bool EmbedData { get; set; }
        public bool BttvEmotes { get; set; }
        public bool FfzEmotes { get; set; }
        public bool StvEmotes { get; set; }
        public int ConnectionCount { get; set; } = 1;
        public TimestampFormat TimeFormat { get; set; }
        public string FileExtension
        {
            get
            {
                return DownloadFormat switch
                {
                    ChatFormat.Json => "json",
                    ChatFormat.Html => "html",
                    ChatFormat.Text => "txt",
                    _ => ""
                };
            }
        }
        public string TempFolder { get; set; }
    }
}
