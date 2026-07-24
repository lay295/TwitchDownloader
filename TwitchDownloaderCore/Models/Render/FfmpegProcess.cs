using System.Diagnostics;

namespace TwitchDownloaderCore.Models.Render
{
    public sealed class FfmpegProcess : Process
    {
        public string SavePath { get; init; }
    }
}