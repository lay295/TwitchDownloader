using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("update", HelpText = "Manages updating the CLI")]
    internal sealed class UpdateArgs : ITwitchDownloaderArgs
    {
        [Option('f', "force", Default = false, Required = false, HelpText = "Skips the yes/no prompt for updating")]
        public bool ForceUpdate { get; set; }

        // Interface args
        public bool? ShowBanner { get; set; }
        public LogLevel LogLevel { get; set; }
    }
}
