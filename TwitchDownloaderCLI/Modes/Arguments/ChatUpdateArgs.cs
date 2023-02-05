using CommandLine;
using TwitchDownloaderCore.Chat;

namespace TwitchDownloaderCLI.Modes.Arguments
{

    [Verb("chatupdate", HelpText = "Updates the embeded emotes, badges, bits, and crops of a chat download and/or converts a JSON chat to another format.")]
    public class ChatUpdateArgs
    {
        [Option('i', "input", Required = true, HelpText = "Path to input file. Valid extensions are: .json.")]
        public string InputFile { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file. File extension will be used to determine new chat type. Valid extensions are: .json, .html, and .txt.")]
        public string OutputFile { get; set; }

        [Option('c', "compression", Default = ChatCompression.None, HelpText = "Compresses an output json chat file using a specified compression, usually resulting in 40-90% size reductions. Valid values are: None, Gzip.")]
        public ChatCompression Compression { get; set; }

        [Option('E', "embed-missing", Default = false, HelpText = "Embeds missing emotes, badges, and cheermotes. Already embedded images will be untouched.")]
        public bool EmbedMissing { get; set; }

        [Option('R', "replace-embeds", Default = false, HelpText = "Replace all embedded emotes, badges, and cheermotes in the file. All embedded images will be overwritten!")]
        public bool ReplaceEmbeds { get; set; }

        [Option('b', "beginning", Default = -1, HelpText = "New time in seconds for chat beginning. Comments may be added but not removed. -1 = No crop.")]
        public int CropBeginningTime { get; set; }

        [Option('e', "ending", Default = -1, HelpText = "New time in seconds for chat ending. Comments may be added but not removed. -1 = No crop.")]
        public int CropEndingTime { get; set; }

        [Option("bttv", Default = true, HelpText = "Enable BTTV embedding in chat download.")]
        public bool? BttvEmotes { get; set; }

        [Option("ffz", Default = true, HelpText = "Enable FFZ embedding in chat download.")]
        public bool? FfzEmotes { get; set; }

        [Option("stv", Default = true, HelpText = "Enable 7TV embedding in chat download.")]
        public bool? StvEmotes { get; set; }

        [Option("timestamp-format", Default = TimestampFormat.Relative, HelpText = "Sets the timestamp format for .txt chat logs. Valid values are: Utc, Relative, and None")]
        public TimestampFormat TimeFormat { get; set; }

        [Option("temp-path", Default = "", HelpText = "Path to temporary folder to use for cache.")]
        public string TempFolder { get; set; }
    }
}
