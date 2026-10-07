using System;

namespace TwitchDownloaderCore.Options
{
    public class LiveStreamDownloadOptions
    {
        /// <summary>
        /// The id of the in-progress VOD. Used to resolve the channel login (and is shown in the queue).
        /// Ignored if <see cref="Channel"/> is set.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The channel login to record. If empty, it is resolved from <see cref="Id"/>.
        /// </summary>
        public string Channel { get; set; }

        public string Quality { get; set; }
        public string Filename { get; set; }
        public string Oauth { get; set; }
        public string FfmpegPath { get; set; }
        public int DownloadThreads { get; set; } = 4;
        public int ThrottleKib { get; set; } = -1;
        public string TempFolder { get; set; }

        public bool TrimBeginning { get; set; }
        public TimeSpan TrimBeginningTime { get; set; }
        public bool TrimEnding { get; set; }
        public TimeSpan TrimEndingTime { get; set; }
    }
}
