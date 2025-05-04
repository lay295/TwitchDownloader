using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("update", HelpText = "Manages updating the CLI")]
    internal sealed class UpdateArgs : ITwitchDownloaderArgs
    {
        [Option('f', "force", Default = false, HelpText = "Bypasses the confirmation prompt.")]
        public bool ForceUpdate { get; set; }

        [Option('k', "keep-update", Default = false, HelpText = "Retain the downloaded update zip file instead of deleting it after the update is finished.")]
        public bool KeepArchive { get; set; }

        // Interface args
        public bool? ShowBanner { get; set; }
        public LogLevel LogLevel { get; set; }
    }
}
