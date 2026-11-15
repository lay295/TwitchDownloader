namespace TwitchDownloaderWPF.Tests.PageTests
{
    // Tests PageChatDownload.ValidateUrl, which accepts either a VOD or a clip because chat can be downloaded for both; it returns the ID or
    // null (the page's "invalid link" signal). (URL parsing itself is covered by Core's IdParseTests.)
    public class PageChatDownloadTests
    {
        // Verifies both VOD and clip links are accepted and reduced to their ID.
        // Why: the chat page decides VOD vs clip from the returned ID's shape (digits vs slug); rejecting either kind would make that chat
        // impossible to download from the GUI.
        [Theory]
        [InlineData("https://www.twitch.tv/videos/6834869128", "6834869128")]
        [InlineData("https://clips.twitch.tv/SpineyPieTwitchRPGNurturing", "SpineyPieTwitchRPGNurturing")]
        public void ValidateUrl_AcceptsVideosAndClips(string input, string expected)
        {
            Assert.Equal(expected, PageChatDownload.ValidateUrl(input));
        }

        // Verifies text that is neither a VOD nor a clip yields null.
        // Why: null is what triggers the invalid-link message instead of a failing API call.
        [Fact]
        public void ValidateUrl_RejectsGarbage()
        {
            Assert.Null(PageChatDownload.ValidateUrl("https://example.com/not/twitch"));
        }
    }
}
