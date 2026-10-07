namespace TwitchDownloaderWPF.Models
{
    public class ChatRenderPreset
    {
        public string Name { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Font { get; set; }
        public double FontSize { get; set; }
        public int BackgroundColorA { get; set; }
        public int BackgroundColorR { get; set; }
        public int BackgroundColorG { get; set; }
        public int BackgroundColorB { get; set; }
        public int AltBackgroundColorA { get; set; }
        public int AltBackgroundColorR { get; set; }
        public int AltBackgroundColorG { get; set; }
        public int AltBackgroundColorB { get; set; }
        public int MessageColorR { get; set; }
        public int MessageColorG { get; set; }
        public int MessageColorB { get; set; }
        public bool Outline { get; set; }
        public bool Timestamp { get; set; }
        public bool Bttv { get; set; } = true;
        public bool Ffz { get; set; } = true;
        public bool Stv { get; set; } = true;
        public bool SubMessages { get; set; }
        public bool Badges { get; set; }
        public bool RenderAvatars { get; set; }
        public bool Offline { get; set; }
        public bool Dispersion { get; set; }
        public bool AlternateBackgrounds { get; set; }
        public bool AdjustUsernameVisibility { get; set; }
        public double UpdateRate { get; set; }
        public int Framerate { get; set; }
        public bool GenerateMask { get; set; }
        public bool ChatRenderSharpening { get; set; }
        public double EmoteScale { get; set; }
        public double BadgeScale { get; set; }
        public double EmojiScale { get; set; }
        public double AvatarScale { get; set; }
        public double VerticalScale { get; set; }
        public double SidePaddingScale { get; set; }
        public double SectionHeightScale { get; set; }
        public double WordSpaceScale { get; set; }
        public double EmoteSpaceScale { get; set; }
        public double AccentStrokeScale { get; set; }
        public double AccentIndentScale { get; set; }
        public double OutlineScale { get; set; }
        public string IgnoreUsers { get; set; }
        public string BannedWords { get; set; }
        public int EmojiVendor { get; set; }
        public int ChatBadgeMask { get; set; }
        public string FfmpegInput { get; set; }
        public string FfmpegOutput { get; set; }
        public string VideoContainer { get; set; }
        public string VideoCodec { get; set; }
    }
}
