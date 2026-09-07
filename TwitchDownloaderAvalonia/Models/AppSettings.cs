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
        public bool EncodeClipMetadata { get; set; } = true;
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
