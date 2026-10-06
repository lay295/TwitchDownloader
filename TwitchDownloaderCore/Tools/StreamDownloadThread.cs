using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCore.Tools
{
    internal sealed record StreamDownloadThread
    {
        private readonly StreamDownloadState _downloadState;
        private readonly HttpClient _client;
        private readonly AutoResetEvent _autoResetEvent;
        private readonly string _cacheFolder;
        private readonly ITaskProgress _progress;
        private readonly CancellationToken _cancellationToken;
        public Task ThreadTask { get; private set; }

        public StreamDownloadThread(StreamDownloadState downloadState, HttpClient httpClient, AutoResetEvent autoResetEvent, string cacheFolder, ITaskProgress progress, CancellationToken cancellationToken)
        {
            _downloadState = downloadState;
            _client = httpClient;
            _autoResetEvent = autoResetEvent;
            _cacheFolder = cacheFolder;
            _progress = progress;
            _cancellationToken = cancellationToken;
            StartDownload();
        }

        public void StartDownload()
        {
            if (ThreadTask is { Status: TaskStatus.Created or TaskStatus.WaitingForActivation or TaskStatus.WaitingToRun or TaskStatus.Running })
            {
                throw new InvalidOperationException($"Tried to start a thread that was already running or waiting to run ({ThreadTask.Status}).");
            }

            ThreadTask = Task.Factory.StartNew(
                Execute,
                _cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Current);
        }

        private void Execute()
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);

            while (true)
            {
                while (_downloadState.PartQueue.TryDequeue(out var videoPart))
                {
                    _cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var result = DownloadStreamPartAsync(videoPart, cts).GetAwaiter().GetResult();
                        if (!result)
                        {
                            Thread.Sleep(Random.Shared.Next(100, 1_000));
                            _downloadState.PartQueue.Enqueue(videoPart);
                        }
                        else
                        {
                            lock (_downloadState.TimeWriteLock)
                            {
                                _downloadState.TotalDownloadedTime += videoPart.Duration;
                            }
                            _progress.ReportProgress((int)_downloadState.TotalDownloadedTime.TotalHours, _downloadState.TotalDownloadedTime, _downloadState.TotalMissingTime);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_cancellationToken.IsCancellationRequested)
                            return;

                        // Deliberately do not re-enqueue the part on exceptions
                        _progress.LogWarning($"Part {videoPart.FileName} could not be downloaded and will be missing from the finalized video.");
                        _progress.LogVerbose($"Error while downloading {videoPart.FileName}: {ex.Message}");

                        lock (_downloadState.TimeWriteLock)
                        {
                            _downloadState.TotalMissingTime += videoPart.Duration;
                        }
                        _progress.ReportProgress((int)_downloadState.TotalDownloadedTime.TotalHours, _downloadState.TotalDownloadedTime, _downloadState.TotalMissingTime);
                    }

                    Thread.Sleep(Random.Shared.Next(100, 200));
                }

                if (!_downloadState.DownloadInProgress)
                    break;

                _autoResetEvent.Reset();
                _autoResetEvent.WaitOne();
            }
        }

        /// <remarks>The <paramref name="cancellationTokenSource"/> may be canceled by this method.</remarks>
        private async Task<bool> DownloadStreamPartAsync(StreamDownloadState.PartState videoPart, CancellationTokenSource cancellationTokenSource)
        {
            var partUri = new Uri(videoPart.Path);
            var partFile = Path.Combine(_cacheFolder, videoPart.FileName);
            var partFi = new FileInfo(partFile);

            if (partFi.Exists)
            {
                _progress.LogWarning($"Tried to redownload already downloaded part: {videoPart.FileName}.");
                return true;
            }

            try
            {
                // Check download attempts
                const int MAX_DOWNLOAD_ATTEMPTS = 5;
                if (videoPart.DownloadAttempts++ >= MAX_DOWNLOAD_ATTEMPTS)
                {
                    throw new Exception($"{videoPart.FileName} failed to download after {videoPart.DownloadAttempts - 1} attempts.");
                }

                // Download file
                // Stream parts don't have a Content-Length header, so this always returns -1
                await DownloadTools.DownloadFileAsync(_client, partUri, partFile, _downloadState.HeaderFile, -1, _progress, cancellationTokenSource);

                // Check file size
                partFi.Refresh();
                CheckTsLength(partFile, partFi.Length);
            }
            catch (HttpRequestException ex)
            {
                _progress.LogVerbose(ex.StatusCode.HasValue
                    ? $"Received {(int)ex.StatusCode}: {ex.StatusCode} for {videoPart.FileName}."
                    : $"{videoPart.FileName}: {ex.Message}");

                await Delay(1_000, cancellationTokenSource.Token);
                return false;
            }
            catch (TaskCanceledException ex) when (ex.Message.Contains("HttpClient.Timeout"))
            {
                _progress.LogVerbose($"{videoPart.FileName} timed out.");

                await Delay(5_000, cancellationTokenSource.Token);
                return false;
            }

            return true;
        }

        private void CheckTsLength(string partFile, long length)
        {
            if (!partFile.EndsWith(".ts"))
            {
                return;
            }

            const int TS_PACKET_LENGTH = 188; // MPEG TS packets are made of a header and a body: [ 4B ][   184B   ] - https://tsduck.io/download/docs/mpegts-introduction.pdf
            if (length % TS_PACKET_LENGTH != 0)
            {
                _progress.LogWarning($"{Path.GetFileName(partFile)} contains malformed packets and may cause encoding issues.");
            }
        }

        private static Task Delay(int millis, CancellationToken cancellationToken)
        {
            var jitteredMillis = millis + Random.Shared.Next(-200, 200);
            return Task.Delay(Math.Clamp(jitteredMillis, millis / 2, millis * 2), cancellationToken);
        }
    }
}