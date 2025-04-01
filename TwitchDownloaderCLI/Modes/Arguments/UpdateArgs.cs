using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("update", HelpText = "Manages updating the CLI")]
    internal sealed class UpdateArgs : ITwitchDownloaderArgs
    {
        [Option('c', "check", Default = true, Required = false, HelpText = "Checks whether a new version of the CLI is available")]
        public bool CheckForUpdate { get; set; }

        // Interface args
        public bool? ShowBanner { get; set; }
        public LogLevel LogLevel { get; set; }
    }
}
