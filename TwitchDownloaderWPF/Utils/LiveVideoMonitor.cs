using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderWPF.Utils
{
    // ReSharper disable InconsistentlySynchronizedField
    internal class LiveVideoMonitor
    {
        private class VideoState
        {
            public DateTimeOffset NextTimeToCheck { get; private set; }
            public bool LastCheck { get; private set; }
            public GqlVideoResponse LatestVideoResponse { get; private set; }
            public SemaphoreSlim Semaphore { get; }
            public long VideoId { get; }
            public int RefCount { get; set; }

            private int _consecutiveErrors;

            public VideoState(long videoId)
            {
                LastCheck = true;
                VideoId = videoId;
                Semaphore = new SemaphoreSlim(1, 1);
            }

            public async Task CheckIsRecording(ITaskLogger logger)
            {
                try
                {
                    LatestVideoResponse = await TwitchHelper.GetVideoInfo(VideoId);
                    LastCheck = LatestVideoResponse.data.video.status == "RECORDING";
                    _consecutiveErrors = 0;
                }
                catch (Exception ex)
                {
                    const int MAX_ERRORS = 6;
                    _consecutiveErrors++;

                    logger?.LogVerbose($"Error while getting monitor info for {VideoId}: {ex.Message} {MAX_ERRORS - _consecutiveErrors} retries left.");
                    if (_consecutiveErrors >= MAX_ERRORS)
                    {
                        logger?.LogError($"Error while getting monitor info for {VideoId}: {ex.Message} Assuming video is not live.");
                        LastCheck = false;
                    }
                }

                var randomOffset = Random.Shared.NextDouble(30, 40);
                NextTimeToCheck = DateTimeOffset.UtcNow.AddSeconds(randomOffset);
            }
        }

        private static readonly Dictionary<long, VideoState> VideoStateCache = new();

        private readonly ITaskLogger _logger;
        private readonly VideoState _state;

        public GqlVideoResponse LatestVideoResponse => _state.LatestVideoResponse;

        public LiveVideoMonitor(long videoId, ITaskLogger logger = null)
        {
            _logger = logger;

            lock (VideoStateCache)
            {
                ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(VideoStateCache, videoId, out var exists);
                if (!exists)
                {
                    state = new VideoState(videoId);
                }

                _state = state;
                _state.RefCount++;
            }
        }

        public async Task<bool> IsVideoRecording()
        {
            await _state.Semaphore.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                if (DateTimeOffset.UtcNow > _state.NextTimeToCheck)
                {
                    await _state.CheckIsRecording(_logger);
                }
            }
            finally
            {
                _state.Semaphore.Release();
            }

            return _state.LastCheck;
        }

        ~LiveVideoMonitor()
        {
            lock (VideoStateCache)
            {
                _state.RefCount--;
                if (_state.RefCount < 1)
                {
                    VideoStateCache.Remove(_state.VideoId);
                }
            }
        }
    }
}