using TwitchDownloaderAvalonia.DependencyInjection;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class ThumbnailService(IHttpClientFactory httpClientFactory)
    {
        internal const long MAX_BYTES = 5 * 1024 * 1024;

        private const string MISSING_THUMBNAIL_URL = @"https://vod-secure.twitch.tv/_404/404_processing_320x180.png";

        public async Task<byte[]?> TryGetAsync(string? url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                url = MISSING_THUMBNAIL_URL;

            try
            {
                using var httpClient = httpClientFactory.CreateClient(HttpClientNames.Thumbnails);
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MAX_BYTES)
                    return null;

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var memory = new MemoryStream();
                var buffer = new byte[81920];
                long total = 0;
                while (true)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read == 0)
                        break;

                    total += read;
                    if (total > MAX_BYTES)
                        return null;

                    memory.Write(buffer, 0, read);
                }

                return memory.ToArray();
            }
            catch
            {
                return null;
            }
        }
    }
}
