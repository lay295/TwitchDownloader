using System.Xml;
using TwitchDownloaderAvalonia.Update.Models;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderAvalonia.Update.Services
{
    public sealed class UpdateCheckService(HttpClient httpClient)
    {
        public const string FEED_URL = "https://downloader-update.twitcharchives.workers.dev/";
        public const string DEFAULT_CHANGELOG_URL = "https://github.com/lay295/TwitchDownloader/releases";

        private readonly SemaphoreSlim _gate = new(1, 1);
        private UpdateCheckResult? _cached;
        private bool _completed;

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
                var xml = await httpClient.GetStringAsync(FEED_URL, cancellationToken);
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

                // Optional. When missing, callers construct a GitHub zip URL; never fall back to <url> / <url-cli>.
                var avaloniaTemplate = doc.DocumentElement?.SelectSingleNode("/item/url-avalonia")?.InnerText.Trim();
                if (string.IsNullOrWhiteSpace(avaloniaTemplate))
                    avaloniaTemplate = null;

                return new UpdateCheckResult(remote, remote > localVersion, changelog, avaloniaTemplate);
            }
            catch
            {
                return null;
            }
        }
    }
}
