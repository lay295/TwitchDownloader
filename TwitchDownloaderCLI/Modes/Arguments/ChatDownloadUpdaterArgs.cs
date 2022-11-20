using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{

    [Verb("chatupdate", HelpText = "Updates the embeded emotes, badges, and bits of a chat download.")]
    public class ChatDownloadUpdaterArgs
    {
        [Option('i', "input", Required = true, HelpText = "Path to input file. Valid extensions are json")]
        public string InputFile { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file. Extension should match the input.")]
        public string OutputFile { get; set; }

        [Option('E', "embed-missing", Default = true, HelpText = "Embed missing emotes, badges, and bits. Already embedded images will be untouched")]
        public bool EmbedMissing { get; set; }

        [Option('U', "update-old", Default = false, HelpText = "Update old emotes, badges, and bits to the current. All embedded images will be overwritten")]
        public bool UpdateOldEmbeds { get; set; }

        [Option("bttv", Default = true, HelpText = "Enable BTTV embedding in chat download.")]
        public bool BttvEmotes { get; set; }

        [Option("ffz", Default = true, HelpText = "Enable FFZ embedding in chat download.")]
        public bool FfzEmotes { get; set; }

        [Option("stv", Default = true, HelpText = "Enable 7tv embedding in chat download.")]
        public bool StvEmotes { get; set; }

        [Option("temp-path", Default = "", HelpText = "Path to temporary folder to use for cache.")]
        public string TempFolder { get; set; }
    }
}
