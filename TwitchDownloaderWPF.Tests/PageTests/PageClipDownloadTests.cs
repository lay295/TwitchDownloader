
namespace TwitchDownloaderWPF.Tests.PageTests
{
    // Tests PageClipDownload.ValidateUrl, which turns the pasted text into a clip slug or null (the page's "invalid link" signal).
    // (URL parsing itself is covered by Core's IdParseTests.)
    public class PageClipDownloadTests
    {
        // Verifies clip URLs and bare slugs yield the slug.
        // Why: these are the forms users paste into the clip page.
        [Theory]
        [InlineData("https://clips.twitch.tv/SpineyPieTwitchRPGNurturing", "SpineyPieTwitchRPGNurturing")]
        [InlineData("https://www.twitch.tv/streamer8/clip/FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf", "FuriousFlaccidTireArgieB8-NHbTiYQlzwHVvv_Vf")]
        [InlineData("SpineyPieTwitchRPGNurturing", "SpineyPieTwitchRPGNurturing")]
        public void ValidateUrl_ReturnsClipSlug(string input, string expected)
        {
            Assert.Equal(expected, PageClipDownload.ValidateUrl(input));
        }

        // Verifies numeric VOD IDs/URLs are rejected with null.
        // Why: a VOD pasted into the clip page must show the invalid-link message instead of querying a clip that does not exist.
        [Theory]
        [InlineData("6834869128")]
        [InlineData("https://www.twitch.tv/videos/6834869128")]
        public void ValidateUrl_RejectsVideos(string input)
        {
            Assert.Null(PageClipDownload.ValidateUrl(input));
        }
    }
}
