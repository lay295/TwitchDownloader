﻿using CommandLine;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCLI.Modes.Arguments
{
    [Verb("chatdownload", HelpText = "Downloads the chat from a VOD or clip")]
    internal sealed class ChatDownloadArgs : TwitchDownloaderArgs
    {
        [Option('u', "id", Required = true, HelpText = "The ID or URL of the VOD or clip to download that chat of.")]
        public string Id { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output file. File extension will be used to determine download type. Valid extensions are: .json, .html, and .txt.")]
        public string OutputFile { get; set; }

        [Option("compression", Default = ChatCompression.None, HelpText = "Compresses an output json chat file using a specified compression, usually resulting in 40-90% size reductions. Valid values are: None, Gzip.")]
        public ChatCompression Compression { get; set; }

        [Option('b', "beginning", HelpText = "Time to trim beginning. Can be milliseconds (#ms), seconds (#s), minutes (#m), hours (#h), or time (##:##:##).")]
        public TimeDuration TrimBeginningTime { get; set; }

        [Option('e', "ending", HelpText = "Time to trim ending. Can be milliseconds (#ms), seconds (#s), minutes (#m), hours (#h), or time (##:##:##).")]
        public TimeDuration TrimEndingTime { get; set; }

        [Option('E', "embed-images", Default = false, HelpText = "Embed first party emotes, badges, and cheermotes into the chat download for offline rendering.")]
        public bool EmbedData { get; set; }

        [Option("bttv", Default = true, HelpText = "Enable BTTV embedding in chat download. Requires -E / --embed-images!")]
        public bool? BttvEmotes { get; set; }

        [Option("ffz", Default = true, HelpText = "Enable FFZ embedding in chat download. Requires -E / --embed-images!")]
        public bool? FfzEmotes { get; set; }

        [Option("stv", Default = true, HelpText = "Enable 7TV embedding in chat download. Requires -E / --embed-images!")]
        public bool? StvEmotes { get; set; }

        [Option("timestamp-format", Default = TimestampFormat.Relative, HelpText = "Sets the timestamp format for .txt chat logs. Valid values are: Utc, UtcFull, Relative, and None")]
        public TimestampFormat TimeFormat { get; set; }

        [Option('t', "threads", Default = 4, HelpText = "Number of parallel download threads. Large values may result in IP rate limiting.")]
        public int DownloadThreads { get; set; }

        [Option("silent", Default = false, HelpText = "Suppresses progress console output")]
        public bool Silent { get; set; }

        [Option("temp-path", Default = "", HelpText = "Path to temporary folder to use for cache.")]
        public string TempFolder { get; set; }
    }
}