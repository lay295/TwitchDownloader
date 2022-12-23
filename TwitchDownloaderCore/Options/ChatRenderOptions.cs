using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

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
        public double ReferenceScale => FontSize / 24;
        public int SectionHeight => (int)(40 * ReferenceScale);
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
                    return (int)(UpdateRate * Framerate);
            }
        }
        public bool GenerateMask { get; set; }
        public string MaskFile
        {
            get
            {
                if (OutputFile == "" || GenerateMask == false)
                    return OutputFile;

                string extension = Path.GetExtension(OutputFile);
                int extensionIndex = OutputFile.LastIndexOf(extension);
                return string.Concat(OutputFile.AsSpan(0, extensionIndex), "_mask", extension);
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
        public int SidePadding => (int)(6 * ReferenceScale);
        public int VerticalPadding => (int)(24 * ReferenceScale);
        public int WordSpacing => (int)(6 * ReferenceScale);
        public int EmoteSpacing => (int)(6 * ReferenceScale);
        public int AscentStrokeWidth => (int)(12 * ReferenceScale);
        public int AscentIndentWidth => (int)(24 * ReferenceScale);
        public bool Offline { get; set; }
        public bool LogFfmpegOutput { get; set; } = false;
        public bool PreWrapBlockArt { get; set; } = true;
    }
}
