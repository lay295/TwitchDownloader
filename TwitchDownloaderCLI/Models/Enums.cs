using System;

namespace TwitchDownloaderCLI.Models
{
    [Flags]
    internal enum LogLevel
    {
        All = Status | Verbose | Info | Warning | Error | Ffmpeg,
        None = 1 << 0,
        Status = 1 << 1,
        Verbose = 1 << 2,
        Info = 1 << 3,
        Warning = 1 << 4,
        Error = 1 << 5,
        Ffmpeg = 1 << 6,
    }

    public enum OverwriteBehavior
    {
        Overwrite,
        Exit,
        Rename,
        Prompt,
    }

    public enum InfoPrintFormat
    {
        Raw,
        Table,
        M3U8,
        M3U = M3U8,
        Json
    }
}