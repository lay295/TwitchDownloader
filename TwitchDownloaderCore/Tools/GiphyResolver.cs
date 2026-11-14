using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tools
{
    /// <summary>Resolves the alt text Twitch records for a chat GIF back to the Giphy GIF it was posted from.</summary>
    /// <remarks>The comment API returns only the bracketed title, e.g. "[Horse Stare GIF by Jan Metternich]", so it has to be searched for.</remarks>
    public static partial class GiphyResolver
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30),
            // Giphy serves a trimmed page to unrecognized agents.
            DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36" } }
        };

        private const string SEARCH_RESULTS_KEY = "initialGifs";

        // Requiring "GIF" keeps this from matching ordinary bracketed chat messages
        [GeneratedRegex(@"^\[(?<title>[^\[\]]*\bGIFs?\b[^\[\]]*)\]$")]
        private static partial Regex AltTextRegex { get; }

        [System.Diagnostics.DebuggerDisplay("{title}")]
        private sealed class GiphySearchResult
        {
            public string id { get; set; }
            public string title { get; set; }
            public GiphyImages images { get; set; }
        }

        private sealed class GiphyImages
        {
            public GiphyRendition original { get; set; }
            public GiphyRendition downsized { get; set; }
        }

        private sealed class GiphyRendition
        {
            public string url { get; set; }
            // Giphy sends these as strings
            public string size { get; set; }
            public string width { get; set; }
            public string height { get; set; }

            public long Bytes => long.TryParse(size, out var bytes) ? bytes : long.MaxValue;
            public int Width => int.TryParse(width, out var value) ? value : 0;
            public int Height => int.TryParse(height, out var value) ? value : 0;
        }

        // Width/Height are what Giphy advertises for the image at Url, used to spot a substituted one
        public readonly record struct GiphyGif(string Id, string Url, int Width, int Height);

        internal readonly record struct GiphySearchEntry(string Id, string Title, GiphyGif Gif);

        // Strips the per-request tracking segment so an archived url stays stable
        [GeneratedRegex(@"/media/v1\.[^/]+/")]
        private static partial Regex MediaTrackingRegex { get; }

        /// <summary>Extracts the Giphy title from <paramref name="messageBody"/>, if it is a posted GIF.</summary>
        public static bool TryParseAltText(string messageBody, out string title)
        {
            if (!string.IsNullOrEmpty(messageBody))
            {
                var match = AltTextRegex.Match(messageBody.Trim());
                if (match.Success)
                {
                    title = match.Groups["title"].Value;
                    return true;
                }
            }

            title = null;
            return false;
        }

        public static IEnumerable<string> GetTitles(IEnumerable<Comment> comments)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var comment in comments ?? [])
            {
                if (TryParseAltText(comment.message?.body, out var title) && seen.Add(title))
                {
                    yield return title;
                }
            }
        }

        public static string MediaUrl(string gifId) => $"https://i.giphy.com/{gifId}.gif";

        /// <summary>Searches Giphy for the GIF titled <paramref name="title"/>.</summary>
        /// <remarks>Titles are not unique, and Twitch records nothing to tell duplicates apart, so the highest ranked match wins.</remarks>
        public static async Task<GiphyGif?> ResolveAsync(string title, ITaskLogger logger, CancellationToken cancellationToken = default)
        {
            var gif = await SearchAsync(title, title, logger, cancellationToken);
            if (gif is not null)
            {
                return gif;
            }

            // Giphy 404s the search route on very long slugs, so retry without the trailing " GIF by <author>".
            var authorIndex = title.IndexOf(" GIF by ", StringComparison.OrdinalIgnoreCase);
            if (authorIndex > 0)
            {
                gif = await SearchAsync(title[..authorIndex], title, logger, cancellationToken);
            }

            if (gif is null)
            {
                logger.LogVerbose($"No Giphy result titled \"{title}\".");
            }

            return gif;
        }

        private static async Task<GiphyGif?> SearchAsync(string query, string wantedTitle, ITaskLogger logger, CancellationToken cancellationToken)
        {
            var slug = Slugify(query);
            if (slug.Length == 0)
            {
                return null;
            }

            using var response = await HttpClient.GetAsync($"https://giphy.com/search/{slug}", cancellationToken);
            if (response.StatusCode is HttpStatusCode.NotFound)
            {
                logger.LogVerbose($"Giphy has no search page for \"{query}\".");
                return null;
            }

            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadAsStringAsync(cancellationToken);

            var results = ParseSearchPage(page);
            if (results.Count == 0)
            {
                logger.LogVerbose($"Giphy search page for \"{query}\" yielded no results.");
                return null;
            }

            foreach (var result in results)
            {
                if (wantedTitle.Equals(result.Title, StringComparison.OrdinalIgnoreCase))
                {
                    return result.Gif;
                }
            }

            return null;
        }

        // Giphy's search API needs an account key, so the results embedded in the public search page are read instead:
        // a JSON array under "initialGifs", escaped inside a Next.js payload. Ceilings: only the first page is server
        // rendered, and a payload rework would stop this matching. Both degrade to plain text. Upgrade path is the API.
        /// <summary>Reads the search results embedded in a Giphy search page, or empty if it holds none.</summary>
        internal static IReadOnlyList<GiphySearchEntry> ParseSearchPage(string page)
        {
            var escaped = SliceResultsArray(page);
            if (escaped is null)
            {
                return [];
            }

            GiphySearchResult[] results;
            try
            {
                // Escaped literal back into text, then that text into the results
                var json = JsonSerializer.Deserialize<string>(string.Concat("\"", escaped, "\""));
                results = JsonSerializer.Deserialize<GiphySearchResult[]>(json!) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }

            var entries = new List<GiphySearchEntry>(results.Length);
            foreach (var result in results)
            {
                if (!string.IsNullOrEmpty(result?.id) && !string.IsNullOrEmpty(result.title))
                {
                    entries.Add(new GiphySearchEntry(result.id, result.title, PickRendition(result)));
                }
            }

            return entries;
        }

        // Prefers the downsized rendition when genuinely smaller, since originals reach 8 MB. The rendition must come
        // from the search results, not a constructed url: Giphy answers those with a placeholder instead of a 404.
        private static GiphyGif PickRendition(GiphySearchResult result)
        {
            var original = result.images?.original;
            var downsized = result.images?.downsized;

            var chosen = downsized?.url is not null && original?.url is not null && downsized.Bytes < original.Bytes
                ? downsized
                : original;

            return chosen?.url is null
                ? new GiphyGif(result.id, MediaUrl(result.id), 0, 0)
                : new GiphyGif(result.id, MediaTrackingRegex.Replace(chosen.url, "/media/"), chosen.Width, chosen.Height);
        }

        // Returns the still-escaped "initialGifs" array, or null
        private static string SliceResultsArray(string page)
        {
            var keyIndex = page.IndexOf(SEARCH_RESULTS_KEY, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return null;
            }

            var start = page.IndexOf('[', keyIndex);
            if (start < 0)
            {
                return null;
            }

            // Every quote in the payload is escaped, so \" toggles string state and brackets only count outside one
            var depth = 0;
            var inString = false;
            for (var i = start; i < page.Length; i++)
            {
                if (page[i] == '\\')
                {
                    if (i + 1 < page.Length && page[i + 1] == '"')
                    {
                        inString = !inString;
                    }

                    i++;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (page[i] == '[')
                {
                    depth++;
                }
                else if (page[i] == ']' && --depth == 0)
                {
                    return page[start..(i + 1)];
                }
            }

            return null;
        }

        // The hyphenated lowercase slug Giphy's search route expects
        public static string Slugify(ReadOnlySpan<char> text)
        {
            var slug = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (char.IsAsciiLetterOrDigit(c))
                {
                    slug.Append(char.ToLowerInvariant(c));
                }
                else if (slug.Length > 0 && slug[^1] != '-')
                {
                    slug.Append('-');
                }
            }

            if (slug.Length > 0 && slug[^1] == '-')
            {
                slug.Length--;
            }

            return slug.ToString();
        }
    }
}
