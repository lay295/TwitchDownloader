using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Options
{
    public class ChatRenderOptions
    {
        public string InputFile { get; set; }
        public string OutputFile { get; set; }
        public SKColor BackgroundColor { get; set; }
        public SKColor MessageColor { get; set; }
        public int ChatHeight { get; set; }
        public int ChatWidth { get; set; }
        public bool BttvEmotes { get; set; }
        public bool FfzEmotes { get; set; }
        public bool StvEmotes { get; set; }
        public bool Outline { get; set; }
        public double OutlineSize { get; set; }
        public string Font { get; set; }
        public double FontSize { get; set; } = 24.0;
        public SKFontStyle MessageFontStyle { get; set; }
        public SKFontStyle UsernameFontStyle { get; set; }
        public double ReferenceScale
        {
            get
            {
                return FontSize / 24;
            }
        }
        public int SectionHeight
        {
            get
            {
                return (int)Math.Floor(40 * ReferenceScale);
            }
        }
        public bool Timestamp { get; set; }
        public int Framerate { get; set; }
        public double UpdateRate { get; set; }
        public int UpdateFrame
        {
            get
            {
                if (UpdateRate == 0)
                    return 1;
                else
                    return (int)Math.Floor(UpdateRate / (1.0 / Framerate));
            }
        }
        public bool GenerateMask { get; set; }
        public string OutputFileMask
        {
            get
            {
                if (OutputFile == "" || GenerateMask == false)
                    return OutputFile;

                string extension = Path.GetExtension(OutputFile);
                int lastIndex = OutputFile.LastIndexOf(extension);
                return OutputFile.Substring(0, lastIndex) + "_mask" + extension;
            }
        }
        public string InputArgs { get; set; }
        public string OutputArgs { get; set; }
        public string FfmpegPath { get; set; }
        public string TempFolder { get; set; }
        public bool SubMessages { get; set; }
        public bool ChatBadges { get; set; }
        public List<string> IgnoreUsersList { get; set; } = new List<string>();
        public double EmoteScale { get; set; } = 1.0;
        public int RenderThreads { get; set; } = 1;
        public int ChatBadgeMask { get; set; } = 0;
        public int StartOverride { get; set; } = -1;
        public int EndOverride { get; set; } = -1;
        public int SidePadding
        {
            get
            {
                return (int)(6 * ReferenceScale);
            }
        }
        public int VerticalPadding
        {
            get
            {
                return (int)(24 * ReferenceScale);
            }
        }
        public int WordSpacing
        {
            get
            {
                return (int)(6 * ReferenceScale);
            }
        }
        public int EmoteSpacing
        {
            get
            {
                return (int)(6 * ReferenceScale);
            }
        }
        public int AscentStrokeWidth
        {
            get
            {
                return (int)(12 * ReferenceScale);
            }
        }
        public int AscentIndentWidth
        {
            get
            {
                return (int)(24 * ReferenceScale);
            }
        }
        public bool Offline { get; set; }
    }
}
