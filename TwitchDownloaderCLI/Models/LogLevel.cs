using System;

namespace TwitchDownloaderCLI.Models
{
    [Flags]
    internal enum LogLevel
    {
        None = 1 << 0,
        Status = 1 << 1,
        Verbose = 1 << 2,
        Info = 1 << 3,
        Warning = 1 << 4,
        Error = 1 << 5,
        Ffmpeg = 1 << 6,
    }
}