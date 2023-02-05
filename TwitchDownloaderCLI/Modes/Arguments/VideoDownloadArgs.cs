using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{

    [Verb("videodownload", HelpText = "Downloads a stream VOD from Twitch")]
    public class VideoDownloadArgs
    {
        [Option('u', "id", Required = true, HelpText = "The ID or URL of the VOD to download.")]
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
        
        [Option("oauth", HelpText = "OAuth access token to download subscriber only VODs. DO NOT SHARE THIS WITH ANYONE.")]
        public string Oauth { get; set; }

        [Option("ffmpeg-path", HelpText = "Path to ffmpeg executable.")]
        public string FfmpegPath { get; set; }

        [Option("temp-path", Default = "", HelpText = "Path to temporary caching folder.")]
        public string TempFolder { get; set; }
    }
}
