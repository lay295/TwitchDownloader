using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{

    [Verb("cache", HelpText = "Manage the working cache")]
    public class CacheArgs
    {
        [Option('c', "clear", Default = false, Required = false, HelpText = "Clears the default cache folder.")]
        public bool ClearCache { get; set; }

        [Option("force-clear", Default = false, Required = false, HelpText = "Clears the default cache folder, bypassing the confirmation prompt")]
        public bool ForceClearCache { get; set; }
    }
}
