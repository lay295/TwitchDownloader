using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed class ClipDownloader
    {
        private readonly ClipDownloadOptions downloadOptions;
        private readonly ITaskProgress _progress;
        private static readonly HttpClient HttpClient = new();

        public ClipDownloader(ClipDownloadOptions clipDownloadOptions, ITaskProgress progress)
        {
            downloadOptions = clipDownloadOptions;
            _progress = progress;
            downloadOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder,
                "TwitchDownloader");
        }

        public async Task DownloadAsync(CancellationToken cancellationToken)
        {
            _progress.SetStatus("Fetching Clip Info");

            var downloadUrl = await GetDownloadUrl();
            var clipInfo = await TwitchHelper.GetClipInfo(downloadOptions.Id);

            cancellationToken.ThrowIfCancellationRequested();

            var clipDirectory = Directory.GetParent(Path.GetFullPath(downloadOptions.Filename))!;
            if (!clipDirectory.Exists)
            {
                TwitchHelper.CreateDirectory(clipDirectory.FullName);
            }

            _progress.SetTemplateStatus("Downloading Clip {0}%", 0);

            void DownloadProgressHandler(StreamCopyProgress streamProgress)
            {
                var percent = (int)(streamProgress.BytesCopied / (double)streamProgress.SourceLength * 100);
                _progress.ReportProgress(percent);
            }

            if (!downloadOptions.EncodeMetadata)
            {
                await DownloadFileTaskAsync(downloadUrl, downloadOptions.Filename, downloadOptions.ThrottleKib, new Progress<StreamCopyProgress>(DownloadProgressHandler), cancellationToken);
                return;
            }

            if (!Directory.Exists(downloadOptions.TempFolder))
            {
                TwitchHelper.CreateDirectory(downloadOptions.TempFolder);
            }

            var tempFile = Path.Combine(downloadOptions.TempFolder, $"{downloadOptions.Id}_{DateTimeOffset.UtcNow.Ticks}.mp4");
            try
            {
                await DownloadFileTaskAsync(downloadUrl, tempFile, downloadOptions.ThrottleKib, new Progress<StreamCopyProgress>(DownloadProgressHandler), cancellationToken);

                _progress.SetTemplateStatus("Encoding Clip Metadata {0}%", 0);

                var clipChapter = TwitchHelper.GenerateClipChapter(clipInfo.data.clip);
                await EncodeClipWithMetadata(tempFile, downloadOptions.Filename, clipInfo.data.clip, clipChapter, cancellationToken);

                if (!File.Exists(downloadOptions.Filename))
                {
                    File.Move(tempFile, downloadOptions.Filename);
                    _progress.LogError("Unable to serialize metadata. The download has been completed without custom metadata.");
                }

                _progress.ReportProgress(100);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        private async Task<string> GetDownloadUrl()
        {
            var listLinks = await TwitchHelper.GetClipLinks(downloadOptions.Id);
            var clip = listLinks.data.clip;

            if (clip.playbackAccessToken is null)
            {
                throw new NullReferenceException("Invalid Clip, deleted possibly?");
            }

            if (clip.videoQualities is null || clip.videoQualities.Length == 0)
            {
                throw new NullReferenceException("Clip has no video qualities, deleted possibly?");
            }

            string downloadUrl = "";

            foreach (var quality in clip.videoQualities)
            {
                if (quality.quality + "p" + (Math.Round(quality.frameRate) == 30 ? "" : Math.Round(quality.frameRate).ToString("F0")) == downloadOptions.Quality)
                {
                    downloadUrl = quality.sourceURL;
                }
            }

            if (downloadUrl == "")
            {
                downloadUrl = clip.videoQualities.First().sourceURL;
            }

            return downloadUrl + "?sig=" + clip.playbackAccessToken.signature + "&token=" + HttpUtility.UrlEncode(clip.playbackAccessToken.value);
        }

        private static async Task DownloadFileTaskAsync(string url, string destinationFile, int throttleKib, IProgress<StreamCopyProgress> progress, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;

            if (throttleKib == -1)
            {
                await using var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await contentStream.ProgressCopyToAsync(fs, contentLength, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await using var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var throttledStream = new ThrottledStream(contentStream, throttleKib);
                await throttledStream.ProgressCopyToAsync(fs, contentLength, progress, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task EncodeClipWithMetadata(string inputFile, string destinationFile, Clip clipMetadata, VideoMomentEdge clipChapter, CancellationToken cancellationToken)
        {
            var metadataFile = $"{inputFile}_metadata.txt";

            Process process = null;
            try
            {
                await FfmpegMetadata.SerializeAsync(metadataFile, clipMetadata.broadcaster?.displayName, downloadOptions.Id, clipMetadata.title, clipMetadata.createdAt, clipMetadata.viewCount,
                    videoMomentEdges: new[] { clipChapter }, cancellationToken: cancellationToken);

                process = new Process
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
                process.BeginErrorReadLine();

                // If the process has exited before we call WaitForExitAsync, the thread locks up.
                if (process.HasExited)
                    return;

                await process.WaitForExitAsync(cancellationToken);
            }
            finally
            {
                if (process is { HasExited: false })
                {
                    process.Kill();
                    await Task.Delay(100, cancellationToken);
                }

                File.Delete(metadataFile);
            }
        }
    }
}
