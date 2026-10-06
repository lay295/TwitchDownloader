using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class TrimLimitsTests
    {
        [Fact]
        public void UsesFallbackWhenLengthIsMissing()
        {
            Assert.Equal(TrimLimits.DEFAULT_HOUR_MAXIMUM, TrimLimits.HourMaximum(TimeSpan.Zero));
            Assert.Equal(TrimLimits.DEFAULT_HOUR_MAXIMUM, TrimLimits.HourMaximum(TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void UsesWholeHoursFromLength()
        {
            Assert.Equal(48, TrimLimits.HourMaximum(TimeSpan.FromHours(48)));
            Assert.Equal(55, TrimLimits.HourMaximum(TimeSpan.FromHours(55) + TimeSpan.FromMinutes(12)));
        }
    }
}
