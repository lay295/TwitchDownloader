using System.Collections.Concurrent;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tools
{
    internal sealed class StreamDownloadState(ITaskLogger logger)
    {
        public class PartState
        {
            public byte DownloadAttempts { get; set; } = 0;
            public required string Path { get; init; }
            public required DateTimeOffset ProgramDateTime { get; init; }
            public required TimeSpan Duration { get; set; }
            public required string FileName { get; init; }

            public override string ToString() => FileName;
        }

        public bool DownloadInProgress { get; private set; } = true;

        public ConcurrentQueue<PartState> PartQueue { get; } = new();

        public string HeaderFile { get; set; } = null;

        public TimeSpan TotalDownloadedTime { get; set; } = TimeSpan.Zero;

        public TimeSpan TotalMissingTime { get; set; } = TimeSpan.Zero;

        public Lock TimeWriteLock { get; } = new();

        private DateTimeOffset _expectedNextPart = default;

        public void AppendSegment(M3U8 playlist)
        {
            if (_expectedNextPart == default)
            {
                _expectedNextPart = playlist.Streams[0].ProgramDateTime;
            }

            for (int i = 0; i < playlist.Streams.Length; i++)
            {
                M3U8.Stream stream = playlist.Streams[i];

                var partsGap = stream.ProgramDateTime - _expectedNextPart;
                if (partsGap < TimeSpan.Zero)
                    continue;

                // Stream parts might rarely have a gap of a microsecond
                if (partsGap > TimeSpan.FromMicroseconds(1))
                {
                    logger.LogWarning($"Parts from {_expectedNextPart.ToString("yyyy-MM-ddTHH-mm-ss.fffffff")} to {stream.ProgramDateTime.ToString("yyyy-MM-ddTHH-mm-ss.fffffff")} are missing from the live feed.");
                    lock (TimeWriteLock)
                    {
                        TotalMissingTime += partsGap;
                    }
                }

                PartQueue.Enqueue(new()
                {
                    Path = stream.Path,
                    ProgramDateTime = stream.ProgramDateTime,
                    Duration = TimeSpan.FromSeconds((double)stream.PartInfo.Duration),
                    FileName = DownloadTools.GetStreamPartFileName(stream)
                });
                _expectedNextPart = stream.ProgramDateTime + TimeSpan.FromSeconds((double)stream.PartInfo.Duration);
            }
        }

        public void StopDownload()
        {
            DownloadInProgress = false;
        }
    }
}