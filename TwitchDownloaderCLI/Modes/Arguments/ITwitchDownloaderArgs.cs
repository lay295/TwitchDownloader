using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    internal interface ITwitchDownloaderArgs
    {
        [Option("banner", Default = true, HelpText = "Displays a banner containing version and copyright information.")]
        bool? ShowBanner { get; set; }

        [Option("log-level", Default = LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, HelpText = "Sets the log level flags. Applicable values are: None, Status, Verbose, Info, Warning, Error, Ffmpeg.")]
        LogLevel LogLevel { get; set; }
    }
}