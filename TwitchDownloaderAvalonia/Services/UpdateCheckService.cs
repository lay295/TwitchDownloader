using System.Xml;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed record UpdateCheckResult(Version RemoteVersion, bool IsNewer, string ChangelogUrl);

    public sealed class UpdateCheckService
    {
        public const string FEED_URL = "https://downloader-update.twitcharchives.workers.dev/";
        public const string DEFAULT_CHANGELOG_URL = "https://github.com/lay295/TwitchDownloader/releases";

        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private UpdateCheckResult? _cached;
        private bool _completed;

        public UpdateCheckService() : this(CreateClient()) { }

        internal UpdateCheckService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TwitchDownloader");
        }

        public async Task<UpdateCheckResult?> CheckAsync(Version localVersion, CancellationToken cancellationToken = default)
        {
            if (_completed)
                return _cached;

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (_completed)
                    return _cached;

                var result = await CheckCoreAsync(localVersion, cancellationToken);
                if (result is null)
                    return result;

                _cached = result;
                _completed = true;

                return result;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<UpdateCheckResult?> CheckCoreAsync(Version localVersion, CancellationToken cancellationToken)
        {
            try
            {
                var xml = await _httpClient.GetStringAsync(FEED_URL, cancellationToken);
                if (string.IsNullOrWhiteSpace(xml))
                    return null;

                var doc = new XmlDocument();
                doc.LoadXml(xml);

                var versionText = doc.DocumentElement?.SelectSingleNode("/item/version")?.InnerText.Trim();
                if (string.IsNullOrWhiteSpace(versionText) || !Version.TryParse(versionText, out var remote))
                    return null;

                remote = remote.StripRevisionIfDefault();
                var changelog = doc.DocumentElement?.SelectSingleNode("/item/changelog")?.InnerText.Trim();
                if (string.IsNullOrWhiteSpace(changelog))
                    changelog = DEFAULT_CHANGELOG_URL;

                return new UpdateCheckResult(remote, remote > localVersion, changelog);
            }
            catch
            {
                return null;
            }
        }

        private static HttpClient CreateClient()
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15),
            };
        }
    }
}
