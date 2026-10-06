namespace TwitchDownloaderAvalonia.Models
{
    public sealed class ChatRenderSettings
    {
        public string Font { get; set; } = "Inter Embedded";
        public double FontSize { get; set; } = 24;
        public int Width { get; set; } = 700;
        public int Height { get; set; } = 1200;
        public bool Outline { get; set; }
        public bool Timestamp { get; set; }
        public string BackgroundColor { get; set; } = "#FF111111";
        public string AlternateBackgroundColor { get; set; } = "#FF191919";
        public string FontColor { get; set; } = "#FFFFFFFF";
        public string HighlightUsersColor { get; set; } = "#C8FF0064";
        public double UpdateTime { get; set; } = 0.2;
        public int Framerate { get; set; } = 60;
        public bool GenerateMask { get; set; }
        public bool Sharpening { get; set; }
        public bool SubMessages { get; set; } = true;
        public bool ChatBadges { get; set; } = true;
        public bool Offline { get; set; }
        public bool UserAvatars { get; set; }
        public bool DisperseCommentOffsets { get; set; } = true;
        public bool AlternateMessageBackgrounds { get; set; }
        public bool AdjustUsernameVisibility { get; set; } = true;
        public string VideoContainer { get; set; } = "MP4";
        public string VideoCodec { get; set; } = "H264";
        public string IgnoreUsersList { get; set; } = string.Empty;
        public string HighlightUsersList { get; set; } = string.Empty;
        public string BannedWordsList { get; set; } = string.Empty;
        public int EmojiVendor { get; set; } = (int)TwitchDownloaderCore.Chat.EmojiVendor.GoogleNotoColor;
        public int ChatBadgeMask { get; set; }
        public double EmoteScale { get; set; } = 1;
        public double EmojiScale { get; set; } = 1;
        public double BadgeScale { get; set; } = 1;
        public double AvatarScale { get; set; } = 1;
        public double VerticalSpacingScale { get; set; } = 1;
        public double UsernameFontScale { get; set; } = 1;
        public double SidePaddingScale { get; set; } = 1;
        public double SectionHeightScale { get; set; } = 1;
        public double WordSpacingScale { get; set; } = 1;
        public double EmoteSpacingScale { get; set; } = 1;
        public double AccentStrokeScale { get; set; } = 1;
        public double AccentIndentScale { get; set; } = 1;
        public double OutlineScale { get; set; } = 1;
        public string FfmpegArguments { get; set; } = "[]";
    }
}
