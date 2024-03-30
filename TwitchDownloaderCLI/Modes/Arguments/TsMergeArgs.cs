using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("tsmerge", HelpText = "Concatenates multiple .ts/.tsv/.tsa/.m2t/.m2ts (MPEG Transport Stream) files into a single file")]
    internal sealed class TsMergeArgs : TwitchDownloaderArgs
    {
        [Option('i', "input", Required = true, HelpText = "Path a text file containing the absolute paths of the files to concatenate, separated by newlines. M3U/M3U8 is also supported.")]
        public string InputList { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file.")]
        public string OutputFile { get; set; }
    }
}
