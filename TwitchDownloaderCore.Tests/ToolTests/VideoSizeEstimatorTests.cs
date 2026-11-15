using System.Globalization;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests VideoSizeEstimator, which produces the "estimated size" shown in the GUI quality list and the free-space check
    // that warns before a VOD download fills the disk.
    public class VideoSizeEstimatorTests
    {
        /// <summary>
        /// Byte counts are shown in binary units with 2 decimals below 100 of a unit and 1 decimal above, and nothing for 0/negative.
        /// Culture is pinned to en-US because the output uses the current culture's decimal separator.
        /// </summary>
        [Theory]
        [InlineData(0, "")]
        [InlineData(-5, "")]
        [InlineData(1, "1B")]
        [InlineData(1023, "1023B")]
        [InlineData(1024, "1.00KiB")]
        [InlineData(1536, "1.50KiB")]
        [InlineData(100 * 1024, "100.0KiB")]
        [InlineData(1024 * 1024, "1.00MiB")]
        [InlineData(500L * 1024 * 1024, "500.0MiB")]
        [InlineData(1024L * 1024 * 1024, "1.00GiB")]
        [InlineData(250L * 1024 * 1024 * 1024, "250.0GiB")]
        public void StringifyByteCount_UsesBinaryUnits(long bytes, string expected)
        {
            var oldCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("en-US");

                Assert.Equal(expected, VideoSizeEstimator.StringifyByteCount(bytes));
            }
            finally
            {
                CultureInfo.CurrentCulture = oldCulture;
            }
        }

        /// <summary>The estimate is bandwidth (bits/s) / 8 * duration, for the whole video or only the trimmed range.</summary>
        [Fact]
        public void EstimateVideoSize_IsBitrateTimesDuration()
        {
            Assert.Equal(6_000_000 / 8 * 60, VideoSizeEstimator.EstimateVideoSize(6_000_000, TimeSpan.FromMinutes(1)));
            Assert.Equal(8_000 / 8 * 30, VideoSizeEstimator.EstimateVideoSize(8_000, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(40)));
        }

        /// <summary>Unknown bandwidth or an inverted trim range yields 0 instead of a negative size that would break the free-space check.</summary>
        [Theory]
        [InlineData(0, 0, 60)]
        [InlineData(-1, 0, 60)]
        [InlineData(1000, 60, 30)]
        public void EstimateVideoSize_ReturnsZeroForInvalidInput(int bandwidth, int startSeconds, int endSeconds)
        {
            Assert.Equal(0, VideoSizeEstimator.EstimateVideoSize(bandwidth, TimeSpan.FromSeconds(startSeconds), TimeSpan.FromSeconds(endSeconds)));
        }
    }
}
