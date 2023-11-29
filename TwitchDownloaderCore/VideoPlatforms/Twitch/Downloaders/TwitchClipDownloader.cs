using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Twitch.Gql;

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
            var clipInfo = await TwitchHelper.GetClipInfo(downloadOptions.Id);

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
                await DownloadTools.DownloadFileAsync(downloadUrl, downloadOptions.Filename, downloadOptions.ThrottleKib, new Progress<StreamCopyProgress>(DownloadProgressHandler), cancellationToken);
                return;
            }

            if (!Directory.Exists(downloadOptions.TempFolder))
            {
                PlatformHelper.CreateDirectory(downloadOptions.TempFolder);
            }

            var tempFile = Path.Combine(downloadOptions.TempFolder, $"{downloadOptions.Id}_{DateTimeOffset.UtcNow.Ticks}.mp4");
            try
            {
                await DownloadTools.DownloadFileAsync(downloadUrl, tempFile, downloadOptions.ThrottleKib, new Progress<StreamCopyProgress>(DownloadProgressHandler), cancellationToken);

                _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Encoding Clip Metadata 0%"));
                _progress.Report(new ProgressReport(0));

                var clipChapter = TwitchHelper.GenerateClipChapter(clipInfo.data.clip);
                await EncodeClipWithMetadata(tempFile, downloadOptions.Filename, clipInfo.data.clip, clipChapter, cancellationToken);

                if (!File.Exists(downloadOptions.Filename))
                {
                    File.Move(tempFile, downloadOptions.Filename);
                    throw new FileNotFoundException("Unable to serialize metadata (is FFmpeg missing?). The download has been completed without custom metadata.");
                }

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
            var clip = listLinks[0].data.clip;

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

        private async Task EncodeClipWithMetadata(string inputFile, string destinationFile, Clip clipMetadata, VideoMomentEdge clipChapter, CancellationToken cancellationToken)
        {
            var metadataFile = $"{Path.GetFileName(inputFile)}_metadata.txt";

            try
            {
                await FfmpegMetadata.SerializeAsync(metadataFile, clipMetadata.broadcaster.displayName, downloadOptions.Id, clipMetadata.title, clipMetadata.createdAt, clipMetadata.viewCount,
                    videoMomentEdges: new[] { clipChapter }, cancellationToken: cancellationToken);

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

                // If the process has exited before we call WaitForExitAsync, the thread locks up.
                // This was probably not intended by the .NET team, but it's an issue regardless.
                if (process.HasExited)
                    return;

                await process.WaitForExitAsync(cancellationToken);
            }
            finally
            {
                File.Delete(metadataFile);
            }
        }
    }
}
