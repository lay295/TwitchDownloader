using System;
using System.IO;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Options
{
    public class VideoDownloadOptions
    {
        public long Id { get; set; }
        public string Quality { get; set; }
        public string Filename { get; set; }
        public bool TrimBeginning { get; set; }
        public TimeSpan TrimBeginningTime { get; set; }
        public bool TrimEnding { get; set; }
        public TimeSpan TrimEndingTime { get; set; }
        public int DownloadThreads { get; set; }
        public int ThrottleKib { get; set; }
        public string Oauth { get; set; }
        public string FfmpegPath { get; set; }
        public string TempFolder { get; set; }
        public Func<DirectoryInfo[], DirectoryInfo[]> CacheCleanerCallback { get; set; }
        public Func<FileInfo, FileInfo> FileCollisionCallback { get; set; } = info => info;
        public VideoTrimMode TrimMode { get; set; }
        public bool DelayDownload { get; set; }

        /// <summary>
        /// When set, the downloader ignores <see cref="Id"/> and instead reconstructs the broadcast's
        /// public DVR playlist from <see cref="ChannelLogin"/>, <see cref="StreamId"/>, and
        /// <see cref="StreamStartTime"/>. This is an unofficial method for grabbing broadcasts from
        /// channels that have "Store past broadcasts" disabled — see <see cref="TwitchHelper.RecoverHiddenVodPlaylistUrl"/>.
        /// It is best-effort: it only works while the broadcast's segments still exist on the CDN
        /// (typically the broadcast must be live or to have ended within the last day or two), and it
        /// produces source quality only.
        /// </summary>
        public bool RecoverHiddenVod { get; set; }
        public string ChannelLogin { get; set; }
        public string StreamId { get; set; }
        public DateTimeOffset StreamStartTime { get; set; }
    }
}