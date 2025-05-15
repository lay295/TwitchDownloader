using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tools
{
    public class VideoDownloadState
    {
        public class PartState
        {
            public long ExpectedFileSize;
            public byte DownloadAttempts;
            public bool TryUnmute;
            /// <summary>
            /// The state of the part to fetch the audio from. Null if not applicable.
            ///
            /// Not yet implemented.
            /// </summary>
            public UnmutedPartState UnmutedPart;
        }

        public class UnmutedPartState
        {
            public long ExpectedFileSize;
            public string FileName;
        }

        public IReadOnlyList<string> AllQualities { get; }

        public string DownloadQuality { get; }

        public ConcurrentQueue<string> PartQueue { get; }

        public Dictionary<string, PartState> PartStates { get; }

        public Uri BaseUrl { get; }

        public string HeaderFile { get; }

        public long HeaderFileSize { get; }

        public DateTimeOffset VodAirDate { get; }

        public int PartCount => PartStates.Count;

        public VideoDownloadState(IEnumerable<string> allQualityPaths, IReadOnlyCollection<M3U8.Stream> playlist, Range videoListCrop, Uri baseUrl, string headerFile, DateTimeOffset vodAirDate)
        {
            AllQualities = allQualityPaths.ToArray();
            DownloadQuality = AllQualities.FirstOrDefault(x => baseUrl.AbsolutePath.AsSpan().TrimEnd('/').EndsWith(x)) ?? baseUrl.Segments.Last();

            var orderedParts = playlist
                .Take(videoListCrop)
                .Select(x => x.Path)
                .OrderBy(x => !x.Contains("-muted")); // Prioritize downloading muted segments

            PartQueue = new ConcurrentQueue<string>(orderedParts);

            var vodAge = DateTimeOffset.UtcNow - vodAirDate;
            PartStates = new Dictionary<string, PartState>(PartQueue.Count);
            foreach (var part in PartQueue)
            {
                PartStates[part] = new PartState { TryUnmute = vodAge <= TimeSpan.FromHours(24) };
            }

            BaseUrl = baseUrl;

            HeaderFile = headerFile;
            if (!string.IsNullOrWhiteSpace(headerFile) && new FileInfo(headerFile) is { Exists: true } hFi)
            {
                HeaderFileSize = hFi.Length;
            }

            VodAirDate = vodAirDate;
        }
    }
}