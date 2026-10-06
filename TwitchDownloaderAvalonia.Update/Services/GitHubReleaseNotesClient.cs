using System.Text.Json;

namespace TwitchDownloaderAvalonia.Update.Services
{
    public sealed record GitHubReleaseNotes(string? Body, DateTimeOffset? PublishedAt);

    public sealed class GitHubReleaseNotesClient(HttpClient httpClient)
    {
        public const string REPOSITORY_OWNER = "lay295";
        public const string REPOSITORY_NAME = "TwitchDownloader";

        public async Task<GitHubReleaseNotes?> GetReleaseAsync(string tag, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return null;

            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get, $"https://api.github.com/repos/{REPOSITORY_OWNER}/{REPOSITORY_NAME}/releases/tags/{Uri.EscapeDataString(tag)}");

                request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
                request.Headers.TryAddWithoutValidation("User-Agent", "TwitchDownloader");

                using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return null;

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                var root = document.RootElement;

                string? body = null;
                if (root.TryGetProperty("body", out var bodyElement))
                {
                    var text = bodyElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                        body = text;
                }

                DateTimeOffset? publishedAt = null;
                if (root.TryGetProperty("published_at", out var publishedElement)
                    && publishedElement.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(publishedElement.GetString(), out var parsed))
                    publishedAt = parsed;

                return new GitHubReleaseNotes(body, publishedAt);
            }
            catch
            {
                return null;
            }
        }
    }
}
