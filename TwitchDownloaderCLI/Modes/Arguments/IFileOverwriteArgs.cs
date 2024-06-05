using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    internal interface IFileOverwriteArgs
    {
        [Option("overwrite", Default = OverwriteBehavior.Prompt, HelpText = ". Valid values are: Overwrite, Exit, Rename, Prompt.")]
        public OverwriteBehavior OverwriteBehavior { get; set; }
    }
}