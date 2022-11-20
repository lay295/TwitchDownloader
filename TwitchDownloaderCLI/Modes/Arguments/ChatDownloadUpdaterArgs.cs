using CommandLine;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes.Arguments
{

    [Verb("chatdownloadupdater", HelpText = "Will update the embeded emotes, badges, and bits.")]
    public class ChatDownloadUpdaterArgs
    {
        [Option('i', "input", Required = true, HelpText = "Path to input file. Valid extensions are json and html")]
        public string InputFile { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file. Format should match the input.")]
        public string OutputFile { get; set; }

        [Option('E', "embed-missing-emotes", Default = true, HelpText = "Embed emotes any emotes that are missing (keeps old ones and only appends)")]
        public bool EmbedMissingEmotes { get; set; }

        [Option('U', "update-old-emotes", Default = false, HelpText = "Update old emotes to the current new ones (will overwrite them)")]
        public bool UpdateOldEmotes { get; set; }

        [Option("bttv", Default = true, HelpText = "Enable BTTV embedding in chat download.")]
        public bool BttvEmotes { get; set; }

        [Option("ffz", Default = true, HelpText = "Enable FFZ embedding in chat download.")]
        public bool FfzEmotes { get; set; }

        [Option("stv", Default = true, HelpText = "Enable 7tv embedding in chat download.")]
        public bool StvEmotes { get; set; }

    }
}
