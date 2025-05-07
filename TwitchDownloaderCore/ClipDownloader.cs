using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed class ClipDownloader
    {
        private readonly ClipDownloadOptions downloadOptions;
        private readonly ITaskProgress _progress;
        private static readonly HttpClient HttpClient = new();
        private readonly string _cacheDir;

        public ClipDownloader(ClipDownloadOptions clipDownloadOptions, ITaskProgress progress)
        {
            downloadOptions = clipDownloadOptions;
            _progress = progress;
            _cacheDir = CacheDirectoryService.GetCacheDirectory(downloadOptions.TempFolder);
        }

        public async Task DownloadAsync(CancellationToken cancellationToken)
        {
            var outputFileInfo = TwitchHelper.ClaimFile(downloadOptions.Filename, downloadOptions.FileCollisionCallback, _progress);
            downloadOptions.Filename = outputFileInfo.FullName;

            // Open the destination file so that it exists in the filesystem.
            await using var outputFs = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

            try
            {
                await DownloadAsyncImpl(outputFileInfo, outputFs, cancellationToken);
            }
            catch
            {
                await Task.Delay(100, CancellationToken.None);

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, outputFs, _progress);

                throw;
            }
        }

        private async Task DownloadAsyncImpl(FileInfo outputFileInfo, FileStream outputFs, CancellationToken cancellationToken)
        {
            _progress.SetStatus("Fetching Clip Info");

            var (clipInfo, downloadUrl) = await GetClipInfo();

            cancellationToken.ThrowIfCancellationRequested();

            _progress.SetTemplateStatus("Downloading Clip {0}%", 0);

            if (!downloadOptions.EncodeMetadata)
            {
                await DownloadFileTaskAsync(downloadUrl, outputFs, downloadOptions.ThrottleKib, new Progress<StreamCopyProgress>(DownloadProgressHandler), cancellationToken);
                return;
            }

            if (!Directory.Exists(_cacheDir))
            {
                TwitchHelper.CreateDirectory(_cacheDir);
            }

            var tempFile = Path.Combine(_cacheDir, $"{downloadOptions.Id}_{DateTimeOffset.UtcNow.Ticks}.mp4");
            try
            {
                await using (var tempFileStream = File.Open(tempFile, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    await DownloadFileTaskAsync(downloadUrl, tempFileStream, downloadOptions.ThrottleKib, new Progress<StreamCopyProgress>(DownloadProgressHandler), cancellationToken);
                }

                outputFs.Close();

                _progress.SetTemplateStatus("Encoding Clip Metadata {0}%", 0);

                var clipChapter = TwitchHelper.GenerateClipChapter(clipInfo.data.clip);
                await EncodeClipWithMetadata(tempFile, outputFileInfo.FullName, clipInfo.data.clip, clipChapter, cancellationToken);

                outputFileInfo.Refresh();
                if (!outputFileInfo.Exists || outputFileInfo.Length == 0)
                {
                    File.Move(tempFile, outputFileInfo.FullName);
                    _progress.LogError("Unable to serialize metadata. The download has been completed without custom metadata.");
                }

                _progress.ReportProgress(100);
            }
            finally
            {
                await Task.Delay(100, CancellationToken.None);

                File.Delete(tempFile);
            }

            void DownloadProgressHandler(StreamCopyProgress streamProgress)
            {
                var percent = (int)(streamProgress.BytesCopied / (double)streamProgress.SourceLength * 100);
                _progress.ReportProgress(percent);
            }
        }

        private async Task<(GqlShareClipRenderStatusResponse clipInfo, string downloadUrl)> GetClipInfo()
        {
            var clipRenderStatus = await TwitchHelper.GetShareClipRenderStatus(downloadOptions.Id);
            var clip = clipRenderStatus.data.clip;

            if (clip.playbackAccessToken is null)
            {
                throw new NullReferenceException("Invalid Clip, deleted possibly?");
            }

            if (clip.assets is not { Length: > 0 } || clip.assets[0].videoQualities is not { Length: > 0 })
            {
                throw new NullReferenceException("Clip has no video qualities, deleted possibly?");
            }

            var qualityUrl = GetDownloadUrlForQuality(clip, downloadOptions.Quality);
            var downloadUrl = qualityUrl + "?sig=" + clip.playbackAccessToken.signature + "&token=" + HttpUtility.UrlEncode(clip.playbackAccessToken.value);

            return (clipRenderStatus, downloadUrl);
        }

        private static string GetDownloadUrlForQuality(ShareClipRenderStatusClip clip, string qualityString)
        {
            var qualities = VideoQualities.FromClip(clip);
            var userQuality = qualities.GetQuality(qualityString) ?? qualities.BestQuality();

            return userQuality?.Item.sourceURL ?? throw new NullReferenceException($"Unknown Quality: {qualityString}");
        }

        private static async Task DownloadFileTaskAsync(string url, FileStream fs, int throttleKib, IProgress<StreamCopyProgress> progress, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;

            if (throttleKib == -1)
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await contentStream.ProgressCopyToAsync(fs, contentLength, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var throttledStream = new ThrottledStream(contentStream, throttleKib);
                await throttledStream.ProgressCopyToAsync(fs, contentLength, progress, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task EncodeClipWithMetadata(string inputFile, string destinationFile, ShareClipRenderStatusClip clipMetadata, VideoMomentEdge clipChapter, CancellationToken cancellationToken)
        {
            var metadataFile = $"{inputFile}_metadata.txt";

            Process process = null;
            try
            {
                await FfmpegMetadata.SerializeAsync(metadataFile, downloadOptions.Id, clipMetadata, new[] { clipChapter });

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
