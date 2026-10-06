using CommandLine;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("streamdownload", HelpText = "Downloads a stream live from Twitch")]
    internal sealed class StreamDownloadArgs : IFileCollisionArgs, ITwitchDownloaderArgs
    {
        [Option('u', "login", Required = true, HelpText = "The login of the target channel.")]
        public string ChannelLogin { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file. File extension will be used to determine download type. Valid extensions are: .mp4 and .m4a.")]
        public string OutputFile { get; set; }

        [Option('q', "quality", HelpText = "The quality the program will attempt to download.")]
        public string Quality { get; set; }

        [Option('t', "threads", Default = 4, HelpText = "Number of parallel download threads. Large values may result in IP rate limiting.")]
        public int DownloadThreads { get; set; }

        // Use string instead of TimeDuration to allow the user to specify 0 but still have a non-zero default value
        [Option('w', "end-wait", Default = "1m", HelpText = "Amount of time to wait after the end of stream before finalizing the video. If another stream starts during that time, the download continues and both streams are concatenated. Default is one minute. Can be milliseconds (#ms), seconds (#s), minutes (#m), hours (#h), or time (##:##:##).")]
        public string StreamEndWaitTime { get; set; }

        [Option('d', "delay-download", HelpText = "Wait until the channel goes live.")]
        public bool DelayDownload { get; set; }

        [Option("oauth", HelpText = "OAuth access token to download subscriber only streams or access higher video qualities. DO NOT SHARE THIS WITH ANYONE.")]
        public string Oauth { get; set; }

        [Option("ffmpeg-path", HelpText = "Path to FFmpeg executable.")]
        public string FfmpegPath { get; set; }

        [Option("temp-path", Default = "", HelpText = "Path to temporary caching folder.")]
        public string TempFolder { get; set; }

        // Interface args
        public OverwriteBehavior OverwriteBehavior { get; set; }
        public bool? ShowBanner { get; set; }
        public LogLevel LogLevel { get; set; }
    }
}
