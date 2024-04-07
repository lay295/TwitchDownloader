using TwitchDownloaderCLI.Models;
using static System.TimeSpan;

namespace TwitchDownloaderCLI.Tests.ModelTests
{
    public class TimeDurationTests
    {
        [Theory]
        [InlineData("200ms", 200 * TicksPerMillisecond)]
        [InlineData("55", 55 * TicksPerSecond)]
        [InlineData("0.2", 2 * TicksPerSecond / 10)]
        [InlineData("23.189", 23189 * TicksPerSecond / 1000)]
        [InlineData("55s", 55 * TicksPerSecond)]
        [InlineData("17m", 17 * TicksPerMinute)]
        [InlineData("31h", 31 * TicksPerHour)]
        [InlineData("0:09:27", 9 * TicksPerMinute + 27 * TicksPerSecond)]
        [InlineData("11:30", 11 * TicksPerHour + 30 * TicksPerMinute)]
        [InlineData("12:03:45", 12 * TicksPerHour + 3 * TicksPerMinute + 45 * TicksPerSecond)]
        public void CorrectlyParsesTimeStrings(string input, long expectedTicks)
        {
            var expected = new TimeDuration(expectedTicks);

            var actual = new TimeDuration(input);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("100 s")]
        [InlineData("123d")]
        [InlineData("0:12345")]
        public void ThrowsOnBadFormat(string input)
        {
            Assert.ThrowsAny<Exception>(() =>
            {
                _ = TimeDuration.Parse(input);
            });
        }
    }
}