using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests;

public class UrlTimeCodeTests {
    [Theory]
    [InlineData("12s", 0, 0, 0, 12)]
    [InlineData("13m12s", 0, 0, 13, 12)]
    [InlineData("14h13m12s", 0, 14, 13, 12)]
    [InlineData("15d14h13m12s", 15, 14, 13, 12)]
    public void ParsesTimeCodeCorrectly(string timeCode, int days, int hours, int minutes, int seconds) {
        var result = UrlTimeCode.Parse(timeCode);

        Assert.Equal(days, result.Days);
        Assert.Equal(hours, result.Hours);
        Assert.Equal(minutes, result.Minutes);
        Assert.Equal(seconds, result.Seconds);
    }

    [Fact]
    public void ReturnsZeroForInvalidTimeCode() {
        const string INVALID_TIME_CODE = "123abc";
        var expected = TimeSpan.Zero;

        var result = UrlTimeCode.Parse(INVALID_TIME_CODE);

        Assert.Equal(expected, result);
    }
}
