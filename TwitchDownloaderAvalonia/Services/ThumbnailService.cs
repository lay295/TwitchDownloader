using Avalonia.Media.Imaging;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class ThumbnailService : IDisposable
    {
        private const string MISSING_THUMBNAIL_URL = @"https://vod-secure.twitch.tv/_404/404_processing_320x180.png";

        private readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        public async Task<Bitmap?> TryGetAsync(string? url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
                url = MISSING_THUMBNAIL_URL;

            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);
                memory.Position = 0;
                return new Bitmap(memory);
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
