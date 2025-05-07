using System.Diagnostics;

namespace TwitchDownloaderCore.Models
{
    public sealed class FfmpegProcess : Process
    {
        public string SavePath { get; init; }
    }
}