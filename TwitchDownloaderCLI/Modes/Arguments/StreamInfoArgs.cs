using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("streaminfo", HelpText = "Prints stream information about a VOD or clip to stdout")]
    internal sealed class StreamInfoArgs : ITwitchDownloaderArgs
    {
        [Option('u', "id", Required = true, HelpText = "The ID or URL of the VOD or clip to print the stream info about.")]
        public string Id { get; set; }

        [Option("format", Default = PrintFormat.Table, HelpText = "The format in which the information should be printed. Valid values are: Raw, Table, M3U/M3U8, and JSON")]
        public PrintFormat Format { get; set; }

        [Option("oauth", HelpText = "OAuth access token to access subscriber only VODs. DO NOT SHARE THIS WITH ANYONE.")]
        public string Oauth { get; set; }

        [Option("banner", Default = true, HelpText = "Displays a banner containing version and copyright information.")]
        public bool? ShowBanner { get; set; }

        public enum PrintFormat
        {
            Raw,
            Table,
            M3U8,
            M3U = M3U8,
            Json
        }
    }
}