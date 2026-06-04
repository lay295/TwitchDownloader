using System;
using System.IO;

namespace TwitchDownloaderCore.Options
{
    public class LiveChatRecorderOptions
    {
        public string Channel { get; set; }
        public string OutputFile { get; set; }
        public bool NextStream { get; set; }
        public TimeSpan Duration { get; set; }
        public Func<FileInfo, FileInfo> FileCollisionCallback { get; set; } = info => info;

    }
}