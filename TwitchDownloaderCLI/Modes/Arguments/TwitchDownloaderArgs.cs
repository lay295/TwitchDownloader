using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    internal abstract class TwitchDownloaderArgs
    {
        [Option("banner", Default = true, HelpText = "Displays a banner containing version and copyright information.")]
        public bool? ShowBanner { get; set; }

        [Option("log-level", Default = LogLevel.Info | LogLevel.Warning | LogLevel.Error, HelpText = $"Sets the log level flags. Applicable values are: {nameof(LogLevel.None)}, {nameof(LogLevel.Verbose)}, {nameof(LogLevel.Info)}, {nameof(LogLevel.Warning)}, {nameof(LogLevel.Error)}, {nameof(LogLevel.Ffmpeg)}")]
        public LogLevel LogLevel { get; set; }
    }
}