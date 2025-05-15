using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCore.Tools
{
    internal sealed record VideoDownloadThread
    {
        private readonly VideoDownloadState _downloadState;
        private readonly HttpClient _client;
        private readonly string _cacheFolder;
        private readonly int _throttleKib;
        private readonly ITaskLogger _logger;
        private readonly CancellationToken _cancellationToken;
        public Task ThreadTask { get; private set; }

        public VideoDownloadThread(VideoDownloadState downloadState, HttpClient httpClient, string cacheFolder, int throttleKib, ITaskLogger logger,
            CancellationToken cancellationToken)
        {
            _downloadState = downloadState;
            _client = httpClient;
            _cacheFolder = cacheFolder;
            _throttleKib = throttleKib;
            _logger = logger;
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

            while (!_downloadState.PartQueue.IsEmpty)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (_downloadState.PartQueue.TryDequeue(out var videoPart))
                {
                    try
                    {
                        var result = DownloadVideoPartAsync(videoPart, cts).GetAwaiter().GetResult();
                        if (!result)
                        {
                            Thread.Sleep(Random.Shared.Next(100, 1_000));
                            _downloadState.PartQueue.Enqueue(videoPart);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogVerbose($"Error while downloading {videoPart}: {ex.Message}");
                        throw;
                    }
                }

                Thread.Sleep(Random.Shared.Next(25, 100));
            }
        }

        /// <remarks>The <paramref name="cancellationTokenSource"/> may be canceled by this method.</remarks>
        private async Task<bool> DownloadVideoPartAsync(string videoPartName, CancellationTokenSource cancellationTokenSource)
        {
            var partState = _downloadState.PartStates[videoPartName];

            try
            {
                // Check download attempts
                const int MAX_DOWNLOAD_ATTEMPTS = 8;
                if (partState.DownloadAttempts++ > MAX_DOWNLOAD_ATTEMPTS)
                {
                    throw new Exception($"{videoPartName} failed to download after {MAX_DOWNLOAD_ATTEMPTS} attempts.");
                }

                // Download file
                long downloadSize;
                var partFile = Path.Combine(_cacheFolder, DownloadTools.RemoveQueryString(videoPartName));
                if (partState.TryUnmute && videoPartName.Contains("-muted"))
                {
                    var unmutedPartName = videoPartName.Replace("-muted", "");
                    downloadSize = await DownloadTools.DownloadFileAsync(_client, new Uri(_downloadState.BaseUrl, unmutedPartName), partFile, _downloadState.HeaderFile, _throttleKib, _logger, cancellationTokenSource);
                }
                else
                {
                    downloadSize = await DownloadTools.DownloadFileAsync(_client, new Uri(_downloadState.BaseUrl, videoPartName), partFile, _downloadState.HeaderFile, _throttleKib, _logger, cancellationTokenSource);
                }

                if (downloadSize > 0)
                {
                    // We don't have reports of this happening, but it's better to be safe than sorry
                    if (partState.ExpectedFileSize > 0 && partState.ExpectedFileSize != downloadSize)
                    {
                        var previousDownloadSize = downloadSize;
                        downloadSize = Math.Max(partState.ExpectedFileSize, downloadSize);

                        _logger.LogWarning($"Got two different file sizes for {videoPartName}: {partState.ExpectedFileSize:N0}B and {previousDownloadSize:N0}B! Using {downloadSize:N0}B.");
                    }

                    partState.ExpectedFileSize = downloadSize;
                }

                // Check file size
                if (partState.ExpectedFileSize > 0 && (!string.IsNullOrWhiteSpace(_downloadState.HeaderFile) || _downloadState.HeaderFileSize > 0))
                {
                    var fi = new FileInfo(partFile);
                    var expectedFileSize = partState.ExpectedFileSize + _downloadState.HeaderFileSize;

                    // I would love to compare hashes here, but unfortunately Twitch doesn't give us a ContentMD5 header
                    if (fi.Length != expectedFileSize)
                    {
                        _logger.LogVerbose($"{partFile} failed to verify: expected {expectedFileSize:N0}B, got {fi.Length:N0}B.");

                        await Delay(50, cancellationTokenSource.Token);
                        return false;
                    }

                    CheckTsLength(partFile, fi.Length);
                }
            }
            catch (HttpRequestException ex) when (partState.TryUnmute && ex.StatusCode is HttpStatusCode.Forbidden)
            {
                // TODO: Try to unmute part with other available qualities

                _logger.LogVerbose($"Received {(int)ex.StatusCode}: {ex.StatusCode} when trying to unmute {videoPartName}. Disabling {nameof(partState.TryUnmute)}.");

                partState.TryUnmute = false;

                await Delay(50, cancellationTokenSource.Token);
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogVerbose(ex.StatusCode.HasValue
                    ? $"Received {(int)ex.StatusCode}: {ex.StatusCode} for {videoPartName}."
                    : $"{videoPartName}: {ex.Message}");

                await Delay(1_000, cancellationTokenSource.Token);
                return false;
            }
            catch (TaskCanceledException ex) when (ex.Message.Contains("HttpClient.Timeout"))
            {
                _logger.LogVerbose($"{videoPartName} timed out.");

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
                _logger.LogWarning($"{Path.GetFileName(partFile)} contains malformed packets and may cause encoding issues.");
            }
        }

        private static Task Delay(int millis, CancellationToken cancellationToken)
        {
            var jitteredMillis = millis + Random.Shared.Next(-200, 200);
            return Task.Delay(Math.Clamp(jitteredMillis, millis / 2, millis * 2), cancellationToken);
        }
    }
}