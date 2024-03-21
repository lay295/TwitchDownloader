using CommandLine;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("videodownload", HelpText = "Downloads a stream VOD from Twitch")]
    public class VideoDownloadArgs : ITwitchDownloaderArgs
    {
        [Option('u', "id", Required = true, HelpText = "The ID or URL of the VOD to download.")]
        public string Id { get; set; }

        [Option('o', "output", HelpText = "Path to output file. File extension will be used to determine download type. Valid extensions are: .mp4, .m4a.")]
        public string OutputFile { get; set; }

        [Option('q', "quality", HelpText = "The quality the program will attempt to download, like '1080p60'. If '-o' and '-q' are missing will be 'best'.")]
        public string Quality { get; set; }

        [Option('p', "parts", HelpText = "Only download playlist.m3u8, metadata.txt and .ts parts to cache folder, and exit. Overrides '-k', '-K', '-o'.")]
        public bool TsPartsOnly { get; set; }

        [Option('K', "cache", HelpText = "Keep entire cache folder. Overrides '-k'.")]
        public bool KeepCache { get; set; }

        [Option('k', "cache-noparts", HelpText = "Keep cache folder except .ts parts. Merged 'output.ts' is not considered a part.")]
        public bool KeepCacheNoParts { get; set; }

        [Option('F', "skip-storagecheck", HelpText = "Skip checking for free storage space.")]
        public bool SkipStorageCheck { get; set; }

        [Option('b', "beginning", HelpText = "Time in seconds where the crop of the ID begins. May break first GOP.")]
        public int CropBeginningTime { get; set; }

        [Option('e', "ending", HelpText = "Time in seconds where the crop of the ID ends. May break last GOP.")]
        public int CropEndingTime { get; set; }

        [Option("tbn", HelpText = "Set specific TBN (time base in AVStream) for output.")]
        public int SetTbnValue { get; set; }

        [Option('t', "threads", Default = 4, HelpText = "Number of simultaneous download threads.")]
        public int DownloadThreads { get; set; }

        [Option("bandwidth", Default = -1, HelpText = "The maximum bandwidth a thread will be allowed to use in kibibytes per second (KiB/s), or -1 for no maximum.")]
        public int ThrottleKib { get; set; }

        [Option('a', "oauth", HelpText = "OAuth access token to download subscriber only VODs. DO NOT SHARE THIS WITH ANYONE.")]
        public string Oauth { get; set; }

        [Option("ffmpeg-path", HelpText = "Path to FFmpeg executable.")]
        public string FfmpegPath { get; set; }

        [Option('c', "temp-path", Default = "", HelpText = "Set custom path to temp/cache folder instead of provided by system. Recommended for '-k', '-K', '-p'.")]
        public string TempFolder { get; set; }

        [Option("banner", Default = true, HelpText = "Displays a banner containing version and copyright information.")]
        public bool? ShowBanner { get; set; }
    }
}
