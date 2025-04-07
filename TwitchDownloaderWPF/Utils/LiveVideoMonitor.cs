using System;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderWPF.Utils
{
    internal class LiveVideoMonitor
    {
        private DateTimeOffset _nextTimeToCheck;
        private bool _lastCheck;
        private readonly long _videoId;

        public LiveVideoMonitor(long videoId) 
        {
            _videoId = videoId;
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
                var videoResponse = await TwitchHelper.GetVideoInfo(_videoId);
                _lastCheck = videoResponse.data.video.status == "RECORDING";
                _nextTimeToCheck = DateTimeOffset.UtcNow.AddSeconds(GenerateNextRandomInterval());
            }

            return _lastCheck;
        }
    }
}
