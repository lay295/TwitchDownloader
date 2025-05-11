using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
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
        private readonly string _headerFile;
        private readonly DateTimeOffset _vodAirDate;
        private TimeSpan VodAge => DateTimeOffset.UtcNow - _vodAirDate;
        private readonly int _throttleKib;
        private readonly ITaskLogger _logger;
        private readonly CancellationToken _cancellationToken;
        public Task ThreadTask { get; private set; }

        public VideoDownloadThread(ConcurrentQueue<string> videoPartsQueue, HttpClient httpClient, Uri baseUrl, string cacheFolder, [AllowNull] string headerFile, DateTimeOffset vodAirDate, int throttleKib, ITaskLogger logger,
            CancellationToken cancellationToken)
        {
            _headerFile = headerFile;
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
                Execute,
                _cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Current);
        }

        private void Execute()
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);

            while (!_videoPartsQueue.IsEmpty)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (_videoPartsQueue.TryDequeue(out var videoPart))
                {
                    try
                    {
                        DownloadVideoPartAsync(videoPart, cts).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            // HACK: Delete the file so the verifier notices the missing part
                            // TODO: Replace with a video part table
                            File.Delete(DownloadTools.RemoveQueryString(videoPart));
                        }
                        catch { }

                        _logger.LogVerbose($"Error while downloading {videoPart}: {ex.Message}");
                        throw;
                    }
                }

                Thread.Sleep(Random.Shared.Next(50, 150));
            }
        }

        /// <remarks>The <paramref name="cancellationTokenSource"/> may be canceled by this method.</remarks>
        private async Task DownloadVideoPartAsync(string videoPartName, CancellationTokenSource cancellationTokenSource)
        {
            var tryUnmute = VodAge < TimeSpan.FromHours(24);
            var errorCount = 0;
            var timeoutCount = 0;
            var lengthFailureCount = 0;
            long expectedLength = 0;
            while (true)
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();

                try
                {
                    var partFile = Path.Combine(_cacheFolder, DownloadTools.RemoveQueryString(videoPartName));
                    if (tryUnmute && videoPartName.Contains("-muted"))
                    {
                        var unmutedPartName = videoPartName.Replace("-muted", "");
                        try
                        {
                            expectedLength = await DownloadTools.DownloadFileAsync(_client, new Uri(_baseUrl, unmutedPartName), partFile, _headerFile, _throttleKib, _logger, cancellationTokenSource);
                        }
                        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden)
                        {
                            // Multi-quality fallback logic
                            string[] qualities = { "chunked", "720p60", "480p30", "360p30", "160p30" };
                            var baseUrlStr = _baseUrl.ToString().TrimEnd('/');
                            var urlParts = baseUrlStr.Split('/');
                            var currentQuality = urlParts[^1];
                            int currentQualityIndex = Array.IndexOf(qualities, currentQuality);
                            bool found = false;
                            for (int i = 0; i < qualities.Length; i++)
                            {
                                if (i == currentQualityIndex) continue;
                                var altBaseUrl = new Uri(_baseUrl, "../" + qualities[i] + "/");
                                var altUrl = new Uri(altBaseUrl, unmutedPartName);
                                try
                                {
                                    expectedLength = await DownloadTools.DownloadFileAsync(_client, altUrl, partFile, _headerFile, _throttleKib, _logger, cancellationTokenSource);
                                    _logger.LogVerbose($"Unmuted segment for {videoPartName} found in quality {qualities[i]}");
                                    found = true;
                                    break;
                                }
                                catch (HttpRequestException)
                                {
                                    continue;
                                }
                                catch (Exception)
                                {
                                    continue;
                                }
                            }
                            if (!found)
                            {
                                expectedLength = await DownloadTools.DownloadFileAsync(_client, new Uri(_baseUrl, videoPartName), partFile, _headerFile, _throttleKib, _logger, cancellationTokenSource);
                            }
                        }
                    }
                    else
                    {
                        expectedLength = await DownloadTools.DownloadFileAsync(_client, new Uri(_baseUrl, videoPartName), partFile, _headerFile, _throttleKib, _logger, cancellationTokenSource);
                    }

                    // TODO: Support checking file length with header file
                    if (string.IsNullOrWhiteSpace(_headerFile) && expectedLength > 0)
                    {
                        // I would love to compare hashes here but unfortunately Twitch doesn't give us a ContentMD5 header
                        var actualLength = new FileInfo(partFile).Length;
                        if (!VerifyFileLength(expectedLength, actualLength, partFile, ref lengthFailureCount))
                        {
                            await Delay(1_000, cancellationTokenSource.Token);
                            continue;
                        }

                        CheckTsLength(partFile, actualLength);
                    }

                    return;
                }
                catch (HttpRequestException ex) when (tryUnmute && ex.StatusCode is HttpStatusCode.Forbidden)
                {
                    _logger.LogVerbose($"Received {(int)(ex.StatusCode ?? 0)}: {ex.StatusCode} when trying to unmute {videoPartName}. Disabling {nameof(tryUnmute)}.");
                    tryUnmute = false;

                    await Delay(100, cancellationTokenSource.Token);
                }
                catch (HttpRequestException ex)
                {
                    const int MAX_ERROR_COUNT = 3;
                    errorCount++;

                    _logger.LogVerbose($"Received {(int)(ex.StatusCode ?? 0)}: {ex.StatusCode} for {videoPartName}. {MAX_ERROR_COUNT - errorCount} retries left.");

                    if (errorCount >= MAX_ERROR_COUNT)
                    {
                        throw new HttpRequestException($"Video part {videoPartName} failed after {MAX_ERROR_COUNT} retries");
                    }

                    await Delay(1_000 * errorCount, cancellationTokenSource.Token);
                }
                catch (TaskCanceledException ex) when (ex.Message.Contains("HttpClient.Timeout"))
                {
                    const int MAX_TIMEOUT_COUNT = 2;
                    timeoutCount++;

                    _logger.LogVerbose($"{videoPartName} timed out. {MAX_TIMEOUT_COUNT - timeoutCount} retries left.");

                    if (timeoutCount >= MAX_TIMEOUT_COUNT)
                    {
                        throw new HttpRequestException($"Video part {videoPartName} timed out {MAX_TIMEOUT_COUNT} times");
                    }

                    await Delay(5_000 * timeoutCount, cancellationTokenSource.Token);
                }
            }

            bool VerifyFileLength(long expectedLength, long actualLength, string partFile, ref int failureCount)
            {
                if (actualLength != expectedLength)
                {
                    const int MAX_RETRIES = 1;

                    _logger.LogVerbose($"{partFile} failed to verify: expected {expectedLength:N0}B, got {actualLength:N0}B.");
                    if (++failureCount > MAX_RETRIES)
                    {
                        throw new Exception($"Failed to download {partFile}: expected {expectedLength:N0}B, got {actualLength:N0}B");
                    }

                    return false;
                }

                return true;
            }
        }

        private void CheckTsLength(string partFile, long length)
        {
            if (partFile.EndsWith(".ts"))
            {
                const int TS_PACKET_LENGTH = 188; // MPEG TS packets are made of a header and a body: [ 4B ][   184B   ] - https://tsduck.io/download/docs/mpegts-introduction.pdf
                if (length % TS_PACKET_LENGTH != 0)
                {
                    _logger.LogVerbose($"{partFile} contains malformed packets and may cause encoding issues.");
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