using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Models.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed partial class StreamDownloader
    {
        private readonly StreamDownloadOptions _downloadOptions;
        private readonly HttpClient _httpClient;
        private readonly ITaskProgress _progress;
        private readonly string _cacheDir;
        private bool _shouldClearCache = true;

        public StreamDownloader(StreamDownloadOptions downloadOptions, ITaskProgress progress = default)
        {
            _downloadOptions = downloadOptions;
            _httpClient = new() { Timeout = TimeSpan.FromSeconds(25) };
            _progress = progress;
            _cacheDir = Path.Combine(CacheDirectoryService.GetCacheDirectory(downloadOptions.TempFolder), $"{downloadOptions.ChannelLogin}_{DateTimeOffset.UtcNow.Ticks}");
        }

        /// <param name="stoppingToken">A <see cref="CancellationToken"/> used to stop the stream download, but still finalize downloaded parts to the output file.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the download.</param>
        public async Task DownloadAsync(CancellationToken stoppingToken, CancellationToken cancellationToken)
        {
            var outputFileInfo = TwitchHelper.ClaimFile(_downloadOptions.Filename, _downloadOptions.FileCollisionCallback, _progress);
            _downloadOptions.Filename = outputFileInfo.FullName;

            // Open the destination file so that it exists in the filesystem.
            await using var outputFs = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

            // Create and delete cache folder here to avoid surrounding DownloadAsyncImpl with a try/finally
            await TwitchHelper.CleanupAbandonedVideoCaches(_cacheDir, _downloadOptions.CacheCleanerCallback, _progress);
            if (Directory.Exists(_cacheDir))
            {
                _progress.LogWarning("Download cache already exists!");
            }
            else
            {
                TwitchHelper.CreateDirectory(_cacheDir);
            }

            try
            {
                await DownloadAsyncImpl(outputFileInfo, outputFs, stoppingToken, cancellationToken);
            }
            catch
            {
                await Task.Delay(100, CancellationToken.None);

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, outputFs, _progress);

                throw;
            }
            finally
            {
                await Task.Delay(100, CancellationToken.None);

                if (_shouldClearCache)
                {
                    Cleanup(_cacheDir);
                }
            }
        }

        private async Task DownloadAsyncImpl(FileInfo outputFileInfo, FileStream outputFs, CancellationToken stoppingToken, CancellationToken cancellationToken)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, cancellationToken);

            // TODO: option to download earlier parts from the VOD, either now or at end of stream download

            // Hacky workaroud to display more than 23h
            _progress.SetTemplateStatus("Downloading Stream ({0}h{1:m\\ms\\s} downloaded) [1/2]", 0, TimeSpan.Zero, TimeSpan.Zero);
            var progressTemplateIncludesMissingTime = false;

            var firstStreamInfo = (await TwitchHelper.GetStreamInfo(_downloadOptions.ChannelLogin)).data.user?.stream;

            var downloadState = new StreamDownloadState(_progress);
            var autoResetEvents = new AutoResetEvent[_downloadOptions.DownloadThreads];
            var downloadThreads = new StreamDownloadThread[_downloadOptions.DownloadThreads];
            for (var i = 0; i < _downloadOptions.DownloadThreads; i++)
            {
                autoResetEvents[i] = new(false);
                downloadThreads[i] = new StreamDownloadThread(downloadState, _httpClient, autoResetEvents[i], _cacheDir, _progress, cancellationToken);
            }

            List<(string fileName, decimal duration)> parts = [];

            PlaybackAccessToken accessToken = null;
            DateTime accessTokenExpirationTime = DateTime.MinValue;

            var isFirstIteration = true;
            TimeSpan retryTime = TimeSpan.Zero;
            while (true)
            {
                try
                {
                    if (DateTime.Now - accessTokenExpirationTime > TimeSpan.Zero)
                    {
                        (accessToken, accessTokenExpirationTime) = await GetAccessToken(cancellationToken);
                    }

                    var quality = await GetQuality(accessToken, linkedCts.Token);

                    if (isFirstIteration)
                    {
                        if (quality is null)
                            throw new Exception("Channel does not exist or is not live");

                        CheckAvailableStorageSpace(quality.Item.Bandwidth);
                    }

                    if (quality is not null)
                    {
                        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
                        do
                        {
                            var playlist = await GetPlaylistAsync(quality, linkedCts.Token);
                            if (playlist is null)
                                break;

                            if (isFirstIteration)
                            {
                                var totalTime = TimeSpan.FromSeconds((double)playlist.FileMetadata.TwitchTotalSeconds);
                                var elapsedTime = TimeSpan.FromSeconds((double)playlist.FileMetadata.TwitchElapsedSeconds);
                                if (elapsedTime > TimeSpan.Zero)
                                {
                                    _progress.LogInfo($"The stream was live for {(int)totalTime.TotalHours}h{totalTime.Minutes:00}m{totalTime.Seconds:00}s. {(int)elapsedTime.TotalHours}h{elapsedTime.Minutes:00}m{elapsedTime.Seconds:00}s will be missing from the stream download.");
                                }
                                else
                                {
                                    _progress.LogInfo($"The stream was live for {(int)totalTime.TotalHours}h{totalTime.Minutes:00}m{totalTime.Seconds:00}s");
                                }
                                _progress.LogVerbose($"Downloading at {quality.Name}");
                            }

                            if (downloadState.HeaderFile is null && playlist.FileMetadata.Map?.Uri is not null)
                            {
                                downloadState.HeaderFile = await GetHeaderFile(playlist, cancellationToken);
                            }

                            downloadState.AppendSegment(playlist);
                            foreach (var autoResetEvent in autoResetEvents)
                                autoResetEvent.Set();

                            parts.AddRange(playlist.Streams.Select(stream => (DownloadTools.GetStreamPartFileName(stream), stream.PartInfo.Duration)));

                            if (!progressTemplateIncludesMissingTime && downloadState.TotalMissingTime > TimeSpan.Zero)
                            {
                                _progress.SetTemplateStatus(
                                    "Downloading Stream ({0}h{1:m\\ms\\s} downloaded, {2:h\\hm\\ms\\s} missing) [1/2]",
                                    (int)downloadState.TotalDownloadedTime.TotalHours,
                                    downloadState.TotalDownloadedTime,
                                    downloadState.TotalMissingTime);
                                progressTemplateIncludesMissingTime = true;
                            }

                            isFirstIteration = false;
                            retryTime = TimeSpan.Zero;
                        } while (await timer.WaitForNextTickAsync(linkedCts.Token));
                    }

                    // End of live stream, retry repeatedly in case stream crashed
                    retryTime += TimeSpan.FromSeconds(5);
                    if (retryTime > _downloadOptions.StreamEndWaitTime)
                        break;
                    _progress.LogVerbose("Stream playlist not found, retrying in 5s...");
                    await Task.Delay(5000, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        _progress.LogInfo("Stopping stream download");
                        break;
                    }
                    else
                    {
                        throw;
                    }
                }

                isFirstIteration = false;
            }

            if (!linkedCts.IsCancellationRequested)
            {
                _progress.LogInfo("End of live stream");
            }

            cancellationToken.ThrowIfCancellationRequested();
            downloadState.StopDownload();
            foreach (var autoResetEvent in autoResetEvents)
                autoResetEvent.Set();

            // StoppingToken does nothing past this point
            linkedCts.Dispose();

            // Download threads only throw when cancelled, we can just wait they all exit
            await Task.WhenAll(downloadThreads.Select(x => x.ThreadTask));
            cancellationToken.ThrowIfCancellationRequested();

            // Adjust parts duration if the next is missing
            for (int i = 1; i < parts.Count; i++)
            {
                if (!File.Exists(Path.Combine(_cacheDir, parts[i].fileName)))
                {
                    parts[i - 1] = (parts[i - 1].fileName, parts[i - 1].duration + parts[i].duration);
                    parts.RemoveAt(i);
                    i--;
                }
            }
            if (!File.Exists(Path.Combine(_cacheDir, parts[0].fileName)))
            {
                downloadState.TotalMissingTime -= TimeSpan.FromSeconds((double)parts[0].duration);
                parts.RemoveAt(0);
            }

            var concatListPath = Path.Combine(_cacheDir, "concat.txt");
            await using var concatFs = new FileStream(concatListPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            await FfmpegConcatList.SerializeAsync(concatFs, parts.Select(x => (x.fileName, x.duration, GetStreamIds(x.fileName))), cancellationToken);

            if (downloadState.TotalDownloadedTime <= TimeSpan.Zero)
            {
                _progress.LogWarning("No stream parts downloaded");
                return;
            }

            // TODO: option to download missing parts from VOD


            _progress.SetTemplateStatus("Finalizing Video {0}% [2/2]", 0);

            string metadataPath = Path.Combine(_cacheDir, "metadata.txt");
            await FfmpegMetadata.SerializeAsync(metadataPath, firstStreamInfo);
            // TODO: get chapters from VOD?

            outputFs.Close();

            int ffmpegExitCode;
            var ffmpegRetries = 0;
            do
            {
                // For some reason using the full concatListPath makes ffmpeg not use _cacheDir as working directory
                ffmpegExitCode = await RunFfmpegVideoCopy(outputFileInfo, "concat.txt", metadataPath, downloadState.TotalDownloadedTime + downloadState.TotalMissingTime, ffmpegRetries > 0, cancellationToken);
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
                throw new Exception($"Failed to finalize video. The download cache has not been cleared and can be found at {_cacheDir} along with a log file.");
            }

            _progress.ReportProgress(100);
        }

        private void CheckAvailableStorageSpace(int bandwidth)
        {
            var bytesPerSecond = bandwidth / 8d;
            var tempFolderDrive = DriveHelper.GetOutputDrive(_cacheDir);
            var destinationDrive = DriveHelper.GetOutputDrive(_downloadOptions.Filename);
            var tempFolderAvailableTime = TimeSpan.FromSeconds(tempFolderDrive.AvailableFreeSpace / bytesPerSecond);
            var destinationAvailableTime = TimeSpan.FromSeconds(destinationDrive.AvailableFreeSpace / bytesPerSecond);

            // Warn user if less than 12h available
            if (tempFolderDrive.Name == destinationDrive.Name)
            {
                if (tempFolderAvailableTime < TimeSpan.FromHours(12 * 2))
                {
                    _progress.LogWarning($"The drive '{tempFolderDrive.Name}' only has space for about {tempFolderAvailableTime:h\\hmm\\m} of stream.");
                }
            }
            else
            {
                if (tempFolderAvailableTime < TimeSpan.FromHours(12))
                {
                    // More drive space is needed by the raw ts files due to repeat metadata, but the amount of metadata packets can vary between files so we won't bother.
                    _progress.LogWarning($"The drive '{tempFolderDrive.Name}' only has space for about {tempFolderAvailableTime:h\\hmm\\m} of stream.");
                }

                if (destinationAvailableTime < TimeSpan.FromHours(12))
                {
                    _progress.LogWarning($"The drive '{destinationDrive.Name}' only has space for about {destinationAvailableTime:h\\hmm\\m} of stream.");
                }
            }
        }

        [GeneratedRegex(@"(?<=expires:"")([0-9]+)")]
        private static partial Regex TokenExpirationTimeRegex { get; }

        private async Task<(PlaybackAccessToken accessToken, DateTime expirationTime)> GetAccessToken(CancellationToken cancellationToken)
        {
            GqlStreamTokenResponse accessTokenResponse = await TwitchHelper.GetStreamToken(_downloadOptions.ChannelLogin, _downloadOptions.Oauth, cancellationToken);
            var token = accessTokenResponse.data.streamPlaybackAccessToken ?? throw new NullReferenceException("Invalid stream");

            var match = TokenExpirationTimeRegex.Match(token.value);
            if (!match.Success)
            {
                // Assume 20 minutes
                return (token, DateTime.Now.AddMinutes(20));
            }
            var epochTime = long.Parse(match.Groups[1].Value);

            return (token, DateTime.UnixEpoch.AddSeconds(epochTime));
        }

        private async Task<IVideoQuality<StreamQuality>> GetQuality(PlaybackAccessToken accessToken, CancellationToken cancellationToken)
        {
            var playlistString = await TwitchHelper.GetStreamPlaylist(
                _downloadOptions.ChannelLogin,
                accessToken.value,
                accessToken.signature,
                cancellationToken);
            if (playlistString.Contains("Can not find channel"))
            {
                return null;
            }

            var m3u8 = M3U8.Parse(playlistString);
            var (availableQualities, unavailableQualities) = VideoQualities.FromStreamM3U8(m3u8);
            var allQualities = new StreamVideoQualities([.. availableQualities.Qualities, .. unavailableQualities.Qualities]);

            _progress.LogVerbose("Available qualities: " + string.Join(',', availableQualities.Select(x => x.Name)));

            var quality = allQualities.GetQuality(_downloadOptions.Quality);

            if (quality is null)
            {
                quality = availableQualities.BestQuality();
                _progress.LogWarning($"Unknown quality: {_downloadOptions.Quality}. Switching to {quality.Name}");
            }
            if (quality.Path is null)
            {
                var fallback = availableQualities.GetQuality(_downloadOptions.Quality);
                _progress.LogWarning($"Quality {quality.Name} is unavailable for reasons: {quality.Item.Video}. Switching to {fallback.Name}");
                return fallback;
            }

            return quality;
        }

        private async Task<M3U8> GetPlaylistAsync(IVideoQuality<StreamQuality> quality, CancellationToken cancellationToken)
        {
            string playlistString;
            try
            {
                playlistString = await _httpClient.GetStringAsync(quality.Path, cancellationToken);
            }
            catch (HttpRequestException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Stream went offline
                    return null;
                }
                else
                {
                    throw;
                }
            }
            var playlist = M3U8.Parse(playlistString);
            return playlist;
        }

        private async Task<string> GetHeaderFile(M3U8 playlist, CancellationToken cancellationToken)
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

            var destinationFile = Path.Combine(_cacheDir, "header" + DownloadTools.GetStreamPartFileExtension(map.Uri));

            var uri = new Uri(map.Uri);
            _progress.LogVerbose($"Downloading header file from '{uri}' to '{destinationFile}'");

            await DownloadTools.DownloadFileAsync(_httpClient, uri, destinationFile, null, -1, _progress, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));

            return destinationFile;
        }

        private FfmpegConcatList.StreamIds GetStreamIds(string path)
        {
            var extension = DownloadTools.GetStreamPartFileExtension(path);
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

        private async Task<int> RunFfmpegVideoCopy(FileInfo outputFile, string concatListPath, string metadataPath, TimeSpan videoLength, bool disableAudioCopy, CancellationToken cancellationToken)
        {
            using var process = new Process
            {
                StartInfo =
                {
                    FileName = _downloadOptions.FfmpegPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = _cacheDir
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
                disableAudioCopy ? "-c:v" : "-c", "copy",
                outputFile.FullName
            };

            if (disableAudioCopy)
            {
                // Some VODs have bad audio data which FFmpeg doesn't like in copy mode. See lay295#1121 for more info
                // No idea if this is necessary with live streams
                _progress.LogVerbose("Running with audio copy disabled.");
            }

            foreach (var arg in args)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            var logQueue = new ConcurrentQueue<string>();

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is null)
                    return;

                logQueue.Enqueue(e.Data); // We cannot use -report ffmpeg arg because it redirects stderr

                HandleFfmpegOutput(e.Data, videoLength);
            };
            cancellationToken.Register(process.Kill);
            cancellationToken.ThrowIfCancellationRequested();

            _progress.LogVerbose($"Running \"{_downloadOptions.FfmpegPath}\" in \"{process.StartInfo.WorkingDirectory}\" with args: {CombineArguments(process.StartInfo.ArgumentList)}");

            process.Start();
            process.BeginErrorReadLine();

            await using var logWriter = File.AppendText(Path.Combine(_cacheDir, "ffmpegLog.txt"));
            logWriter.AutoFlush = true;
            do // We cannot handle logging inside the ErrorDataReceived lambda because more than 1 can come in at once and cause a race condition. lay295#598
            {
                await Task.Delay(200, cancellationToken);
                while (!logQueue.IsEmpty && logQueue.TryDequeue(out var logMessage))
                {
                    await logWriter.WriteLineAsync(logMessage);
                    cancellationToken.ThrowIfCancellationRequested();
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

        [GeneratedRegex(@"(?<=time=)(\d\d):(\d\d):(\d\d)\.(\d\d)")]
        private static partial Regex EncodingTimeRegex { get; }

        private void HandleFfmpegOutput(string output, TimeSpan videoLength)
        {
            var encodingTimeMatch = EncodingTimeRegex.Match(output);
            if (!encodingTimeMatch.Success)
                return;

            // TimeSpan.Parse insists that hours cannot be greater than 24, thus we must use the TimeSpan ctor.
            if (!int.TryParse(encodingTimeMatch.Groups[1].ValueSpan, out var hours))
                return;
            if (!int.TryParse(encodingTimeMatch.Groups[2].ValueSpan, out var minutes))
                return;
            if (!int.TryParse(encodingTimeMatch.Groups[3].ValueSpan, out var seconds))
                return;
            if (!int.TryParse(encodingTimeMatch.Groups[4].ValueSpan, out var milliseconds))
                return;
            var encodingTime = new TimeSpan(0, hours, minutes, seconds, milliseconds);

            var percent = (int)Math.Round(encodingTime / videoLength * 100);

            _progress.ReportProgress(Math.Clamp(percent, 0, 100));
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
