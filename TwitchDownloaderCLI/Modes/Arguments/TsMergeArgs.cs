using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("tsmerge", HelpText = "Concatenates (not binary) .ts/.tsv/.tsa/.m2t/.m2ts (MPEG Transport Stream) parts into another file")]
    public class TsMergeArgs : ITwitchDownloaderArgs
    {
        [Option('l', "inputlist", Required = true, HelpText = "Path to input list file in text format (one part per line).")]
        public string InputList { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file.")]
        public string OutputFile { get; set; }

        [Option("banner", Default = true, HelpText = "Displays a banner containing version and copyright information.")]
        public bool? ShowBanner { get; set; }
    }
}
