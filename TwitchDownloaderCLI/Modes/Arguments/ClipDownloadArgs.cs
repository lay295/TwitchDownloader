using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("clipdownload", HelpText = "Downloads a clip from Twitch")]
    internal sealed class ClipDownloadArgs : TwitchDownloaderArgs
    {
        [Option('u', "id", Required = true, HelpText = "The ID or URL of the clip to download.")]
        public string Id { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file.")]
        public string OutputFile { get; set; }

        [Option('q', "quality", HelpText = "The quality the program will attempt to download.")]
        public string Quality { get; set; }

        [Option("bandwidth", Default = -1, HelpText = "The maximum bandwidth the clip downloader is allowed to use in kibibytes per second (KiB/s), or -1 for no maximum.")]
        public int ThrottleKib { get; set; }

        [Option("encode-metadata", Default = true, HelpText = "Uses FFmpeg to add metadata to the clip output file.")]
        public bool? EncodeMetadata { get; set; }

        [Option("ffmpeg-path", HelpText = "Path to FFmpeg executable.")]
        public string FfmpegPath { get; set; }

        [Option("temp-path", Default = "", HelpText = "Path to temporary caching folder.")]
        public string TempFolder { get; set; }
    }
}