using CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace TwitchDownloaderCLI
{
    public enum RunMode
    {
        VideoDownload,
        ClipDownload,
        ChatDownload,
        ChatRender
    }

    class Options
    {
        [Option('m', "mode", Required = true, HelpText = "Set the run mode for the program. Valid values are VideoDownload, ClipDownload, ChatDownload, and ChatRender.")]
        public RunMode RunMode { get; set; }
        [Option('u', "id", HelpText = "The ID of the VOD or clip to download.")]
        public string Id { get; set; }
        [Option('q', "quality", HelpText = "The quality the program will attempt to download.")]
        public string Quality { get; set; }
        [Option('o', "output", Required = true, HelpText = "Path to output file.")]
        public string OutputFile { get; set; }
        [Option('i', "input", HelpText = "Path to input file.")]
        public string InputFile { get; set; }
        [Option('b', "beginning", HelpText = "Time in seconds to crop beginning.")]
        public int CropBeginningTime { get; set; }
        [Option('e', "ending", HelpText = "Time in seconds to crop ending.")]
        public int CropEndingTime { get; set; }
        [Option('t', "threads", HelpText = "Number of download threads.", Default = 10)]
        public int DownloadThreads { get; set; }
        [Option("oauth", HelpText = "OAuth to be passed when downloading a VOD.")]
        public string Oauth { get; set; }
        [Option("timestamp", HelpText = "Enable timestamp for chat download in .txt format or chat render.")]
        public bool Timestamp { get; set; }
        [Option("embed-emotes", HelpText = "Embed emotes into chat download.")]
        public bool EmbedEmotes { get; set; }
        [Option("background-color", Default = "#111111", HelpText = "Color of background for chat render.")]
        public string BackgroundColor { get; set; }
        [Option("message-color", Default = "#ffffff", HelpText = "Color of messages for chat render.")]
        public string MessageColor { get; set; }
        [Option('h', "chat-height", Default = 600, HelpText = "Height of chat render.")]
        public int ChatHeight { get; set; }
        [Option('w', "chat-width", Default = 350, HelpText = "Width of chat render.")]
        public int ChatWidth { get; set; }
        [Option("bttv", Default = true, HelpText = "Enable BTTV emotes in chat render.")]
        public bool BttvEmotes { get; set; }
        [Option("ffz", Default = true, HelpText = "Enable FFZ emotes in chat render.")]
        public bool FfzEmotes { get; set; }
        [Option("outline", Default = false, HelpText = "Enable outline in chat render.")]
        public bool Outline { get; set; }
        [Option("sub-messages", Default = true, HelpText = "Enable sub messages.")]
        public bool SubMessages { get; set; }
        [Option("generate-mask", Default = false, HelpText = "Generates a mask file in addition to the regular chat file.")]
        public bool GenerateMask { get; set; }
        [Option("outline-size", Default = 4, HelpText = "Size of outline in chat render.")]
        public double OutlineSize { get; set; }
        [Option('f', "font", Default = "arial", HelpText = "Font to use in chat render.")]
        public string Font { get; set; }
        [Option("font-size", Default = 12, HelpText = "Size of font in chat render.")]
        public double FontSize { get; set; }
        [Option("message-fontstyle", Default = "normal", HelpText = "Font style to use for message. Valid values are normal, bold, and italic.")]
        public string MessageFontStyle { get; set; }
        [Option("username-fontstyle", Default = "bold", HelpText = "Font style to use for username. Valid values are normal, bold, and italic.")]
        public string UsernameFontStyle { get; set; }
        [Option("padding-left", Default = 2, HelpText = "Padding space to left of chat render.")]
        public int PaddingLeft { get; set; }
        [Option("framerate", Default = 30, HelpText = "Framerate of chat render output.")]
        public int Framerate { get; set; }
        [Option("update-rate", Default = 0.2, HelpText = "Time in seconds to update chat render output.")]
        public double UpdateRate { get; set; }
        [Option("input-args", Default = "-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt bgra -video_size {width}x{height} -i -", HelpText = "Input arguments for ffmpeg chat render.")]
        public string InputArgs { get; set; }
        [Option("output-args", Default = "-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"", HelpText = "Output arguments for ffmpeg chat render.")]
        public string OutputArgs { get; set; }
        [Option("download-ffmpeg", Required = false, HelpText = "Downloads ffmpeg and exits.")]
        public bool DownloadFfmpeg { get; set; }
        [Option("ffmpeg-path", HelpText = "Path to ffmpeg executable.")]
        public string FfmpegPath { get; set; }
        [Option("temp-path", Default = "", HelpText = "Path to temporary folder to use for cache.")]
        public string TempFolder { get; set; }
    }
}
