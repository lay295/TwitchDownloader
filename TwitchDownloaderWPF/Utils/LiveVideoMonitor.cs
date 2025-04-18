using System;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderWPF.Utils
{
    internal class LiveVideoMonitor
    {
        private DateTimeOffset _nextTimeToCheck;
        private bool _lastCheck;
        private int _consecutiveErrors;
        private readonly long _videoId;
        private readonly ITaskLogger _logger;

        public LiveVideoMonitor(long videoId, ITaskLogger logger = null)
        {
            _videoId = videoId;
            _logger = logger;
        }

        private static double GenerateNextRandomInterval()
        {
            const int SECONDS_LOWER_BOUND = 30;
            const int SECONDS_UPPER_BOUND = 34;
            return Random.Shared.NextDouble(SECONDS_LOWER_BOUND, SECONDS_UPPER_BOUND);
        }

        public async Task<bool> IsVideoRecording()
        {
            if (DateTimeOffset.UtcNow > _nextTimeToCheck)
            {
                try
                {
                    var videoResponse = await TwitchHelper.GetVideoInfo(_videoId);
                    _lastCheck = videoResponse.data.video.status == "RECORDING";
                    _consecutiveErrors = 0;
                }
                catch (Exception ex)
                {
                    const int MAX_ERRORS = 10;
                    _consecutiveErrors++;

                    _logger?.LogVerbose($"Error while getting monitor info for {_videoId}: {ex.Message} {MAX_ERRORS - _consecutiveErrors} retries left.");
                    if (_consecutiveErrors >= MAX_ERRORS)
                    {
                        _logger?.LogError($"Error while getting monitor info for {_videoId}: {ex.Message} Assuming video is not live.");
                        return false;
                    }
                }

                _nextTimeToCheck = DateTimeOffset.UtcNow.AddSeconds(GenerateNextRandomInterval());
            }

            return _lastCheck;
        }
    }
}
