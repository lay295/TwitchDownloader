using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("chatrecord", HelpText = "Records the live chat of a streamer")]
    internal sealed class ChatRecorderArgs : IFileCollisionArgs, ITwitchDownloaderArgs
    {
        [Option('u', "channel", Required = true, HelpText = "The channel to record.")]
        public string Channel { get; set; }

        [Option('o', "output", Default = "irc.txt", HelpText = "Path to output file. Not yet implemented.")]
        public string OutputFile { get; set; }

        // Interface args
        public OverwriteBehavior OverwriteBehavior { get; set; }
        public bool? ShowBanner { get; set; }
        public LogLevel LogLevel { get; set; }
    }
}