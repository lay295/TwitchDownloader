namespace TwitchDownloaderWPF.Models
{
    /// <summary>
    /// A full snapshot of all Live Monitor settings for one channel.
    /// </summary>
    public class LiveMonitorChannelSettings
    {
        public string Folder { get; set; } = "";
        public string Quality { get; set; } = "Source";
        public int DownloadMode { get; set; } = 0;
        public int PollSeconds { get; set; } = 60;
        public int Threads { get; set; } = 4;
        public bool DownloadChat { get; set; } = false;
        public bool AutoRender { get; set; } = false;
        /// <summary>
        /// Opt-in: when the channel is live but has "Store past broadcasts" disabled (so no published
        /// VOD exists), detect it via stream metadata and recover the hidden broadcast from Twitch's
        /// public CDN once the stream ends. Unofficial, best-effort, source quality only.
        /// </summary>
        public bool RecoverHiddenVods { get; set; } = false;
        public string RenderPreset { get; set; } = "";
        public bool TrimBeginning { get; set; } = false;
        public int TrimBeginningHour { get; set; } = 0;
        public int TrimBeginningMinute { get; set; } = 0;
        public int TrimBeginningSecond { get; set; } = 0;
        public bool TrimEnding { get; set; } = false;
        public int TrimEndingHour { get; set; } = 0;
        public int TrimEndingMinute { get; set; } = 0;
        public int TrimEndingSecond { get; set; } = 0;
    }
}
