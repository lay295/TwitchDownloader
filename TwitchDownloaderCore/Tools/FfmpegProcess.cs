using System.Diagnostics;

namespace TwitchDownloaderCore.Tools
{
    public class FfmpegProcess
    {
        public Process Process { get; init; }
        public string SavePath { get; private set; }

        public FfmpegProcess() { }
        public FfmpegProcess(Process process, string savePath)
        {
            Process = process;
            SavePath = savePath;
        }

        ~FfmpegProcess()
        {
            SavePath = null;
            Process.Dispose();
        }
    }
}
