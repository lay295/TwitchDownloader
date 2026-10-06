using System.Text.RegularExpressions;

namespace TwitchDownloaderAvalonia.Update.Markdown
{
    public static partial class GitHubMarkdownText
    {
        public static string FormatLinkLabel(string? displayedText, string? url)
        {
            if (TryGetIssueOrPullRef(url, out var issueRef) && ShouldReplaceWithIssueRef(displayedText, url))
                return issueRef;

            return string.IsNullOrWhiteSpace(displayedText) ? url ?? string.Empty : displayedText;
        }

        public static bool TryGetIssueOrPullRef(string? url, out string label)
        {
            label = string.Empty;
            if (string.IsNullOrWhiteSpace(url))
                return false;

            var match = IssueOrPullRegex().Match(url);
            if (!match.Success)
                return false;

            label = "#" + match.Groups[1].Value;
            return true;
        }

        private static bool ShouldReplaceWithIssueRef(string? displayedText, string? url)
        {
            if (string.IsNullOrWhiteSpace(displayedText))
                return true;

            return string.Equals(displayedText, url, StringComparison.OrdinalIgnoreCase) || TryGetIssueOrPullRef(displayedText, out _);
        }

        [GeneratedRegex(@"github\.com/[^/\s]+/[^/\s]+/(?:issues|pulls?|discussions)/(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex IssueOrPullRegex();
    }
}
