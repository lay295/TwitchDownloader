using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed class VideoDownloader
    {
        private readonly VideoDownloadOptions downloadOptions;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        private readonly ITaskProgress _progress;
        private readonly string _cacheDir;
        private readonly string _vodCacheDir;
        private bool _shouldClearCache = true;

        public VideoDownloader(VideoDownloadOptions videoDownloadOptions, ITaskProgress progress = default)
        {
            downloadOptions = videoDownloadOptions;
            _cacheDir = CacheDirectoryService.GetCacheDirectory(downloadOptions.TempFolder);
            _vodCacheDir = Path.Combine(_cacheDir, $"{downloadOptions.Id}_{DateTimeOffset.UtcNow.Ticks}");
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

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, outputFs, _progress);

                throw;
            }
        }

        private async Task DownloadAsyncImpl(FileInfo outputFileInfo, FileStream outputFs, CancellationToken cancellationToken)
        {
            await TwitchHelper.CleanupAbandonedVideoCaches(_cacheDir, downloadOptions.CacheCleanerCallback, _progress);

            _progress.SetStatus("Fetching Video Info [1/4]");

            try
            {
                GqlVideoResponse videoInfoResponse = await TwitchHelper.GetVideoInfo(downloadOptions.Id);
                if (videoInfoResponse.data.video == null)
                {
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
                }

                GqlVideoChapterResponse videoChapterResponse = await TwitchHelper.GetOrGenerateVideoChapters(downloadOptions.Id, videoInfoResponse.data.video);

                var (allQualityPaths, qualityPlaylist) = await GetQualityPlaylist();

                var playlistUrl = qualityPlaylist.Path;
                var baseUrl = new Uri(playlistUrl[..(playlistUrl.LastIndexOf('/') + 1)], UriKind.Absolute);

                var videoInfo = videoInfoResponse.data.video;
                var (playlist, airDate) = await GetVideoPlaylist(playlistUrl, cancellationToken);

                var videoListCrop = GetStreamListTrim(playlist.Streams, out var videoLength, out var startOffset, out var endDuration);

                CheckAvailableStorageSpace(qualityPlaylist.StreamInfo.Bandwidth, videoLength);

                if (Directory.Exists(_vodCacheDir))
                {
                    _progress.LogWarning("Download cache already exists!");
                }

                TwitchHelper.CreateDirectory(_vodCacheDir);

                if (qualityPlaylist.StreamInfo.Codecs.Any(x => x.StartsWith("av01")))
                {
                    _progress.LogWarning("AV1 VOD support is still experimental. " +
                                         "If you encounter playback issues, try using an FFmpeg-based application like MPV, Kdenlive, or Blender, or re-encode the video file as H.264/AVC or H.265/HEVC with FFmpeg or Handbrake.");
                }

                var headerFile = await GetHeaderFile(playlist, baseUrl, cancellationToken);

                _progress.SetTemplateStatus("Downloading {0}% [2/4]", 0);

                var downloadState = new VideoDownloadState(allQualityPaths, playlist.Streams, videoListCrop, baseUrl, headerFile, airDate);
                await DownloadVideoPartsAsync(downloadState, cancellationToken);

                _progress.SetTemplateStatus("Verifying Parts {0}% [3/4]", 0);

                await VerifyDownloadedParts(downloadState, cancellationToken);

                _progress.SetTemplateStatus("Finalizing Video {0}% [4/4]", 0);

                string metadataPath = Path.Combine(_vodCacheDir, "metadata.txt");
                await FfmpegMetadata.SerializeAsync(metadataPath, downloadOptions.Id.ToString(), videoInfo, downloadOptions.TrimBeginning ? downloadOptions.TrimBeginningTime : TimeSpan.Zero, videoLength,
                    videoChapterResponse.data.video.moments.edges);

                var concatListPath = Path.Combine(_vodCacheDir, "concat.txt");
                var streamIds = GetStreamIds(playlist);

                var validParts = playlist.Streams
                    .Take(videoListCrop)
                    .Where(x => File.Exists(Path.Combine(_vodCacheDir, DownloadTools.RemoveQueryString(x.Path))))
                    .ToArray();

                // This should never occur unless stub emission fails
                if (validParts.Length < videoListCrop.GetOffsetAndLength(playlist.Streams.Length).Length)
                {
                    // This is probably super inefficient, but it's a very cold path anyway
                    var missingParts = playlist.Streams
                        .Take(videoListCrop)
                        .Where(x => !validParts.Contains(x));

                    _progress.LogWarning($"The following parts could not be downloaded and will be missing from the finalized video: {string.Join(", ", missingParts.Select(x => x.Path))}");
                }

                await FfmpegConcatList.SerializeAsync(concatListPath, validParts, streamIds, cancellationToken);

                outputFs.Close();

                int ffmpegExitCode;
                var ffmpegRetries = 0;
                do
                {
                    ffmpegExitCode = await RunFfmpegVideoCopy(outputFileInfo, concatListPath, metadataPath, startOffset, endDuration, videoLength, ffmpegRetries > 0, cancellationToken);
                    if (ffmpegExitCode != 0)
                    {
                        _progress.LogError($"Failed to finalize video (code {ffmpegExitCode}), retrying in 5 seconds...");
                        await Task.Delay(5_000, cancellationToken);
                    }
                } while (ffmpegExitCode != 0 && ffmpegRetries++ < 1);

                outputFileInfo.Refresh();
                if (ffmpegExitCode != 0 || !outputFileInfo.Exists || outputFileInfo.Length == 0)
                {
                    _shouldClearCache = false;
                    throw new Exception($"Failed to finalize video. The download cache has not been cleared and can be found at {_vodCacheDir} along with a log file.");
                }

                _progress.ReportProgress(100);
            }
            finally
            {
                await Task.Delay(100, CancellationToken.None);

                if (_shouldClearCache)
                {
                    Cleanup(_vodCacheDir);
                }
            }
        }

        private async Task<string> GetHeaderFile(M3U8 playlist, Uri baseUrl, CancellationToken cancellationToken)
        {
            var map = playlist.FileMetadata.Map;
            if (string.IsNullOrWhiteSpace(map?.Uri))
            {
                return null;
            }

            if (map.ByteRange != default)
            {
                _progress.LogWarning($"Byte range was {map.ByteRange}, but is not yet implemented!");
            }

            var destinationFile = Path.Combine(_vodCacheDir, map.Uri);

            var uri = new Uri(baseUrl, map.Uri);
            _progress.LogVerbose($"Downloading header file from '{uri}' to '{destinationFile}'");

            await DownloadTools.DownloadFileAsync(_httpClient, uri, destinationFile, null, downloadOptions.ThrottleKib, _progress);

            return destinationFile;
        }

        private void CheckAvailableStorageSpace(int bandwidth, TimeSpan videoLength)
        {
            var videoSizeInBytes = VideoSizeEstimator.EstimateVideoSize(bandwidth,
                downloadOptions.TrimBeginning ? downloadOptions.TrimBeginningTime : TimeSpan.Zero,
                downloadOptions.TrimEnding ? downloadOptions.TrimEndingTime : videoLength);
            var tempFolderDrive = DriveHelper.GetOutputDrive(_vodCacheDir);
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

        private async Task DownloadVideoPartsAsync(VideoDownloadState downloadState, CancellationToken cancellationToken)
        {
            var downloadThreads = new VideoDownloadThread[downloadOptions.DownloadThreads];
            for (var i = 0; i < downloadOptions.DownloadThreads; i++)
            {
                downloadThreads[i] = new VideoDownloadThread(downloadState, _httpClient, _vodCacheDir, downloadOptions.ThrottleKib, _progress, cancellationToken);
            }

            var partCount = downloadState.PartCount;
            var downloadExceptions = await WaitForDownloadThreads(downloadThreads, downloadState, partCount, true, cancellationToken);

            LogDownloadThreadExceptions(downloadExceptions);
        }

        private async Task<IReadOnlyCollection<Exception>> WaitForDownloadThreads(VideoDownloadThread[] downloadThreads, VideoDownloadState downloadState, int partCount, bool limitThreadRestarts, CancellationToken cancellationToken)
        {
            var allThreadsExited = false;
            var previousDoneCount = 0;
            var restartedThreads = 0;
            var maxRestartedThreads = limitThreadRestarts ? (int)Math.Ceiling(partCount * 0.95) : int.MaxValue;
            var downloadExceptions = new Dictionary<int, Exception>();
            do
            {
                if (downloadState.PartQueue.Count != previousDoneCount)
                {
                    previousDoneCount = downloadState.PartQueue.Count;
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

            if (restartedThreads >= maxRestartedThreads)
            {
                throw new AggregateException("The download thread restart limit was reached.", downloadExceptions.Values);
            }

            return downloadExceptions.Values;
        }

        private void LogDownloadThreadExceptions(IReadOnlyCollection<Exception> downloadExceptions)
        {
            if (downloadExceptions.Count == 0)
                return;

            var culprits = new HashSet<string>();
            var sb = new StringBuilder();
            foreach (var downloadException in downloadExceptions)
            {
                var ex = downloadException switch
                {
                    AggregateException { InnerException: { } innerException } => innerException,
                    _ => downloadException
                };

                // Try to only log exceptions from different sources
                var targetSiteName = ex.TargetSite?.Name ?? "PrivateMethod";
                var targetSiteTypeName = ex.TargetSite?.DeclaringType?.Name ?? "PrivateType";

                var culprit = $"{targetSiteTypeName}.{targetSiteName}";
                if (!culprits.Add(culprit))
                    continue;

                sb.Append(", ");
                sb.Append(ex.Message);
                sb.Append(" at ");
                sb.Append(culprit);
            }

            if (sb.Length == 0)
            {
                return;
            }

            _ = downloadExceptions.Count == 1
                ? sb.Replace(",", "1 error was encountered while downloading:", 0, 1)
                : sb.Replace(",", $"{downloadExceptions.Count} errors were encountered while downloading:", 0, 1);
            _progress.LogInfo(sb.ToString());
        }

        private async Task VerifyDownloadedParts(VideoDownloadState downloadState, CancellationToken cancellationToken)
        {
            var partCount = downloadState.PartCount;
            var invalidCount = await Task.Run(() => GetInvalidParts(downloadState), cancellationToken).ConfigureAwait(false);

            if (invalidCount > 0)
            {
                if (partCount > 20 && invalidCount >= partCount * 0.95)
                {
                    // 19/20 parts are invalid, something went horribly wrong.
                    // TODO: Somehow let the user bypass this. Maybe with callbacks?
                    throw new Exception($"Too many parts are missing or corrupt ({invalidCount}/{partCount}), aborting.");
                }

                _progress.LogInfo($"{invalidCount} parts were missing or corrupt and will be redownloaded.");
                await DownloadVideoPartsAsync(downloadState, cancellationToken).ConfigureAwait(false);

                try
                {
                    await Task.Run(() => EmitPartStubs(downloadState), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _progress.LogError($"Failed to emit part stubs: {ex.Message}");
                }
            }
        }

        private void EmitPartStubs(VideoDownloadState downloadState)
        {
            byte[] transportStreamStub =
            {
                0x47, 0x40, 0x00, 0x1A, 0x00, 0x00, 0xB0, 0x0D, 0x00, 0x01, 0xC1, 0x00, 0x00, 0x00, 0x01, 0xF0, 0x00, 0x2A, 0xB1, 0x04, 0xB2, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0x47, 0x41, 0x00, 0x30, 0x72, 0x40, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x01, 0xC0, 0x00, 0x3F, 0x84, 0x80, 0x05, 0x21, 0x06, 0xC3, 0xCF, 0x45, 0xFF, 0xF1, 0x4C, 0x40, 0x01, 0x7F, 0xFC, 0x00, 0xD0, 0x00, 0x07,
                0xFF, 0xF1, 0x4C, 0x40, 0x01, 0x7F, 0xFC, 0x00, 0xD0, 0x00, 0x07, 0xFF, 0xF1, 0x4C, 0x40, 0x01, 0x7F, 0xFC, 0x00, 0xD0, 0x00, 0x07, 0xFF, 0xF1, 0x4C, 0x40, 0x01, 0x7F, 0xFC, 0x00, 0xD0, 0x00, 0x07, 0xFF, 0xF1, 0x4C,
                0x40, 0x01, 0x7F, 0xFC, 0x00, 0xD0, 0x00, 0x07
            };

            _progress.ReportProgress(0);
            var partCount = downloadState.PartCount;
            var doneCount = 0;

            using var headerFs = !string.IsNullOrWhiteSpace(downloadState.HeaderFile)
                ? File.OpenRead(downloadState.HeaderFile)
                : null;

            // Emit stubs for missing parts
            foreach (var part in downloadState.PartStates.Keys)
            {
                var partName = DownloadTools.RemoveQueryString(part);
                var path = Path.Combine(_vodCacheDir, partName);

                var fi = new FileInfo(path);
                if (fi.Exists && fi.Length > 0)
                    continue;

                try
                {
                    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);

                    if (headerFs is null)
                    {
                        // TS stream
                        fs.Write(transportStreamStub);
                    }
                    else
                    {
                        // AV1 stream
                        headerFs.Seek(0, SeekOrigin.Begin);
                        headerFs.CopyTo(fs);
                    }
                }
                catch (Exception ex)
                {
                    _progress.LogVerbose($"Failed to write stub for part {partName}: {ex.Message}");
                }

                doneCount++;
                var percent = (int)(doneCount / (double)partCount * 100);
                _progress.ReportProgress(percent);
            }
        }

        private int GetInvalidParts(VideoDownloadState downloadState)
        {
            var partCount = downloadState.PartCount;
            var doneCount = 0;

            var invalidCount = 0;
            foreach (var (partName, partState) in downloadState.PartStates)
            {
                var filePath = Path.Combine(_vodCacheDir, DownloadTools.RemoveQueryString(partName));
                var fi = new FileInfo(filePath);

                var expectedFileSize = partState.ExpectedFileSize + downloadState.HeaderFileSize;
                if (!fi.Exists || fi.Length == 0 || fi.Length != expectedFileSize)
                {
                    downloadState.PartQueue.Enqueue(partName);
                    invalidCount++;
                }

                doneCount++;
                var percent = (int)(doneCount / (double)partCount * 100);
                _progress.ReportProgress(percent);
            }

            return invalidCount;
        }

        private FfmpegConcatList.StreamIds GetStreamIds(M3U8 playlist)
        {
            var path = DownloadTools.RemoveQueryString(playlist.Streams.FirstOrDefault()?.Path ?? "");
            var extension = Path.GetExtension(path);
            switch (extension)
            {
                case ".mp4":
                    return FfmpegConcatList.StreamIds.Mp4;
                case ".ts":
                    return FfmpegConcatList.StreamIds.TransportStream;
                default:
                    _progress.LogWarning("No file extension was found! Assuming TS.");
                    return FfmpegConcatList.StreamIds.TransportStream;
            }
        }

        private async Task<int> RunFfmpegVideoCopy(FileInfo outputFile, string concatListPath, string metadataPath, decimal startOffset, decimal endDuration, TimeSpan videoLength, bool disableAudioCopy, CancellationToken cancellationToken)
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
                    WorkingDirectory = _vodCacheDir
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
                "-max_streams", $"{int.MaxValue}",
                "-i", concatListPath,
                "-i", metadataPath,
                "-map_metadata", "1",
                (disableAudioCopy ? "-c:v" : "-c"), "copy",
                outputFile.FullName
            };

            if (disableAudioCopy)
            {
                // Some VODs have bad audio data which FFmpeg doesn't like in copy mode. See lay295#1121 for more info
                _progress.LogVerbose("Running with audio copy disabled.");
            }

            if (endDuration > 0)
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

            _progress.LogVerbose($"Running \"{downloadOptions.FfmpegPath}\" in \"{process.StartInfo.WorkingDirectory}\" with args: {CombineArguments(process.StartInfo.ArgumentList)}");

            process.Start();
            process.BeginErrorReadLine();

            await using var logWriter = File.AppendText(Path.Combine(_vodCacheDir, "ffmpegLog.txt"));
            logWriter.AutoFlush = true;
            do // We cannot handle logging inside the ErrorDataReceived lambda because more than 1 can come in at once and cause a race condition. lay295#598
            {
                await Task.Delay(200, cancellationToken);
                while (!logQueue.IsEmpty && logQueue.TryDequeue(out var logMessage))
                {
                    await logWriter.WriteLineAsync(logMessage);
                }
            } while (!process.HasExited || !logQueue.IsEmpty);

            return process.ExitCode;

            static string CombineArguments(IEnumerable<string> args)
            {
                return string.Join(' ', args.Select(x =>
                {
                    if (!x.StartsWith('"') && !x.StartsWith('\'') && x.Contains(' '))
                        return $"\"{x}\"";

                    return x;
                }));
            }
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
            string playlistString;
            try
            {
                playlistString = await _httpClient.GetStringAsync(playlistUrl, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
            {
                // Hacky workaround for old highlights that were muted
                var newUrl = Regex.Replace(playlistUrl, @"-muted-\w+(?=\.m3u8$)", "");
                if (playlistUrl == newUrl)
                    throw;

                _progress.LogError($"Received {(int)ex.StatusCode}: {ex.StatusCode} when fetching playlist. Attempting workaround...");
                playlistString = await _httpClient.GetStringAsync(newUrl, cancellationToken);
            }

            var playlist = M3U8.Parse(playlistString);

            var airDate = DateTimeOffset.UtcNow.AddHours(-25);
            var airDateKvp = playlist.FileMetadata.UnparsedValues.FirstOrDefault(x => x.Key == "#ID3-EQUIV-TDTG:");
            if (DateTimeOffset.TryParse(airDateKvp.Value, out var vodAirDate))
            {
                airDate = vodAirDate;
            }

            return (playlist, airDate);
        }

        private Range GetStreamListTrim(IList<M3U8.Stream> streamList, out TimeSpan videoLength, out decimal startOffset, out decimal endDuration)
        {
            startOffset = 0;
            endDuration = 0;

            var startIndex = 0;
            var startTime = 0m;
            if (downloadOptions.TrimBeginning)
            {
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
            var endTime = streamList.Sum(x => x.PartInfo.Duration);
            var endOffset = 0m;
            if (downloadOptions.TrimEnding)
            {
                var trimTotalSeconds = (decimal)downloadOptions.TrimEndingTime.TotalSeconds;
                for (var i = streamList.Count - 1; i >= 0; i--)
                {
                    var videoPart = streamList[i];
                    if (endTime - videoPart.PartInfo.Duration < trimTotalSeconds)
                    {
                        endOffset = endTime - trimTotalSeconds;
                        if (endOffset > 0) endDuration = videoPart.PartInfo.Duration - endOffset;

                        break;
                    }

                    endIndex--;
                    endTime -= videoPart.PartInfo.Duration;
                }
            }

            if (downloadOptions.TrimMode == VideoTrimMode.Safe)
            {
                startOffset = endOffset = endDuration = 0;
            }

            videoLength = TimeSpan.FromSeconds((double)((endTime - endOffset) - (startTime + startOffset)));

            return new Range(startIndex, endIndex);
        }

        private async Task<(string[] allQualityPaths, M3U8.Stream qualityPlaylist)> GetQualityPlaylist()
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
            var qualities = VideoQualities.FromM3U8(m3u8);
            var userQuality = qualities.GetQuality(downloadOptions.Quality) ?? qualities.BestQuality();

            var allQualityPaths = qualities.Qualities
                .Select(x =>
                {
                    var lastSlash = x.Path.LastIndexOf('/');
                    var secondLastSlash = x.Path.AsSpan(0, lastSlash).LastIndexOf('/');
                    return x.Path[(secondLastSlash + 1)..lastSlash];
                })
                .ToArray();

            return (allQualityPaths, userQuality?.Item ?? throw new NullReferenceException($"Unknown quality: {downloadOptions.Quality}"));
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