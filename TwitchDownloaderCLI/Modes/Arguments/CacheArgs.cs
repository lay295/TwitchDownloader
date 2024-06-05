using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("cache", HelpText = "Manage the working cache")]
    internal sealed class CacheArgs : ITwitchDownloaderArgs
    {
        [Option('c', "clear", Default = false, Required = false, HelpText = "Clears the default cache folder.")]
        public bool ClearCache { get; set; }

        [Option("force-clear", Default = false, Required = false, HelpText = "Clears the default cache folder, bypassing the confirmation prompt")]
        public bool ForceClearCache { get; set; }

        // Interface args
        public bool? ShowBanner { get; set; }
        public LogLevel LogLevel { get; set; }
    }
}
