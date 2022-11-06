using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{

    [Verb("cache", HelpText = "Manage the working cache")]
    public class CacheArgs
    {
        [Option('c', "clear", Default = false, Required = false, HelpText = "Clears the default cache folder.")]
        public bool ClearCache { get; set; }
    }
}
