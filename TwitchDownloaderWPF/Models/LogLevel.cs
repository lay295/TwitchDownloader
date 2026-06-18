using System;

namespace TwitchDownloaderWPF.Models
{
    [Flags]
    internal enum LogLevel
    {
        None = 0,
        Verbose = 1 << 0,
        Info = 1 << 1,
        Warning = 1 << 2,
        Error = 1 << 3,
        Ffmpeg = 1 << 4,
    }
}