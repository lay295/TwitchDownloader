using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("streaminfo", HelpText = "Prints stream information about a VOD or clip to stdout")]
    internal sealed class StreamInfoArgs : ITwitchDownloaderArgs
    {
        [Option('u', "id", Required = true, HelpText = "The ID or URL of the VOD or clip to print the stream info about.")]
        public string Id { get; set; }

        [Option("format", Default = StreamInfoPrintFormat.Table, HelpText = "The format in which the information should be printed. When using table format, use a terminal that supports ANSI escape sequences for best results. Valid values are: Raw, Table, and M3U/M3U8")]
        public StreamInfoPrintFormat Format { get; set; }

        [Option("oauth", HelpText = "OAuth access token to access subscriber only VODs. DO NOT SHARE THIS WITH ANYONE.")]
        public string Oauth { get; set; }

        // Interface args
        public bool? ShowBanner { get; set; }
        public LogLevel LogLevel { get; set; }
    }
}