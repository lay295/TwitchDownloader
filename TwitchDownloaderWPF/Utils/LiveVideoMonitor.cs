using System;
using TwitchDownloaderCore;

namespace TwitchDownloaderWPF.Utils
{
    internal class LiveVideoMonitor
    {
        private DateTimeOffset _nextTimeToCheck;
        private bool _lastCheck;
        private const int SECONDS_LOWER_BOUND = 30;
        private const int SECONDS_UPPER_BOUND = 34;
        private Random _random = new Random();
        private long _videoId;

        public LiveVideoMonitor(long videoId) 
        {
            _videoId = videoId;
        }

        private double generateNextRandomInterval()
        {
            return _random.NextDouble() * (SECONDS_UPPER_BOUND - SECONDS_LOWER_BOUND) + SECONDS_LOWER_BOUND;
        }
        public bool IsVideoRecording()
        {
            if (DateTimeOffset.UtcNow > _nextTimeToCheck)
            {
                var videoResponse = TwitchHelper.GetVideoInfo(_videoId).Result;
                _lastCheck = videoResponse.data.video.status == "RECORDING";
                _nextTimeToCheck = DateTimeOffset.UtcNow.AddSeconds(generateNextRandomInterval());
            }

            return _lastCheck;
        }
    }
}
