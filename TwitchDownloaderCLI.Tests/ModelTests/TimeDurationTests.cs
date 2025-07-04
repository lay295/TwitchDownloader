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
        [InlineData("11:30", 11 * TicksPerMinute + 30 * TicksPerSecond)]
        [InlineData("10:24.12345", 10 * TicksPerMinute + 24 * TicksPerSecond + 123 * TicksPerMillisecond)]
        [InlineData("12:03:45", 12 * TicksPerHour + 3 * TicksPerMinute + 45 * TicksPerSecond)]
        [InlineData("39:23:02", 39 * TicksPerHour + 23 * TicksPerMinute + 2 * TicksPerSecond)]
        [InlineData("47:22:08.123", 47 * TicksPerHour + 22 * TicksPerMinute + 8 * TicksPerSecond + 123 * TicksPerMillisecond)]
        [InlineData("47:22:08.12345", 47 * TicksPerHour + 22 * TicksPerMinute + 8 * TicksPerSecond + 123 * TicksPerMillisecond)]
        [InlineData("1.2:3:4.5", 1 * TicksPerDay + 2 * TicksPerHour + 3 * TicksPerMinute + 4 * TicksPerSecond + 500 * TicksPerMillisecond)]
        [InlineData("2:03:54:27.26", 2 * TicksPerDay + 3 * TicksPerHour + 54 * TicksPerMinute + 27 * TicksPerSecond + 260 * TicksPerMillisecond)]
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
            Assert.ThrowsAny<Exception>(() => TimeDuration.Parse(input));
        }
    }
}