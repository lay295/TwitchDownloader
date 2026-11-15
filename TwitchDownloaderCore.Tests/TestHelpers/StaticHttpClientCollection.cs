namespace TwitchDownloaderCore.Tests.TestHelpers
{
    /// <summary>
    /// Tests that swap one of the process-wide static <see cref="HttpClient"/> fields (TwitchHelper.httpClient,
    /// ChatDownloader.HttpClient, ClipDownloader.HttpClient) must be in this collection. It disables
    /// parallelization so two tests never replace the same static client at the same time.
    /// </summary>
    [CollectionDefinition(NAME, DisableParallelization = true)]
    public sealed class StaticHttpClientCollection
    {
        public const string NAME = "Static HttpClient";
    }

    /// <summary>
    /// Replaces a static <see cref="HttpClient"/> for the lifetime of a test and restores the original on dispose.
    /// </summary>
    /// <example><code>using var _ = new HttpClientOverride(c => TwitchHelper.httpClient = c, () => TwitchHelper.httpClient, handler);</code></example>
    public sealed class HttpClientOverride : IDisposable
    {
        private readonly Action<HttpClient> _set;
        private readonly HttpClient _original;
        private readonly HttpClient _fake;

        public HttpClientOverride(Action<HttpClient> set, Func<HttpClient> get, HttpMessageHandler handler, Uri? baseAddress = null)
        {
            _set = set;
            _original = get();
            _fake = new HttpClient(handler) { BaseAddress = baseAddress ?? _original.BaseAddress };
            foreach (var header in _original.DefaultRequestHeaders)
            {
                _fake.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            set(_fake);
        }

        public void Dispose()
        {
            _set(_original);
            _fake.Dispose();
        }
    }
}
