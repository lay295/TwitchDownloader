using System;

namespace TwitchDownloaderCLI.Models
{
    [Flags]
    internal enum LogLevel
    {
        None = 1 << 0,
        Verbose = 1 << 1,
        Info = 1 << 2,
        Warning = 1 << 3,
        Error = 1 << 4,
        Ffmpeg = 1 << 5,
    }
}