using System;
using System.IO;

namespace TwitchDownloaderCore.Options
{
    public class LiveChatRecorderOptions
    {
        public string Channel { get; set; }
        public string OutputFile { get; set; }
        public event EventHandler StopRecording;
        public Func<FileInfo, FileInfo> FileCollisionCallback { get; set; } = info => info;

        public void OnStopRecording()
        {
            StopRecording?.Invoke(this, EventArgs.Empty);
        }
    }
}