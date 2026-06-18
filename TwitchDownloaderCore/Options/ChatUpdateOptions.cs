using System;
using System.IO;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Options
{
    public class ChatUpdateOptions
    {
        public string InputFile { get; set; }
        public string OutputFile { get; set; }
        public ChatCompression Compression { get; set; } = ChatCompression.None;
        public ChatFormat OutputFormat { get; set; } = ChatFormat.Json;
        public bool EmbedMissing { get; set; }
        public bool ReplaceEmbeds { get; set; }
        public bool TrimBeginning { get; set; }
        public double TrimBeginningTime { get; set; }
        public bool TrimEnding { get; set; }
        public double TrimEndingTime { get; set; }
        public bool BttvEmotes { get; set; }
        public bool FfzEmotes { get; set; }
        public bool StvEmotes { get; set; }
        public TimestampFormat TextTimestampFormat { get; set; }
        public string FileExtension
        {
            get
            {
                return string.Concat(
                    OutputFormat switch
                    {
                        ChatFormat.Json => ".json",
                        ChatFormat.Html => ".html",
                        ChatFormat.Text => ".txt",
                        _ => ""
                    },
                    Compression switch
                    {
                        ChatCompression.None => "",
                        ChatCompression.Gzip => ".gz",
                        _ => ""
                    }
                );
            }
        }
        public string TempFolder { get; set; }
        public Func<FileInfo, FileInfo> FileCollisionCallback { get; set; } = info => info;
    }
}
