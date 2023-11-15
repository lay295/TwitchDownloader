using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("ffmpeg", HelpText = "Manage standalone ffmpeg")]
    public class FfmpegArgs : ITwitchDownloaderArgs
    {
        [Option('d', "download", Default = false, Required = false, HelpText = "Downloads FFmpeg as a standalone file.")]
        public bool DownloadFfmpeg { get; set; }

        [Option("banner", Default = true, HelpText = "Displays a banner containing version and copyright information.")]
        public bool? ShowBanner { get; set; }
    }
}
