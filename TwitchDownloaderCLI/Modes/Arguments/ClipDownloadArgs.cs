using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{

    [Verb("clipdownload", HelpText = "Downloads a clip from Twitch")]
    public class ClipDownloadArgs
    {
        [Option('u', "id", Required = true, HelpText = "The ID or URL of the clip to download.")]
        public string Id { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file.")]
        public string OutputFile { get; set; }

        [Option('q', "quality", HelpText = "The quality the program will attempt to download.")]
        public string Quality { get; set; }
    }
}