using CommandLine;

namespace TwitchDownloaderCLI.Models
{
    public interface IFileOverwriteArgs
    {
        [Option("overwrite", Default = OverwriteBehavior.Prompt, HelpText = ". Valid values are: Overwrite, Exit, Rename, Prompt.")]
        public OverwriteBehavior OverwriteBehavior { get; set; }
    }
}