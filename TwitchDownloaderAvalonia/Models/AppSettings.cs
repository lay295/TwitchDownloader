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

        public string RenderFont { get; set; } = "Inter Embedded";
        public double RenderFontSize { get; set; } = 24;
        public int RenderWidth { get; set; } = 700;
        public int RenderHeight { get; set; } = 1200;
        public bool RenderOutline { get; set; }
        public bool RenderTimestamp { get; set; }
        public string RenderBackgroundColor { get; set; } = "#FF111111";
        public string RenderAlternateBackgroundColor { get; set; } = "#FF191919";
        public string RenderFontColor { get; set; } = "#FFFFFFFF";
        public string RenderHighlightUsersColor { get; set; } = "#C8FF0064";
        public double RenderUpdateTime { get; set; } = 0.2;
        public int RenderFramerate { get; set; } = 60;
        public bool RenderGenerateMask { get; set; }
        public bool RenderSharpening { get; set; }
        public bool RenderSubMessages { get; set; } = true;
        public bool RenderChatBadges { get; set; } = true;
        public bool RenderOffline { get; set; }
        public bool RenderUserAvatars { get; set; }
        public bool RenderDisperseCommentOffsets { get; set; } = true;
        public bool RenderAlternateMessageBackgrounds { get; set; }
        public bool RenderAdjustUsernameVisibility { get; set; } = true;
        public string RenderVideoContainer { get; set; } = "MP4";
        public string RenderVideoCodec { get; set; } = "H264";
        public string RenderIgnoreUsersList { get; set; } = string.Empty;
        public string RenderHighlightUsersList { get; set; } = string.Empty;
        public string RenderBannedWordsList { get; set; } = string.Empty;
        public int RenderEmojiVendor { get; set; } = (int)TwitchDownloaderCore.Chat.EmojiVendor.GoogleNotoColor;
        public int RenderChatBadgeMask { get; set; }
        public double RenderEmoteScale { get; set; } = 1;
        public double RenderEmojiScale { get; set; } = 1;
        public double RenderBadgeScale { get; set; } = 1;
        public double RenderAvatarScale { get; set; } = 1;
        public double RenderVerticalSpacingScale { get; set; } = 1;
        public double RenderUsernameFontScale { get; set; } = 1;
        public double RenderSidePaddingScale { get; set; } = 1;
        public double RenderSectionHeightScale { get; set; } = 1;
        public double RenderWordSpacingScale { get; set; } = 1;
        public double RenderEmoteSpacingScale { get; set; } = 1;
        public double RenderAccentStrokeScale { get; set; } = 1;
        public double RenderAccentIndentScale { get; set; } = 1;
        public double RenderOutlineScale { get; set; } = 1;
        public string RenderFfmpegArguments { get; set; } = "[]";
    }
}
