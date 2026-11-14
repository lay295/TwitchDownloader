using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    public class UrlTimeCodeTests
    {
        [Theory]
        [InlineData("12s", 0, 0, 0, 12)]
        [InlineData("13m12s", 0, 0, 13, 12)]
        [InlineData("14h13m12s", 0, 14, 13, 12)]
        [InlineData("15d14h13m12s", 15, 14, 13, 12)]
        [InlineData("14h12s", 0, 14, 0, 12)]
        [InlineData("15d12s", 15, 0, 0, 12)]
        [InlineData("15d13m12s", 15, 0, 13, 12)]
        [InlineData("12s13m15d", 15, 0, 13, 12)]
        public void ParsesTimeCodeCorrectly(string timeCode, int days, int hours, int minutes, int seconds)
        {
            var result = UrlTimeCode.Parse(timeCode);

            Assert.Equal(days, result.Days);
            Assert.Equal(hours, result.Hours);
            Assert.Equal(minutes, result.Minutes);
            Assert.Equal(seconds, result.Seconds);
        }

        // Malformed time codes (repeated/unknown units, negative or fractional numbers) must parse to zero rather than a
        // partial value, so a bad ?t= parameter never trims a download at a wrong offset.
        [Theory]
        [InlineData("123abc")]
        [InlineData("12s12s")]
        [InlineData("13ms")]
        [InlineData("123ddd")]
        [InlineData("h")]
        [InlineData("hh")]
        [InlineData("-5s")] // Negative numbers are rejected by the unsigned parse
        [InlineData("1.5m")]
        public void ReturnsZeroForInvalidTimeCode(string timeCode)
        {
            var expected = TimeSpan.Zero;

            var result = UrlTimeCode.Parse(timeCode);

            Assert.Equal(expected, result);
        }
    }
}