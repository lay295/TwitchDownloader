using System;
using System.IO;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Options
{
    public class ChatDownloadOptions
    {
        public ChatFormat DownloadFormat { get; set; } = ChatFormat.Json;
        public string Id { get; set; }
        public string Filename { get; set; }
        public ChatCompression Compression { get; set; } = ChatCompression.None;
        public bool TrimBeginning { get; set; }
        public double TrimBeginningTime { get; set; }
        public bool TrimEnding { get; set; }
        public double TrimEndingTime { get; set; }
        public bool EmbedData { get; set; }
        public bool BttvEmotes { get; set; }
        public bool FfzEmotes { get; set; }
        public bool StvEmotes { get; set; }
        public int DownloadThreads { get; set; } = 1;
        public TimestampFormat TimeFormat { get; set; }
        public string FileExtension
        {
            get
            {
                return string.Concat(
                    DownloadFormat switch
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
        public bool DelayDownload { get; set; }
    }
}
