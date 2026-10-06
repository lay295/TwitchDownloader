namespace TwitchDownloaderAvalonia.Models
{
    public sealed class UiSettings
    {
        public string Theme { get; set; } = "System";
        public string LightTheme { get; set; } = "Light";
        public string DarkTheme { get; set; } = "Dark";
        public string Culture { get; set; } = "en-US";
        public bool HideDonation { get; set; }
        public bool ReduceMotion { get; set; }
        public int LogLevels { get; set; } = (int)(LogLevel.Info | LogLevel.Warning | LogLevel.Error | LogLevel.Ffmpeg);
    }

    public sealed class GeneralSettings
    {
        public string OAuth { get; set; } = string.Empty;
        public string TempPath { get; set; } = string.Empty;
        public string TemplateVod { get; set; } = "{channel}/[{date_custom=\"M-d-yy\"}] - {title}";
        public string TemplateClip { get; set; } = "{channel}/[{date_custom=\"M-d-yy\"}] - {title}";
        public string TemplateChat { get; set; } = "{channel}/[{date_custom=\"M-d-yy\"}] - {title} - Chat";
        public bool DownloadThrottleEnabled { get; set; }
        public int MaximumBandwidthKib { get; set; } = 4096;
        public CollisionBehavior FileCollisionBehavior { get; set; } = CollisionBehavior.Prompt;
        public bool VerboseErrors { get; set; }
        public bool UtcVideoTime { get; set; }
    }

    public sealed class VodSettings
    {
        public int DownloadThreads { get; set; } = 4;
        public VideoTrimMode TrimMode { get; set; } = VideoTrimMode.Exact;
    }

    public sealed class ClipSettings
    {
        public bool EncodeMetadata { get; set; } = true;
    }

    public sealed class ChatSettings
    {
        public int DownloadThreads { get; set; } = 4;
        public ChatFormat DownloadFormat { get; set; } = ChatFormat.Json;
        public ChatCompression JsonCompression { get; set; } = ChatCompression.None;
        public TimestampFormat TextTimestampStyle { get; set; } = TimestampFormat.Utc;
        public bool EmbedEmotes { get; set; }
        public bool EmbedMissing { get; set; }
        public bool ReplaceEmbeds { get; set; }
        public bool BttvEmotes { get; set; } = true;
        public bool FfzEmotes { get; set; } = true;
        public bool StvEmotes { get; set; } = true;
    }

    public sealed class QueueSettings
    {
        public string Folder { get; set; } = string.Empty;
        public string PreferredQuality { get; set; } = QualityNames.SOURCE;
        public int LimitVod { get; set; } = 6;
        public int LimitClip { get; set; } = 10;
        public int LimitChat { get; set; } = 10;
        public int LimitRender { get; set; } = 2;
        public bool AutoRemoveFinished { get; set; }
        public bool EnqueueDownloadVideo { get; set; } = true;
        public bool EnqueueDownloadChat { get; set; }
        public bool EnqueueRenderChat { get; set; }
        public bool EnqueueDelayVideo { get; set; }
        public bool EnqueueDelayChat { get; set; }
        public List<string> RecentChannels { get; set; } = [];
    }

    public sealed class SearchSettings
    {
        public SearchKind Kind { get; set; } = SearchKind.Videos;
        public string VideoType { get; set; } = string.Empty;
        public string ClipPeriod { get; set; } = "LAST_MONTH";
        public int PageSize { get; set; } = 30;
    }
}
