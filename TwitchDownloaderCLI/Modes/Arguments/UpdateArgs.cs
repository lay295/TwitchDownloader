using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("update", HelpText = "Manage updating the CLI")]
    internal sealed class UpdateArgs : ITwitchDownloaderArgs
    {
        [Option('f', "force", Default = false, Required = false, HelpText = "Bypasses the confirmation prompt")]
        public bool ForceUpdate { get; set; }

        // Interface args
        public bool? ShowBanner { get; set; }
        public LogLevel LogLevel { get; set; }
    }
}
