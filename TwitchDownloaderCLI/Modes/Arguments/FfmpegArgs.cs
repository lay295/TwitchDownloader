using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{

    [Verb("ffmpeg", HelpText = "Manage standalone ffmpeg")]
    public class FfmpegArgs
    {
        [Option('d', "download", Default = false, Required = false, HelpText = "Downloads FFmpeg as a standalone file.")]
        public bool DownloadFfmpeg { get; set; }
    }
}
