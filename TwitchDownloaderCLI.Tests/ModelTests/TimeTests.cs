using TwitchDownloaderCLI.Models;
using static System.TimeSpan;

namespace TwitchDownloaderCLI.Tests.ModelTests
{
    public class TimeTests
    {
        [Theory]
        [InlineData("200ms", 200 * TicksPerMillisecond)]
        [InlineData("55", 55 * TicksPerSecond)]
        [InlineData("0.2", 2 * TicksPerSecond / 10)]
        [InlineData("23.189", 23189 * TicksPerSecond / 1000)]
        [InlineData("55s", 55 * TicksPerSecond)]
        [InlineData("17m", 17 * TicksPerMinute)]
        [InlineData("31h", 31 * TicksPerHour)]
        [InlineData("12:03:45", 12 * TicksPerHour + 3 * TicksPerMinute + 45 * TicksPerSecond)]
        public void CorrectlyParsesTimeStrings(string input, long expectedTicks)
        {
            var expected = new Time(expectedTicks);

            var actual = new Time(input);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("123d")]
        [InlineData("0:12345")]
        public void ThrowsOnBadFormat(string input)
        {
            Assert.ThrowsAny<Exception>(() =>
            {
                _ = Time.Parse(input);
            });
        }
    }
}