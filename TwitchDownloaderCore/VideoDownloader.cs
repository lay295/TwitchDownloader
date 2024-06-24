using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed class VideoDownloader
    {
        private readonly VideoDownloadOptions downloadOptions;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        private readonly ITaskProgress _progress;
        private bool _shouldClearCache = true;

        public VideoDownloader(VideoDownloadOptions videoDownloadOptions, ITaskProgress progress = default)
        {
            downloadOptions = videoDownloadOptions;
            downloadOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder,
                "TwitchDownloader");
            downloadOptions.TrimBeginningTime = downloadOptions.TrimBeginningTime >= TimeSpan.Zero ? downloadOptions.TrimBeginningTime : TimeSpan.Zero;
            downloadOptions.TrimEndingTime = downloadOptions.TrimEndingTime >= TimeSpan.Zero ? downloadOptions.TrimEndingTime : TimeSpan.Zero;
            _progress = progress;
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

                outputFileInfo.Refresh();
                if (outputFileInfo.Exists && outputFileInfo.Length == 0)
                {
                    try
                    {
                        await outputFs.DisposeAsync();
                        outputFileInfo.Delete();
                    }
                    catch { }
                }

                throw;
            }
        }

        private async Task DownloadAsyncImpl(FileInfo outputFileInfo, FileStream outputFs, CancellationToken cancellationToken)
        {
            await TwitchHelper.CleanupAbandonedVideoCaches(downloadOptions.TempFolder, downloadOptions.CacheCleanerCallback, _progress);

            string downloadFolder = Path.Combine(
                downloadOptions.TempFolder,
                $"{downloadOptions.Id}_{DateTimeOffset.UtcNow.Ticks}");

            _progress.SetStatus("Fetching Video Info [1/4]");

            try
            {
                GqlVideoResponse videoInfoResponse = await TwitchHelper.GetVideoInfo(downloadOptions.Id);
                if (videoInfoResponse.data.video == null)
                {
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
                }

                GqlVideoChapterResponse videoChapterResponse = await TwitchHelper.GetOrGenerateVideoChapters(downloadOptions.Id, videoInfoResponse.data.video);

                var qualityPlaylist = await GetQualityPlaylist();

                var playlistUrl = qualityPlaylist.Path;
                var baseUrl = new Uri(playlistUrl[..(playlistUrl.LastIndexOf('/') + 1)], UriKind.Absolute);

                var videoInfo = videoInfoResponse.data.video;
                var (playlist, airDate) = await GetVideoPlaylist(playlistUrl, cancellationToken);

                var videoListCrop = GetStreamListTrim(playlist.Streams, videoInfo, out var videoLength, out var startOffset, out var endOffset);

                CheckAvailableStorageSpace(qualityPlaylist.StreamInfo.Bandwidth, videoLength);

                if (Directory.Exists(downloadFolder))
                    Directory.Delete(downloadFolder, true);
                TwitchHelper.CreateDirectory(downloadFolder);

                _progress.SetTemplateStatus("Downloading {0}% [2/4]", 0);

                await DownloadVideoPartsAsync(playlist.Streams, videoListCrop, baseUrl, downloadFolder, airDate, cancellationToken);

                _progress.SetTemplateStatus("Verifying Parts {0}% [3/4]", 0);

                await VerifyDownloadedParts(playlist.Streams, videoListCrop, baseUrl, downloadFolder, airDate, cancellationToken);

                _progress.SetTemplateStatus("Finalizing Video {0}% [4/4]", 0);

                string metadataPath = Path.Combine(downloadFolder, "metadata.txt");
                await FfmpegMetadata.SerializeAsync(metadataPath, videoInfo.owner.displayName, downloadOptions.Id.ToString(), videoInfo.title, videoInfo.createdAt, videoInfo.viewCount,
                    videoInfo.description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd(), downloadOptions.TrimBeginning ? downloadOptions.TrimBeginningTime : TimeSpan.Zero,
                    videoLength, videoChapterResponse.data.video.moments.edges, cancellationToken);

                var concatListPath = Path.Combine(downloadFolder, "concat.txt");
                await FfmpegConcatList.SerializeAsync(concatListPath, playlist, videoListCrop, cancellationToken);

                outputFs.Close();

                int ffmpegExitCode;
                var ffmpegRetries = 0;
                do
                {
                    ffmpegExitCode = await Task.Run(() => RunFfmpegVideoCopy(downloadFolder, outputFileInfo, concatListPath, metadataPath, startOffset, endOffset, videoLength), cancellationToken);
                    if (ffmpegExitCode != 0)
                    {
                        _progress.LogError($"Failed to finalize video (code {ffmpegExitCode}), retrying in 10 seconds...");
                        await Task.Delay(10_000, cancellationToken);
                    }
                } while (ffmpegExitCode != 0 && ffmpegRetries++ < 1);

                outputFileInfo.Refresh();
                if (ffmpegExitCode != 0 || !outputFileInfo.Exists)
                {
                    _shouldClearCache = false;
                    throw new Exception($"Failed to finalize video. The download cache has not been cleared and can be found at {downloadFolder} along with a log file.");
                }

                _progress.ReportProgress(100);
            }
            finally
            {
                await Task.Delay(100, CancellationToken.None);

                if (_shouldClearCache)
                {
                    Cleanup(downloadFolder);
                }
            }
        }

        private void CheckAvailableStorageSpace(int bandwidth, TimeSpan videoLength)
        {
            var videoSizeInBytes = VideoSizeEstimator.EstimateVideoSize(bandwidth,
                downloadOptions.TrimBeginning ? downloadOptions.TrimBeginningTime : TimeSpan.Zero,
                downloadOptions.TrimEnding ? downloadOptions.TrimEndingTime : videoLength);
            var tempFolderDrive = DriveHelper.GetOutputDrive(downloadOptions.TempFolder);
            var destinationDrive = DriveHelper.GetOutputDrive(downloadOptions.Filename);

            if (tempFolderDrive.Name == destinationDrive.Name)
            {
                if (tempFolderDrive.AvailableFreeSpace < videoSizeInBytes * 2)
                {
                    _progress.LogWarning($"The drive '{tempFolderDrive.Name}' may not have enough free space to complete the download.");
                }
            }
            else
            {
                if (tempFolderDrive.AvailableFreeSpace < videoSizeInBytes)
                {
                    // More drive space is needed by the raw ts files due to repeat metadata, but the amount of metadata packets can vary between files so we won't bother.
                    _progress.LogWarning($"The drive '{tempFolderDrive.Name}' may not have enough free space to complete the download.");
                }

                if (destinationDrive.AvailableFreeSpace < videoSizeInBytes)
                {
                    _progress.LogWarning($"The drive '{destinationDrive.Name}' may not have enough free space to complete finalization.");
                }
            }
        }

        private async Task DownloadVideoPartsAsync(IEnumerable<M3U8.Stream> playlist, Range videoListCrop, Uri baseUrl, string downloadFolder, DateTimeOffset vodAirDate, CancellationToken cancellationToken)
        {
            var partCount = videoListCrop.End.Value - videoListCrop.Start.Value;
            var videoPartsQueue = new ConcurrentQueue<string>(playlist.Take(videoListCrop).Select(x => x.Path));

            var downloadThreads = new VideoDownloadThread[downloadOptions.DownloadThreads];
            for (var i = 0; i < downloadOptions.DownloadThreads; i++)
            {
                downloadThreads[i] = new VideoDownloadThread(videoPartsQueue, _httpClient, baseUrl, downloadFolder, vodAirDate, downloadOptions.ThrottleKib, _progress, cancellationToken);
            }

            var downloadExceptions = await WaitForDownloadThreads(downloadThreads, videoPartsQueue, partCount, cancellationToken);

            LogDownloadThreadExceptions(downloadExceptions);
        }

        private async Task<IReadOnlyCollection<Exception>> WaitForDownloadThreads(VideoDownloadThread[] downloadThreads, ConcurrentQueue<string> videoPartsQueue, int partCount, CancellationToken cancellationToken)
        {
            var allThreadsExited = false;
            var previousDoneCount = 0;
            var restartedThreads = 0;
            var maxRestartedThreads = (int)Math.Max(downloadOptions.DownloadThreads * 1.5, 10);
            var downloadExceptions = new Dictionary<int, Exception>();
            do
            {
                if (videoPartsQueue.Count != previousDoneCount)
                {
                    previousDoneCount = videoPartsQueue.Count;
                    var percent = (int)((partCount - previousDoneCount) / (double)partCount * 100);
                    _progress.ReportProgress(percent);
                }

                allThreadsExited = true;
                foreach (var thread in downloadThreads)
                {
                    var task = thread.ThreadTask;

                    if (task.IsFaulted)
                    {
                        downloadExceptions.TryAdd(task.Id, task.Exception);

                        if (restartedThreads <= maxRestartedThreads)
                        {
                            thread.StartDownload();
                            restartedThreads++;
                        }
                    }

                    if (allThreadsExited && !task.IsCompleted)
                    {
                        allThreadsExited = false;
                    }
                }

                await Task.Delay(100, cancellationToken);
            } while (!allThreadsExited);

            _progress.ReportProgress(100);

            if (restartedThreads == maxRestartedThreads)
            {
                throw new AggregateException("The download thread restart limit was reached.", downloadExceptions.Values);
            }

            return downloadExceptions.Values;
        }

        private void LogDownloadThreadExceptions(IReadOnlyCollection<Exception> downloadExceptions)
        {
            if (downloadExceptions.Count == 0)
                return;

            var culpritList = new List<string>();
            var sb = new StringBuilder();
            foreach (var downloadException in downloadExceptions)
            {
                var ex = downloadException switch
                {
                    AggregateException { InnerException: { } innerException } => innerException,
                    _ => downloadException
                };

                // Try to only log exceptions from different sources
                var culprit = ex.TargetSite?.Name;
                if (string.IsNullOrEmpty(culprit) || culpritList.Contains(culprit))
                    continue;

                sb.EnsureCapacity(sb.Capacity + ex.Message.Length + culprit.Length + 6);
                sb.Append(", ");
                sb.Append(ex.Message);
                sb.Append(" at ");
                sb.Append(culprit);
                culpritList.Add(culprit);
            }

            if (sb.Length == 0)
            {
                return;
            }

            sb.Replace(",", $"{downloadExceptions.Count} errors were encountered while downloading:", 0, 1);
            _progress.LogInfo(sb.ToString());
        }

        private async Task VerifyDownloadedParts(ICollection<M3U8.Stream> playlist, Range videoListCrop, Uri baseUrl, string downloadFolder, DateTimeOffset vodAirDate, CancellationToken cancellationToken)
        {
            var failedParts = new List<M3U8.Stream>();
            var partCount = videoListCrop.End.Value - videoListCrop.Start.Value;
            var doneCount = 0;

            foreach (var part in playlist.Take(videoListCrop))
            {
                var filePath = Path.Combine(downloadFolder, DownloadTools.RemoveQueryString(part.Path));
                var fi = new FileInfo(filePath);
                if (!fi.Exists || fi.Length == 0)
                {
                    failedParts.Add(part);
                }

                doneCount++;
                var percent = (int)(doneCount / (double)partCount * 100);
                _progress.ReportProgress(percent);

                cancellationToken.ThrowIfCancellationRequested();
            }

            if (failedParts.Count != 0)
            {
                if (partCount > 20 && failedParts.Count >= partCount * 0.95)
                {
                    // 19/20 parts are missing or empty, something went horribly wrong.
                    // TODO: Somehow let the user bypass this. Maybe with callbacks?
                    throw new Exception($"Too many parts are missing ({failedParts.Count}/{partCount}), aborting.");
                }

                _progress.LogInfo($"The following parts will be redownloaded: {string.Join(", ", failedParts)}");
                await DownloadVideoPartsAsync(failedParts, videoListCrop, baseUrl, downloadFolder, vodAirDate, cancellationToken);
            }
        }

        private int RunFfmpegVideoCopy(string tempFolder, FileInfo outputFile, string concatListPath, string metadataPath, decimal startOffset, decimal endOffset, TimeSpan videoLength)
        {
            using var process = new Process
            {
                StartInfo =
                {
                    FileName = downloadOptions.FfmpegPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = tempFolder
                }
            };

            var args = new List<string>
            {
                "-stats",
                "-y",
                "-avoid_negative_ts", "make_zero",
                "-analyzeduration", $"{int.MaxValue}",
                "-probesize", $"{int.MaxValue}",
                "-f", "concat",
                "-i", concatListPath,
                "-i", metadataPath,
                "-map_metadata", "1",
                "-c", "copy",
                outputFile.FullName
            };

            // TODO: Make this optional - "Safe" and "Exact" trimming methods
            if (endOffset > 0)
            {
                args.Insert(0, "-t");
                args.Insert(1, videoLength.TotalSeconds.ToString(CultureInfo.InvariantCulture));
            }

            if (startOffset > 0)
            {
                args.Insert(0, "-ss");
                args.Insert(1, startOffset.ToString(CultureInfo.InvariantCulture));
            }

            foreach (var arg in args)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            var encodingTimeRegex = new Regex(@"(?<=time=)(\d\d):(\d\d):(\d\d)\.(\d\d)", RegexOptions.Compiled);
            var logQueue = new ConcurrentQueue<string>();

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is null)
                    return;

                logQueue.Enqueue(e.Data); // We cannot use -report ffmpeg arg because it redirects stderr

                HandleFfmpegOutput(e.Data, encodingTimeRegex, videoLength);
            };

            process.Start();
            process.BeginErrorReadLine();

            using var logWriter = File.AppendText(Path.Combine(tempFolder, "ffmpegLog.txt"));
            logWriter.AutoFlush = true;
            do // We cannot handle logging inside the ErrorDataReceived lambda because more than 1 can come in at once and cause a race condition. lay295#598
            {
                Thread.Sleep(100);
                while (!logQueue.IsEmpty && logQueue.TryDequeue(out var logMessage))
                {
                    logWriter.WriteLine(logMessage);
                }
            } while (!process.HasExited || !logQueue.IsEmpty);

            return process.ExitCode;
        }

        private void HandleFfmpegOutput(string output, Regex encodingTimeRegex, TimeSpan videoLength)
        {
            var encodingTimeMatch = encodingTimeRegex.Match(output);
            if (!encodingTimeMatch.Success)
                return;

            // TimeSpan.Parse insists that hours cannot be greater than 24, thus we must use the TimeSpan ctor.
            if (!int.TryParse(encodingTimeMatch.Groups[1].ValueSpan, out var hours)) return;
            if (!int.TryParse(encodingTimeMatch.Groups[2].ValueSpan, out var minutes)) return;
            if (!int.TryParse(encodingTimeMatch.Groups[3].ValueSpan, out var seconds)) return;
            if (!int.TryParse(encodingTimeMatch.Groups[4].ValueSpan, out var milliseconds)) return;
            var encodingTime = new TimeSpan(0, hours, minutes, seconds, milliseconds);

            var percent = (int)Math.Round(encodingTime / videoLength * 100);

            _progress.ReportProgress(Math.Clamp(percent, 0, 100));
        }

        private async Task<(M3U8 playlist, DateTimeOffset airDate)> GetVideoPlaylist(string playlistUrl, CancellationToken cancellationToken)
        {
            var playlistString = await _httpClient.GetStringAsync(playlistUrl, cancellationToken);
            var playlist = M3U8.Parse(playlistString);

            var airDate = DateTimeOffset.UtcNow.AddHours(-25);
            var airDateKvp = playlist.FileMetadata.UnparsedValues.FirstOrDefault(x => x.Key == "#ID3-EQUIV-TDTG:");
            if (DateTimeOffset.TryParse(airDateKvp.Value, out var vodAirDate))
            {
                airDate = vodAirDate;
            }

            return (playlist, airDate);
        }

        private Range GetStreamListTrim(IList<M3U8.Stream> streamList, VideoInfo videoInfo, out TimeSpan videoLength, out decimal startOffset, out decimal endOffset)
        {
            startOffset = 0;
            endOffset = 0;

            var startIndex = 0;
            if (downloadOptions.TrimBeginning)
            {
                var startTime = 0m;
                var trimTotalSeconds = (decimal)downloadOptions.TrimBeginningTime.TotalSeconds;
                foreach (var videoPart in streamList)
                {
                    if (startTime + videoPart.PartInfo.Duration > trimTotalSeconds)
                    {
                        startOffset = trimTotalSeconds - startTime;
                        break;
                    }

                    startIndex++;
                    startTime += videoPart.PartInfo.Duration;
                }
            }

            var endIndex = streamList.Count;
            if (downloadOptions.TrimEnding)
            {
                var endTime = streamList.Sum(x => x.PartInfo.Duration);
                var trimTotalSeconds = (decimal)downloadOptions.TrimEndingTime.TotalSeconds;
                for (var i = streamList.Count - 1; i >= 0; i--)
                {
                    var videoPart = streamList[i];
                    if (endTime - videoPart.PartInfo.Duration < trimTotalSeconds)
                    {
                        var offset = endTime - trimTotalSeconds;
                        if (offset > 0) endOffset = videoPart.PartInfo.Duration - offset;

                        break;
                    }

                    endIndex--;
                    endTime -= videoPart.PartInfo.Duration;
                }
            }

            videoLength =
                (downloadOptions.TrimEnding ? downloadOptions.TrimEndingTime : TimeSpan.FromSeconds(videoInfo.lengthSeconds))
                - (downloadOptions.TrimBeginning ? downloadOptions.TrimBeginningTime : TimeSpan.Zero);

            return new Range(startIndex, endIndex);
        }

        private async Task<M3U8.Stream> GetQualityPlaylist()
        {
            GqlVideoTokenResponse accessToken = await TwitchHelper.GetVideoToken(downloadOptions.Id, downloadOptions.Oauth);

            if (accessToken.data.videoPlaybackAccessToken is null)
            {
                throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
            }

            var playlistString = await TwitchHelper.GetVideoPlaylist(downloadOptions.Id, accessToken.data.videoPlaybackAccessToken.value, accessToken.data.videoPlaybackAccessToken.signature);
            if (playlistString.Contains("vod_manifest_restricted") || playlistString.Contains("unauthorized_entitlements"))
            {
                throw new NullReferenceException("Insufficient access to VOD, OAuth may be required.");
            }

            var m3u8 = M3U8.Parse(playlistString);

            return m3u8.GetStreamOfQuality(downloadOptions.Quality);
        }

        private void Cleanup(string downloadFolder)
        {
            try
            {
                if (Directory.Exists(downloadFolder))
                {
                    Directory.Delete(downloadFolder, true);
                }
            }
            catch (IOException e)
            {
                _progress.LogWarning($"Failed to delete download cache: {e.Message}");
            }
        }
    }
}