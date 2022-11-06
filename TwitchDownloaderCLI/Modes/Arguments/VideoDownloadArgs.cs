using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{

    [Verb("videodownload", HelpText = "Downloads a stream VOD from Twitch")]
    public class VideoDownloadArgs
    {
        [Option('u', "id", Required = true, HelpText = "The ID of the VOD to download.")]
        public string Id { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file.")]
        public string OutputFile { get; set; }

        [Option('q', "quality", HelpText = "The quality the program will attempt to download.")]
        public string Quality { get; set; }

        [Option('b', "beginning", HelpText = "Time in seconds to crop beginning.")]
        public int CropBeginningTime { get; set; }
        
        [Option('e', "ending", HelpText = "Time in seconds to crop ending.")]
        public int CropEndingTime { get; set; }
        
        [Option('t', "threads", Default = 10, HelpText = "Number of download threads.")]
        public int DownloadThreads { get; set; }
        
        [Option('a', "oauth", HelpText = "OAuth to access subscriber only/unpublished VODs.")]
        public string Oauth { get; set; }

        [Option("ffmpeg-path", HelpText = "Path to ffmpeg executable.")]
        public string FfmpegPath { get; set; }

        [Option("cache-path", Default = "", HelpText = "Path to temporary caching folder.")]
        public string TempFolder { get; set; }
    }
}
