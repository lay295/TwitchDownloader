using System.Runtime.CompilerServices;

namespace TwitchDownloaderWPF.Tests.PageTests
{
    // Tests PageVodDownload.ValidateUrl, the only pure logic in the VOD page's code-behind: it turns whatever the user pasted into a VOD ID,
    // or -1, which the page treats as "show the invalid link message". (URL parsing itself is covered by Core's IdParseTests.)
    public class PageVodDownloadTests
    {
        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "ValidateUrl")]
        private static extern long ValidateUrl(PageVodDownload? @null, string text);

        // Verifies VOD URLs, highlight URLs and bare IDs (with surrounding whitespace) yield the numeric ID.
        // Why: these are the forms users paste; each must reach GetVideoInfo as the right long.
        [Theory]
        [InlineData("https://www.twitch.tv/videos/6834869128", 6834869128)]
        [InlineData("https://www.twitch.tv/kitboga/v/2865132173", 2865132173)]
        [InlineData("  41546181  ", 41546181)]
        public void ValidateUrl_ReturnsVideoId(string input, long expected)
        {
            Assert.Equal(expected, ValidateUrl(null, input));
        }

        // Verifies clip links and garbage yield the -1 sentinel.
        // Why: a clip slug pasted into the VOD page must produce the invalid-link message, not a lookup of some unrelated numeric ID.
        [Theory]
        [InlineData("https://clips.twitch.tv/SpineyPieTwitchRPGNurturing")]
        [InlineData("not a link")]
        [InlineData("")]
        public void ValidateUrl_ReturnsMinusOneForNonVideos(string input)
        {
            Assert.Equal(-1, ValidateUrl(null, input));
        }
    }
}
