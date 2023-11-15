using System.Diagnostics;

namespace TwitchDownloaderCore.Tools
{
    public sealed class FfmpegProcess : Process
    {
        public string SavePath { get; init; }
    }
}