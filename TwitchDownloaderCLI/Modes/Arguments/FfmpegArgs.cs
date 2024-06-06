using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("ffmpeg", HelpText = "Manage standalone ffmpeg")]
    internal sealed class FfmpegArgs : ITwitchDownloaderArgs
    {
        [Option('d', "download", Default = false, Required = false, HelpText = "Downloads FFmpeg as a standalone file.")]
        public bool DownloadFfmpeg { get; set; }

        // Interface args
        public bool? ShowBanner { get; set; }
        public LogLevel LogLevel { get; set; }
    }
}
