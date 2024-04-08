using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentQueue<string> _videoPartsQueue;
        private readonly HttpClient _client;
        private readonly Uri _baseUrl;
        private readonly string _cacheFolder;
        private readonly DateTimeOffset _vodAirDate;
        private TimeSpan VodAge => DateTimeOffset.UtcNow - _vodAirDate;
        private readonly int _throttleKib;
        private readonly ITaskLogger _logger;
        private readonly CancellationToken _cancellationToken;
        public Task ThreadTask { get; private set; }

        public VideoDownloadThread(ConcurrentQueue<string> videoPartsQueue, HttpClient httpClient, Uri baseUrl, string cacheFolder, DateTimeOffset vodAirDate, int throttleKib, ITaskLogger logger, CancellationToken cancellationToken)
        {
            _videoPartsQueue = videoPartsQueue;
            _client = httpClient;
            _baseUrl = baseUrl;
            _cacheFolder = cacheFolder;
            _vodAirDate = vodAirDate;
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
                ExecuteDownloadThread,
                _cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Current);
        }

        private void ExecuteDownloadThread()
        {
            using var cts = new CancellationTokenSource();
            _cancellationToken.Register(PropagateCancel, cts);

            while (!_videoPartsQueue.IsEmpty)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                string videoPart = null;
                try
                {
                    if (_videoPartsQueue.TryDequeue(out videoPart))
                    {
                        DownloadVideoPartAsync(videoPart, cts).GetAwaiter().GetResult();
                    }
                }
                catch
                {
                    if (videoPart != null && !_cancellationToken.IsCancellationRequested)
                    {
                        // Requeue the video part now instead of deferring to the verifier since we already know it's bad
                        _videoPartsQueue.Enqueue(videoPart);
                    }

                    throw;
                }

                const int A_PRIME_NUMBER = 71;
                Thread.Sleep(A_PRIME_NUMBER);
            }
        }

        private static void PropagateCancel(object tokenSourceToCancel)
        {
            try
            {
                (tokenSourceToCancel as CancellationTokenSource)?.Cancel();
            }
            catch (ObjectDisposedException) { }
        }

        /// <remarks>The <paramref name="cancellationTokenSource"/> may be canceled by this method.</remarks>
        private async Task DownloadVideoPartAsync(string videoPartName, CancellationTokenSource cancellationTokenSource)
        {
            var tryUnmute = VodAge < TimeSpan.FromHours(24);
            var errorCount = 0;
            var timeoutCount = 0;
            while (true)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();

                try
                {
                    var partFile = Path.Combine(_cacheFolder, DownloadTools.RemoveQueryString(videoPartName));
                    if (tryUnmute && videoPartName.Contains("-muted"))
                    {
                        var unmutedPartName = videoPartName.Replace("-muted", "");
                        await DownloadTools.DownloadFileAsync(_client, new Uri(_baseUrl, unmutedPartName), partFile, _throttleKib, _logger, cancellationTokenSource);
                    }
                    else
                    {
                        await DownloadTools.DownloadFileAsync(_client, new Uri(_baseUrl, videoPartName), partFile, _throttleKib, _logger, cancellationTokenSource);
                    }

                    return;
                }
                catch (HttpRequestException ex) when (tryUnmute && ex.StatusCode is HttpStatusCode.Forbidden)
                {
                    _logger.LogVerbose($"Received {ex.StatusCode}: {ex.StatusCode} when trying to unmute {videoPartName}. Disabling {nameof(tryUnmute)}.");
                    tryUnmute = false;

                    await Task.Delay(100, cancellationTokenSource.Token);
                }
                catch (HttpRequestException ex)
                {
                    const int MAX_RETRIES = 10;

                    _logger.LogVerbose($"Received {(int)(ex.StatusCode ?? 0)}: {ex.StatusCode} for {videoPartName}. {MAX_RETRIES - (errorCount + 1)} retries left.");
                    if (++errorCount > MAX_RETRIES)
                    {
                        throw new HttpRequestException($"Video part {videoPartName} failed after {MAX_RETRIES} retries");
                    }

                    await Task.Delay(1_000 * errorCount, cancellationTokenSource.Token);
                }
                catch (TaskCanceledException ex) when (ex.Message.Contains("HttpClient.Timeout"))
                {
                    const int MAX_RETRIES = 3;

                    _logger.LogVerbose($"{videoPartName} timed out. {MAX_RETRIES - (timeoutCount + 1)} retries left.");
                    if (++timeoutCount > MAX_RETRIES)
                    {
                        throw new HttpRequestException($"Video part {videoPartName} timed out {MAX_RETRIES} times");
                    }

                    await Task.Delay(5_000 * timeoutCount, cancellationTokenSource.Token);
                }
            }
        }
    }
}