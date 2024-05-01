using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("videodownload", HelpText = "Downloads a stream VOD from Twitch")]
    internal sealed class VideoDownloadArgs : TwitchDownloaderArgs
    {
        [Option('u', "id", Required = true, HelpText = "The ID or URL of the VOD to download.")]
        public string Id { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file. File extension will be used to determine download type. Valid extensions are: .mp4 and .m4a.")]
        public string OutputFile { get; set; }

        [Option('q', "quality", HelpText = "The quality the program will attempt to download.")]
        public string Quality { get; set; }

        [Option('b', "beginning", HelpText = "Time to trim beginning. Can be milliseconds (#ms), seconds (#s), minutes (#m), hours (#h), or time (##:##:##).")]
        public TimeDuration TrimBeginningTime { get; set; }

        [Option('e', "ending", HelpText = "Time to trim ending. Can be milliseconds (#ms), seconds (#s), minutes (#m), hours (#h), or time (##:##:##).")]
        public TimeDuration TrimEndingTime { get; set; }

        [Option('t', "threads", Default = 4, HelpText = "Number of download threads.")]
        public int DownloadThreads { get; set; }

        [Option("bandwidth", Default = -1, HelpText = "The maximum bandwidth a thread will be allowed to use in kibibytes per second (KiB/s), or -1 for no maximum.")]
        public int ThrottleKib { get; set; }

        [Option("oauth", HelpText = "OAuth access token to download subscriber only VODs. DO NOT SHARE THIS WITH ANYONE.")]
        public string Oauth { get; set; }

        [Option("ffmpeg-path", HelpText = "Path to FFmpeg executable.")]
        public string FfmpegPath { get; set; }

        [Option("temp-path", Default = "", HelpText = "Path to temporary caching folder.")]
        public string TempFolder { get; set; }
    }
}
