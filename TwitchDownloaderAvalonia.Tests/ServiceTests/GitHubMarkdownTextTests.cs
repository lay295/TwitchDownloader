using TwitchDownloaderAvalonia.Update;
using GitHubMarkdownText = TwitchDownloaderAvalonia.Update.Markdown.GitHubMarkdownText;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class GitHubMarkdownTextTests
    {
        [Theory]
        [InlineData("https://github.com/lay295/TwitchDownloader/pull/1583", "#1583")]
        [InlineData("https://github.com/lay295/TwitchDownloader/issues/1581", "#1581")]
        [InlineData("https://www.github.com/lay295/TwitchDownloader/discussions/12", "#12")]
        [InlineData("https://github.com/lay295/TwitchDownloader/pulls/99/", "#99")]
        public void ShortensGitHubIssueAndPullUrls(string url, string expected)
        {
            Assert.Equal(expected, GitHubMarkdownText.FormatLinkLabel(url, url));
        }

        [Fact]
        public void KeepsCustomLinkText()
        {
            Assert.Equal(
                "Fix crash",
                GitHubMarkdownText.FormatLinkLabel("Fix crash", "https://github.com/lay295/TwitchDownloader/pull/1583"));
        }

        [Fact]
        public void LeavesNonGitHubUrlsUnchanged()
        {
            const string URL = "https://example.com/pull/1583";
            Assert.Equal(URL, GitHubMarkdownText.FormatLinkLabel(URL, URL));
        }
    }
}
