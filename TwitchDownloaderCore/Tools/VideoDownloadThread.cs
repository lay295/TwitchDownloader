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
            this._videoPartsQueue = videoPartsQueue;
            this._client = httpClient;
            this._baseUrl = baseUrl;
            this._cacheFolder = cacheFolder;
            this._vodAirDate = vodAirDate;
            this._throttleKib = throttleKib;
            this._logger = logger;
            this._cancellationToken = cancellationToken;
            this.StartDownload();
        }

        public void StartDownload()
        {
            if (this.ThreadTask is { Status: TaskStatus.Created or TaskStatus.WaitingForActivation or TaskStatus.WaitingToRun or TaskStatus.Running })
                throw new InvalidOperationException($"Tried to start a thread that was already running or waiting to run ({ThreadTask.Status}).");

            this.ThreadTask = Task.Factory.StartNew(
                this.ExecuteDownloadThread,
                this._cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Current);
        }

        private void ExecuteDownloadThread()
        {
            using var cts = new CancellationTokenSource();
            this._cancellationToken.Register(PropagateCancel, cts);

            while (!this._videoPartsQueue.IsEmpty)
            {
                this._cancellationToken.ThrowIfCancellationRequested();

                string videoPart = null;
                try
                {
                    if (this._videoPartsQueue.TryDequeue(out videoPart))
                        this.DownloadVideoPartAsync(videoPart, cts).GetAwaiter().GetResult();
                }
                catch
                {
                    if (videoPart != null && !this._cancellationToken.IsCancellationRequested)
                        // Requeue the video part now instead of deferring to the verifier since we already know it's bad
                        this._videoPartsQueue.Enqueue(videoPart);

                    throw;
                }

                Thread.Sleep(Random.Shared.Next(50, 150));
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
            var tryUnmute = this.VodAge < TimeSpan.FromHours(24);
            var errorCount = 0;
            var timeoutCount = 0;
            var lengthFailureCount = 0;
            while (true)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();

                try
                {
                    var partFile = Path.Combine(_cacheFolder, DownloadTools.RemoveQueryString(videoPartName));
                    long expectedLength;
                    if (tryUnmute && videoPartName.Contains("-muted"))
                    {
                        var unmutedPartName = videoPartName.Replace("-muted", "");
                        expectedLength = await DownloadTools.DownloadFileAsync(_client, new Uri(_baseUrl, unmutedPartName), partFile, _throttleKib, _logger, cancellationTokenSource);
                    }
                    else
                        expectedLength = await DownloadTools.DownloadFileAsync(_client, new Uri(_baseUrl, videoPartName), partFile, _throttleKib, _logger, cancellationTokenSource);

                    if (expectedLength is -1)
                        return;

                    // I would love to compare hashes here but unfortunately Twitch doesn't give us a ContentMD5 header
                    var actualLength = new FileInfo(partFile).Length;
                    if (actualLength != expectedLength)
                    {
                        const int MAX_RETRIES = 1;

                        this._logger.LogVerbose($"{partFile} failed to verify: expected {expectedLength:N0}B, got {actualLength:N0}B.");
                        if (++lengthFailureCount > MAX_RETRIES)
                        {
                            throw new Exception($"Failed to download {partFile}: expected {expectedLength:N0}B, got {actualLength:N0}B.");
                        }

                        await Delay(1_000, cancellationTokenSource.Token);
                        continue;
                    }

                    const int TS_PACKET_LENGTH = 188; // MPEG TS packets are made of a header and a body: [ 4B ][   184B   ] - https://tsduck.io/download/docs/mpegts-introduction.pdf
                    if (expectedLength % TS_PACKET_LENGTH != 0)
                        this._logger.LogVerbose($"{partFile} contains malformed packets and may cause encoding issues.");

                    return;
                }
                catch (HttpRequestException ex) when (tryUnmute && ex.StatusCode is HttpStatusCode.Forbidden)
                {
                    this._logger.LogVerbose($"Received {ex.StatusCode}: {ex.StatusCode} when trying to unmute {videoPartName}. Disabling {nameof(tryUnmute)}.");
                    tryUnmute = false;

                    await Delay(100, cancellationTokenSource.Token);
                }
                catch (HttpRequestException ex)
                {
                    const int MAX_RETRIES = 10;

                    this._logger.LogVerbose($"Received {(int)(ex.StatusCode ?? 0)}: {ex.StatusCode} for {videoPartName}. {MAX_RETRIES - (errorCount + 1)} retries left.");
                    if (++errorCount > MAX_RETRIES)
                    {
                        throw new HttpRequestException($"Video part {videoPartName} failed after {MAX_RETRIES} retries");
                    }

                    await Delay(1_000 * errorCount, cancellationTokenSource.Token);
                }
                catch (TaskCanceledException ex) when (ex.Message.Contains("HttpClient.Timeout"))
                {
                    const int MAX_RETRIES = 3;

                    this._logger.LogVerbose($"{videoPartName} timed out. {MAX_RETRIES - (timeoutCount + 1)} retries left.");
                    if (++timeoutCount > MAX_RETRIES)
                        throw new HttpRequestException($"Video part {videoPartName} timed out {MAX_RETRIES} times");

                    await Delay(5_000 * timeoutCount, cancellationTokenSource.Token);
                }
            }
        }

        private static Task Delay(int millis, CancellationToken cancellationToken)
        {
            var jitteredMillis = millis + Random.Shared.Next(-200, 200);
            return Task.Delay(Math.Max(millis / 2, jitteredMillis), cancellationToken);
        }
    }
}