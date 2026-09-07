using TwitchDownloaderCore.Models;

namespace TwitchDownloaderAvalonia.Models
{
    public sealed class AppSettings
    {
        public int VodDownloadThreads { get; set; } = 4;
        public string OAuth { get; set; } = string.Empty;
        public string TempPath { get; set; } = string.Empty;
        public string TemplateVod { get; set; } = "[{date_custom=\"M-d-yy\"}] {channel} - {title}";
        public string TemplateClip { get; set; } = "[{date_custom=\"M-d-yy\"}] {channel} - {title}";
        public string TemplateChat { get; set; } = "[{date_custom=\"M-d-yy\"}] {channel} - {title} - Chat";
        public bool EncodeClipMetadata { get; set; } = true;
        public int ChatDownloadThreads { get; set; } = 4;
        public ChatFormat ChatDownloadFormat { get; set; } = ChatFormat.Json;
        public ChatCompression ChatJsonCompression { get; set; } = ChatCompression.None;
        public TimestampFormat ChatTextTimestampStyle { get; set; } = TimestampFormat.Utc;
        public bool ChatEmbedEmotes { get; set; }
        public bool ChatEmbedMissing { get; set; }
        public bool ChatReplaceEmbeds { get; set; }
        public bool BttvEmotes { get; set; } = true;
        public bool FfzEmotes { get; set; } = true;
        public bool StvEmotes { get; set; } = true;
        public bool DownloadThrottleEnabled { get; set; }
        public int MaximumBandwidthKib { get; set; } = 4096;
        public int LogLevels { get; set; } = (int)(LogLevel.Info | LogLevel.Warning | LogLevel.Error | LogLevel.Ffmpeg);
        public VideoTrimMode VodTrimMode { get; set; } = VideoTrimMode.Exact;
        public CollisionBehavior FileCollisionBehavior { get; set; } = CollisionBehavior.Prompt;
        public bool VerboseErrors { get; set; }
        public bool UtcVideoTime { get; set; }
        public bool ReduceMotion { get; set; }
    }
}
