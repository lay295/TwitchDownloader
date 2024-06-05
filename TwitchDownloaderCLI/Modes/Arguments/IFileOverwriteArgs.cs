using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    internal interface IFileOverwriteArgs
    {
        // TODO: This is probably a bad arg name
        [Option("overwrite", Default = OverwriteBehavior.Prompt, HelpText = "Sets the handling of output file name collisions. Valid values are: Overwrite, Exit, Rename, Prompt.")]
        public OverwriteBehavior OverwriteBehavior { get; set; }
    }
}