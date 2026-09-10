namespace TwitchDownloaderAvalonia.Services
{
    public sealed class ThumbnailService : IDisposable
    {
        internal const long MAX_BYTES = 5 * 1024 * 1024;

        private const string MISSING_THUMBNAIL_URL = @"https://vod-secure.twitch.tv/_404/404_processing_320x180.png";

        private readonly HttpClient _httpClient;

        public ThumbnailService() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
        {
        }

        internal ThumbnailService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<byte[]?> TryGetAsync(string? url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                url = MISSING_THUMBNAIL_URL;

            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
