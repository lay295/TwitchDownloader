using TwitchDownloaderCore.Chat;

namespace TwitchDownloaderCore.Options
{
    public class ChatUpdateOptions
    {
        public string InputFile { get; set; }
        public string OutputFile { get; set; }
        public ChatCompression Compression { get; set; } = ChatCompression.None;
        public ChatFormat OutputFormat { get; set; } = ChatFormat.Json;
        public bool EmbedMissing { get; set; }
        public bool ReplaceEmbeds { get; set; }
        public bool CropBeginning { get; set; }
        public double CropBeginningTime { get; set; }
        public bool CropEnding { get; set; }
        public double CropEndingTime { get; set; }
        public bool BttvEmotes { get; set; }
        public bool FfzEmotes { get; set; }
        public bool StvEmotes { get; set; }
        public TimestampFormat TextTimestampFormat { get; set; }
        public string FileExtension
        {
            get
            {
                return OutputFormat switch
                {
                    ChatFormat.Json when Compression is ChatCompression.None => "json",
                    ChatFormat.Json when Compression is ChatCompression.Gzip => "json.gz",
                    ChatFormat.Html => "html",
                    ChatFormat.Text => "txt",
                    _ => ""
                };
            }
        }
        public string TempFolder { get; set; }
    }
}
