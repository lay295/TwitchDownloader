using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;

namespace TwitchDownloaderCore.VideoPlatforms.Kick.Downloaders
{
    public sealed class KickClipDownloader : IClipDownloader
    {
        private readonly ClipDownloadOptions downloadOptions;
        private readonly IProgress<ProgressReport> _progress;

        public KickClipDownloader(ClipDownloadOptions clipDownloadOptions, IProgress<ProgressReport> progress)
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

            cancellationToken.ThrowIfCancellationRequested();

            KickClipResponse response = await KickHelper.GetClipInfo(downloadOptions.Id);
            bool alreadyEncoded = response.VideoUrl.EndsWith(".mp4"); // Kick clips can point to an m3u8 playlist or an already encoded mp4

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

            if (!downloadOptions.EncodeMetadata && alreadyEncoded)
            {
                await DownloadTools.DownloadFileAsync(response.VideoUrl, downloadOptions.Filename, downloadOptions.ThrottleKib, new Progress<StreamCopyProgress>(DownloadProgressHandler), cancellationToken);
                return;
            }

            if (!Directory.Exists(downloadOptions.TempFolder))
            {
                PlatformHelper.CreateDirectory(downloadOptions.TempFolder);
            }

            var tempDownloadFolder = Path.Combine(downloadOptions.TempFolder, $"clip_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}_{Path.GetRandomFileName()}");
            PlatformHelper.CreateDirectory(tempDownloadFolder);
            var tempFile = Path.Combine(tempDownloadFolder, $"clip_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}_{Path.GetRandomFileName()}");
            try
            {
                if (!alreadyEncoded)
                {
                    string playlistData = await KickHelper.GetString(response.VideoUrl);
                    string baseUrl = response.VideoUrl[..(response.VideoUrl.LastIndexOf('/') + 1)];
                    var playlist = M3U8.Parse(playlistData, baseUrl);

                    await using var outputStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                    for (int i = 0; i < playlist.Streams.Length; i++)
                    {
                        var videoPart = playlist.Streams[i];
                        string downloadPath = Path.Combine(tempDownloadFolder, Path.GetFileName(videoPart.Path)!);
                        await DownloadTools.DownloadFileAsync(videoPart.Path, downloadPath, downloadOptions.ThrottleKib, null, cancellationToken);

                        await using (var fs = File.Open(downloadPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            fs.Seek(videoPart.ByteRange.Start, SeekOrigin.Begin);
                            await fs.CopyBytesToAsync(outputStream, videoPart.ByteRange.Length, cancellationToken);
                        }

                        try
                        {
                            File.Delete(downloadPath);
                        }
                        catch { /* Oh well, it should get cleaned up later */ }

                        var percent = (int)((i+1) / (double)playlist.Streams.Length * 100);
                        _progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Downloading Clip {percent}%"));
                        _progress.Report(new ProgressReport(percent));
                    }
                }
                else
                {
                    await DownloadTools.DownloadFileAsync(response.VideoUrl, tempFile, downloadOptions.ThrottleKib, new Progress<StreamCopyProgress>(DownloadProgressHandler), cancellationToken);
                }

                _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Encoding Clip Metadata 0%"));
                _progress.Report(new ProgressReport(0));

                await EncodeClipMetadata(tempFile, downloadOptions.Filename, response, cancellationToken);

                _progress.Report(new ProgressReport(ReportType.SameLineStatus, "Encoding Clip Metadata 100%"));
                _progress.Report(new ProgressReport(100));
            }
            finally
            {
                await Task.Delay(300, CancellationToken.None); // Wait a bit for any download threads to be killed.
                Directory.Delete(tempDownloadFolder, true);
            }
        }

        private async Task EncodeClipMetadata(string inputFile, string destinationFile, KickClipResponse clipInfo, CancellationToken cancellationToken)
        {
            var metadataFile = $"{Path.GetFileNameWithoutExtension(inputFile)}_metadata{Path.GetExtension(inputFile)}";

            try
            {
                await FfmpegMetadata.SerializeAsync(metadataFile, clipInfo.StreamerName, downloadOptions.Id, clipInfo.Title, clipInfo.CreatedAt,
                    clipInfo.ViewCount, cancellationToken: cancellationToken);

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
