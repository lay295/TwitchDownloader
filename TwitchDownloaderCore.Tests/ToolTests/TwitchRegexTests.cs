using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Tests.ToolTests
{
    // Tests TwitchRegex: UrlTimeCode extracts "?t=1h2m3s" from VOD links (start time for trimming) and BitsRegex detects
    // cheermotes in chat so the renderer can draw them as bits.
    public class TwitchRegexTests
    {
        /// <summary>The time code is taken from a "t" query parameter at any position, and only when it is a complete d/h/m/s value.</summary>
        [Theory]
        [InlineData("https://www.twitch.tv/videos/123?t=1h2m3s", "1h2m3s")]
        [InlineData("https://www.twitch.tv/videos/123?foo=bar&t=45s&baz=1", "45s")]
        [InlineData("https://www.twitch.tv/videos/123?t=2d4h", "2d4h")]
        [InlineData("https://www.twitch.tv/videos/123?st=1h", null)]
        [InlineData("https://www.twitch.tv/videos/123?t=1hx", null)]
        [InlineData("https://www.twitch.tv/videos/123", null)]
        public void UrlTimeCode_ExtractsTimeParameter(string url, string? expected)
        {
            var match = TwitchRegex.UrlTimeCode.Match(url);

            Assert.Equal(expected, match.Success ? match.Value : null);
        }

        /// <summary>
        /// A cheermote is a known prefix followed by 1-7 digits (not starting with 0) as a whole word; partial words, zero amounts
        /// and unknown prefixes must not be treated as bits.
        /// </summary>
        [Theory]
        [InlineData("Cheer100", "100")]
        [InlineData("hello Kappa50 world", "50")]
        [InlineData("ShowLove1000000", "1000000")]
        [InlineData("Cheer0", null)]
        [InlineData("Cheer12345678", null)]
        [InlineData("xCheer100", null)]
        [InlineData("Cheer100x", null)]
        [InlineData("Unknown100", null)]
        public void BitsRegex_MatchesCheermoteAmounts(string message, string? expectedAmount)
        {
            var match = TwitchRegex.BitsRegex.Match(message);

            Assert.Equal(expectedAmount, match.Success ? match.Value : null);
        }
    }
}
