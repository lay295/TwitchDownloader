using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Tests.ModelTests;

public class TimeDurationTests {
    [Theory]
    [InlineData("200ms", 200 * TimeSpan.TicksPerMillisecond)]
    [InlineData("55", 55 * TimeSpan.TicksPerSecond)]
    [InlineData("0.2", 2 * TimeSpan.TicksPerSecond / 10)]
    [InlineData("23.189", 23189 * TimeSpan.TicksPerSecond / 1000)]
    [InlineData("55s", 55 * TimeSpan.TicksPerSecond)]
    [InlineData("17m", 17 * TimeSpan.TicksPerMinute)]
    [InlineData("31h", 31 * TimeSpan.TicksPerHour)]
    [InlineData("0:09:27", 9 * TimeSpan.TicksPerMinute + 27 * TimeSpan.TicksPerSecond)]
    [InlineData("11:30", 11 * TimeSpan.TicksPerHour + 30 * TimeSpan.TicksPerMinute)]
    [InlineData("12:03:45", 12 * TimeSpan.TicksPerHour + 3 * TimeSpan.TicksPerMinute + 45 * TimeSpan.TicksPerSecond)]
    public void CorrectlyParsesTimeStrings(string input, long expectedTicks) {
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
    public void ThrowsOnBadFormat(string input) {
        Assert.ThrowsAny<Exception>(() => { _ = TimeDuration.Parse(input); });
    }
}
