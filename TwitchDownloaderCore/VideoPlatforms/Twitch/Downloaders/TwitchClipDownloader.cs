using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;

namespace TwitchDownloaderCore.VideoPlatforms.Twitch.Downloaders
{
    public sealed class TwitchClipDownloader : IClipDownloader
    {
        private readonly ClipDownloadOptions downloadOptions;
        private readonly IProgress<ProgressReport> _progress;

        public TwitchClipDownloader(ClipDownloadOptions clipDownloadOptions, IProgress<ProgressReport> progress)
        {
            downloadOptions = clipDownloadOptions;
            _progress = progress;
            downloadOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder,
                "TwitchDownloader");
        }

        public async Task DownloadAsync(CancellationToken cancellationToken)
        {
            _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Fetching Clip Info"));

            var downloadUrl = await GetDownloadUrl();

            cancellationToken.ThrowIfCancellationRequested();

            var clipDirectory = Directory.GetParent(Path.GetFullPath(downloadOptions.Filename))!;
            if (!clipDirectory.Exists)
            {
                PlatformHelper.CreateDirectory(clipDirectory.FullName);
            }

            _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Downloading Clip 0%"));

            void DownloadProgressHandler(StreamCopyProgress streamProgress)
            {
                var percent = (int)(streamProgress.BytesCopied / (double)streamProgress.SourceLength * 100);
                _progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Downloading Clip {percent}%"));
                _progress.Report(new ProgressReport(percent));
            }

            if (!downloadOptions.EncodeMetadata)
            {
                await DownloadTools.DownloadClipFileTaskAsync(downloadUrl, downloadOptions.Filename, downloadOptions.ThrottleKib, new Progress<StreamCopyProgress>(DownloadProgressHandler), cancellationToken);
                return;
            }

            if (!Directory.Exists(downloadOptions.TempFolder))
            {
                PlatformHelper.CreateDirectory(downloadOptions.TempFolder);
            }

            var tempFile = Path.Combine(downloadOptions.TempFolder, $"clip_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}_{Path.GetRandomFileName()}");
            try
            {
                await DownloadTools.DownloadClipFileTaskAsync(downloadUrl, tempFile, downloadOptions.ThrottleKib, new Progress<StreamCopyProgress>(DownloadProgressHandler), cancellationToken);

                _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Encoding Clip Metadata 0%"));
                _progress.Report(new ProgressReport(0));

                await EncodeClipMetadata(tempFile, downloadOptions.Filename, cancellationToken);

                _progress.Report(new ProgressReport(ReportType.SameLineStatus, "Encoding Clip Metadata 100%"));
                _progress.Report(new ProgressReport(100));
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        private async Task<string> GetDownloadUrl()
        {
            var listLinks = await TwitchHelper.GetClipLinks(downloadOptions.Id);

            if (listLinks[0].data.clip.playbackAccessToken is null)
            {
                throw new NullReferenceException("Invalid Clip, deleted possibly?");
            }

            if (listLinks[0].data.clip.videoQualities is null || listLinks[0].data.clip.videoQualities.Count == 0)
            {
                throw new NullReferenceException("Clip has no video qualities, deleted possibly?");
            }

            string downloadUrl = "";

            foreach (var quality in listLinks[0].data.clip.videoQualities)
            {
                if (quality.quality + "p" + (quality.frameRate.ToString() == "30" ? "" : quality.frameRate.ToString()) == downloadOptions.Quality)
                {
                    downloadUrl = quality.sourceURL;
                }
            }

            if (downloadUrl == "")
            {
                downloadUrl = listLinks[0].data.clip.videoQualities.First().sourceURL;
            }

            return downloadUrl + "?sig=" + listLinks[0].data.clip.playbackAccessToken.signature + "&token=" + HttpUtility.UrlEncode(listLinks[0].data.clip.playbackAccessToken.value);
        }

        private async Task EncodeClipMetadata(string inputFile, string destinationFile, CancellationToken cancellationToken)
        {
            var metadataFile = $"{Path.GetFileNameWithoutExtension(inputFile)}_metadata{Path.GetExtension(inputFile)}";
            var clipInfo = await TwitchHelper.GetClipInfo(downloadOptions.Id);

            try
            {
                await FfmpegMetadata.SerializeAsync(metadataFile, clipInfo.data.clip.broadcaster.displayName, downloadOptions.Id, clipInfo.data.clip.title, clipInfo.data.clip.createdAt,
                    clipInfo.data.clip.viewCount, cancellationToken: cancellationToken);

                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = downloadOptions.FfmpegPath,
                        Arguments = $"-i \"{inputFile}\" -i \"{metadataFile}\" -map_metadata 1 -y -c copy \"{destinationFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync(cancellationToken);
            }
            finally
            {
                File.Delete(metadataFile);
            }
        }
    }
}
