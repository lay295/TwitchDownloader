using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchDownloaderCore;

namespace TwitchDownloaderWPF.Utils
{
    internal class LiveVideoMonitor
    {
        private DateTimeOffset _nextTimeToCheck;
        private bool _lastCheck;
        private const int SECONDS_UPPER_BOUND = 30;
        private const int SECONDS_LOWER_BOUND = 33;
        private Random _random = new Random();

        private double generateNextRandomInterval()
        {
            return _random.NextDouble() * (SECONDS_UPPER_BOUND - SECONDS_LOWER_BOUND) + SECONDS_LOWER_BOUND;
        }
        public bool IsVideoRecording(long videoId)
        {
            if (DateTimeOffset.UtcNow > _nextTimeToCheck)
            {
                var videoResponse = TwitchHelper.GetVideoInfo(videoId).Result;
                _lastCheck = videoResponse.data.video.status == "RECORDING";
                _nextTimeToCheck = DateTimeOffset.UtcNow.AddSeconds(generateNextRandomInterval());
            }

            return _lastCheck;
        }
    }
}
