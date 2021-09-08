using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        public double FontSize { get; set; }
        public SKFontStyle MessageFontStyle { get; set; }
        public SKFontStyle UsernameFontStyle { get; set; }
        public double EmoteScale
        {
            get
            {
                return FontSize / 12;
            }
        }
        public int PaddingLeft { get; set; }
        public int SectionHeight
        {
            get
            {
                return (int)Math.Floor(22 * EmoteScale);
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
    }
}
