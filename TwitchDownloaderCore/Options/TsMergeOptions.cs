using System;
using System.IO;

namespace TwitchDownloaderCore.Options
{
    public class TsMergeOptions
    {
        public string OutputFile { get; set; }
        public string InputFile { get; set; }
        public Func<FileInfo, FileInfo> FileCollisionCallback { get; set; } = info => info;
    }
}
