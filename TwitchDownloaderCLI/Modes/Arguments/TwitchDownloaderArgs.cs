using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    internal abstract class TwitchDownloaderArgs
    {
        [Option("banner", Default = true, HelpText = "Displays a banner containing version and copyright information.")]
        public bool? ShowBanner { get; set; }

        [Option("log-level", Default = LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, HelpText = "Sets the log level flags. Applicable values are: None, Status, Verbose, Info, Warning, Error, Ffmpeg.")]
        public LogLevel LogLevel { get; set; }
    }
}