using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderWPF.Utils
{
    internal class LiveVideoMonitor
    {
        private class VideoState
        {
            public DateTimeOffset NextTimeToCheck { get; private set; }
            public bool LastCheck { get; private set; }
            public GqlVideoResponse LatestVideoResponse { get; private set; }
            public long VideoId { get; }
            public SemaphoreSlim Semaphore { get; }

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
        private static Timer _cacheCleanTimer;

        private readonly long _videoId;
        private readonly ITaskLogger _logger;

        public GqlVideoResponse LatestVideoResponse
        {
            get
            {
                lock (VideoStateCache)
                {
                    if (VideoStateCache.TryGetValue(_videoId, out var state))
                    {
                        return state.LatestVideoResponse;
                    }
                }

                return null;
            }
        }

        public LiveVideoMonitor(long videoId, ITaskLogger logger = null)
        {
            _videoId = videoId;
            _logger = logger;
        }

        public async Task<bool> IsVideoRecording()
        {
            var state = GetOrCreateState(_videoId);

            await state.Semaphore.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                if (DateTimeOffset.UtcNow > state.NextTimeToCheck)
                {
                    await state.CheckIsRecording(_logger);
                }
            }
            finally
            {
                state.Semaphore.Release();
            }

            return state.LastCheck;
        }

        private static VideoState GetOrCreateState(long videoId)
        {
            lock (VideoStateCache)
            {
                // Restart cleanup timer if it is stopped
                _cacheCleanTimer ??= new Timer(TimerCallback, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

                ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(VideoStateCache, videoId, out var exists);
                if (!exists)
                {
                    state = new VideoState(videoId);
                }

                return state;
            }
        }

        private static void TimerCallback(object state)
        {
            lock (VideoStateCache)
            {
                // Remove entries that haven't been checked in a while
                var removeThreshold = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(15);
                foreach (var videoState in VideoStateCache.Values.ToArray())
                {
                    if (videoState.NextTimeToCheck < removeThreshold)
                    {
                        VideoStateCache.Remove(videoState.VideoId);
                    }
                }

                // If the cache is empty, stop the timer
                if (VideoStateCache.Count is 0)
                {
                    _cacheCleanTimer.Dispose();
                    _cacheCleanTimer = null;
                }
            }
        }
    }
}