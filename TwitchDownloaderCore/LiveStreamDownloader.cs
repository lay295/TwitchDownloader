using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCore
{
    /// <summary>
    /// "Live download": captures a currently-airing broadcast in full.
    /// <list type="number">
    /// <item><b>Back-catalog</b> <c>[start -&gt; split]</c>: the hours already aired are
    /// downloaded immediately with the multi-threaded <see cref="VideoDownloader"/>. This
    /// range is static, so it is fast and finalizes reliably.</item>
    /// <item><b>Live tail</b> <c>[split -&gt; end]</c>: recorded concurrently in real time by
    /// ffmpeg streaming the in-progress VOD from segment index <c>split</c>. ffmpeg only has
    /// to keep up with real time (the backlog is the back-catalog's job), and it finalizes
    /// once cleanly when the broadcast ends.</item>
    /// <item><b>Stitch</b>: the two halves are concatenated with <c>-c copy</c>. The split is
    /// an exact HLS segment boundary (same source segments, "Safe" trim), so the join is
    /// bit-exact - no re-encode, no overlap, no gap, no frame matching.</item>
    /// </list>
    /// </summary>
    public sealed class LiveStreamDownloader
    {
        private static readonly HttpClient HttpClient = new();

        private readonly LiveStreamDownloadOptions _options;
        private readonly ITaskProgress _progress;

        public LiveStreamDownloader(LiveStreamDownloadOptions options, ITaskProgress progress)
        {
            _options = options;
            _progress = progress;
        }

        public async Task DownloadAsync(CancellationToken cancellationToken)
        {
            _progress.SetStatus("Starting live download...");

            var info = await TwitchHelper.GetVideoInfo(_options.Id);
            var video = info?.data?.video;
            if (video is null)
                throw new Exception($"Could not get video info for {_options.Id}. A live download requires the in-progress VOD (the channel must have VODs/archive enabled).");

            var channel = video.owner?.login ?? video.owner?.displayName ?? _options.Channel ?? _options.Id.ToString();
            var isLive = string.Equals(video.status, "RECORDING", StringComparison.OrdinalIgnoreCase);

            var (outputFile, extension) = ResolveOutputPath();

            // If the broadcast already ended, the in-progress VOD is just a normal finished
            // VOD - one multi-threaded download, no split, no stitch.
            if (!isLive)
            {
                _progress.LogWarning($"'{channel}' is not live (status: {video.status ?? "unknown"}). Downloading the complete VOD instead.");
                await new VideoDownloader(BuildOptions(outputFile, null, null), _progress).DownloadAsync(cancellationToken);
                _progress.ReportProgress(100);
                return;
            }

            // Resolve the media playlist for the chosen quality and measure where the live
            // edge currently is (segment count + total duration so far).
            var tokenResponse = await TwitchHelper.GetVideoToken(_options.Id, _options.Oauth);
            var vodToken = tokenResponse?.data?.videoPlaybackAccessToken;
            if (vodToken is null || string.IsNullOrEmpty(vodToken.value))
                throw new Exception($"Could not retrieve a playback token for video {_options.Id}.");

            var masterString = await TwitchHelper.GetVideoPlaylist(_options.Id, vodToken.value, vodToken.signature);
            if (masterString.Contains("vod_manifest_restricted") || masterString.Contains("unauthorized_entitlements"))
                throw new Exception("This VOD requires authorization (sub-only / restricted). Provide a valid OAuth token in Settings.");

            var master = M3U8.Parse(masterString);
            master.SortStreamsByQuality();
            var quality = VideoQualities.FromM3U8(master).GetQuality(_options.Quality);
            var mediaUrl = quality?.Item?.Path;
            if (string.IsNullOrWhiteSpace(mediaUrl))
                throw new Exception("Could not find a playable quality for this stream.");

            int splitSegmentIndex;
            TimeSpan splitTime;
            try
            {
                var mediaString = await HttpClient.GetStringAsync(mediaUrl, cancellationToken);
                var media = M3U8.Parse(mediaString);
                var segments = media.Streams ?? Array.Empty<M3U8.Stream>();
                splitSegmentIndex = segments.Length;
                splitTime = TimeSpan.FromSeconds((double)segments.Sum(s => s.PartInfo?.Duration ?? 0m));
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not read the live media playlist to find the split point: {ex.Message}", ex);
            }

            if (splitSegmentIndex <= 0 || splitTime <= TimeSpan.FromSeconds(1))
            {
                // Nothing meaningful aired yet - just record everything live with ffmpeg.
                _progress.LogInfo("Nothing has aired yet; recording the whole broadcast live.");
                await RecordLiveTail(mediaUrl, 0, outputFile, extension, channel, cancellationToken);
                _progress.ReportProgress(100);
                return;
            }

            var partA = Path.Combine(Path.GetDirectoryName(outputFile) ?? "", Path.GetFileNameWithoutExtension(outputFile) + ".partA" + extension);
            var partB = Path.Combine(Path.GetDirectoryName(outputFile) ?? "", Path.GetFileNameWithoutExtension(outputFile) + ".partB" + extension);

            try
            {
                _progress.LogInfo($"Live download for '{channel}': {splitTime:hh\\:mm\\:ss} already aired ({splitSegmentIndex} segments). "
                                  + "Downloading the back-catalog multi-threaded while recording the live tail with ffmpeg in parallel.");

                // Back-catalog uses its own CancellationTokenSource so it can run to
                // completion even when the user requests an early stop.  The back-catalog
                // is static content and downloads much faster than real time, so it will
                // almost always be done before the user ever stops the recording.
                using var backCatalogCts = new CancellationTokenSource();

                var backCatalogTask = new VideoDownloader(BuildOptions(partA, null, splitTime), _progress)
                    .DownloadAsync(backCatalogCts.Token);

                // Live tail listens to the user's cancellation token — pressing Cancel
                // gracefully stops the ffmpeg recording and lets us stitch what we have.
                var liveTailTask = RecordLiveTail(mediaUrl, splitSegmentIndex, partB, extension, channel, cancellationToken);

                bool stoppedEarly = false;
                try
                {
                    await Task.WhenAll(backCatalogTask, liveTailTask);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    stoppedEarly = true;
                    _progress.SetStatus("Recording stopped — waiting for back-catalog to finalize...");

                    // Give the back-catalog up to 60 s to finish cleanly (it is static
                    // content and almost certainly already done).  If it misses the
                    // deadline, cancel it and stitch whatever partial output we have.
                    using var backCatalogTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    try
                    {
                        await backCatalogTask.WaitAsync(backCatalogTimeoutCts.Token);
                    }
                    catch
                    {
                        backCatalogCts.Cancel();
                        try { await backCatalogTask; } catch { /* ignored */ }
                    }
                }

                _progress.SetStatus(stoppedEarly ? "Saving captured footage..." : "Stitching the two halves (lossless)...");
                try
                {
                    await ConcatLossless(partA, partB, outputFile, CancellationToken.None);
                }
                catch (Exception ex) when (stoppedEarly)
                {
                    _progress.LogWarning($"Could not save partial recording: {ex.Message}");
                }

                if (stoppedEarly)
                {
                    _progress.SetStatus("Recording stopped and saved.");
                    throw new OperationCanceledException("Recording stopped by user.", cancellationToken);
                }

                _progress.SetStatus("Live download complete.");
                _progress.ReportProgress(100);
            }
            finally
            {
                TryDelete(partA);
                TryDelete(partB);
            }
        }

        private (string outputFile, string extension) ResolveOutputPath()
        {
            var outputFile = _options.Filename;
            var extension = (Path.GetExtension(outputFile) ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".mp4";
                outputFile += extension;
            }

            var directory = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);

                if (outputFile.Length > 250)
                {
                    var name = Path.GetFileNameWithoutExtension(outputFile);
                    var available = 250 - directory.Length - 1 - extension.Length - 6;
                    if (available > 16)
                    {
                        name = name.Substring(0, Math.Min(name.Length, available));
                        outputFile = Path.Combine(directory, name + extension);
                        _progress.LogWarning($"Output path was too long; truncated to {outputFile}");
                    }
                }
            }

            return (outputFile, extension);
        }

        private VideoDownloadOptions BuildOptions(string filename, TimeSpan? trimStart, TimeSpan? trimEnd)
        {
            return new VideoDownloadOptions
            {
                Id = _options.Id,
                Quality = _options.Quality,
                Filename = filename,
                Oauth = _options.Oauth,
                FfmpegPath = _options.FfmpegPath,
                DownloadThreads = _options.DownloadThreads,
                ThrottleKib = _options.ThrottleKib,
                TempFolder = _options.TempFolder,
                TrimMode = VideoTrimMode.Safe,
                TrimBeginning = trimStart.HasValue,
                TrimBeginningTime = trimStart ?? TimeSpan.Zero,
                TrimEnding = trimEnd.HasValue,
                TrimEndingTime = trimEnd ?? TimeSpan.Zero,
            };
        }

        private async Task RecordLiveTail(string mediaUrl, int startSegmentIndex, string outFile, string extension, string channel, CancellationToken cancellationToken)
        {
            var format = extension switch
            {
                ".mp4" => "mp4",
                ".mov" => "mov",
                ".mkv" => "matroska",
                ".ts" => "mpegts",
                ".webm" => "webm",
                ".m4a" => "mp4",
                _ => "mp4"
            };
            var audioBsf = extension is ".mp4" or ".mov" or ".m4a" ? " -bsf:a aac_adtstoasc" : "";

            var arguments =
                "-hide_banner -loglevel warning "
                + "-user_agent \"Mozilla/5.0\" "
                + "-protocol_whitelist file,crypto,data,http,https,tcp,tls "
                + "-reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 5 "
                + $"-live_start_index {startSegmentIndex} "
                + $"-i \"{mediaUrl}\" -c copy{audioBsf} -f {format} -progress pipe:1 -y "
                + $"\"{outFile}\"";

            _progress.LogVerbose($"ffmpeg {arguments}");

            var errorTail = new Queue<string>();
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.FfmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null || !e.Data.StartsWith("out_time=", StringComparison.Ordinal)) return;
                var value = e.Data.Substring("out_time=".Length).Trim();
                if (TimeSpan.TryParse(value, out var captured))
                    _progress.SetStatus($"● LIVE recording '{channel}'  -  {captured:hh\\:mm\\:ss} captured");
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                _progress.LogFfmpeg(e.Data);
                lock (errorTail)
                {
                    errorTail.Enqueue(e.Data);
                    while (errorTail.Count > 20) errorTail.Dequeue();
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _progress.SetStatus("Stopping live recording...");
                try { if (!process.HasExited) await process.StandardInput.WriteLineAsync("q"); }
                catch { /* ignored */ }
                try
                {
                    using var graceCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await process.WaitForExitAsync(graceCts.Token);
                }
                catch
                {
                    try { if (!process.HasExited) process.Kill(true); } catch { /* ignored */ }
                }
                throw;
            }

            if (process.ExitCode != 0)
            {
                string tail;
                lock (errorTail) tail = string.Join(Environment.NewLine, errorTail);
                throw new Exception($"Live tail recording failed (ffmpeg code {process.ExitCode})."
                                    + (string.IsNullOrWhiteSpace(tail) ? "" : Environment.NewLine + tail));
            }
        }

        private async Task ConcatLossless(string partA, string partB, string outputFile, CancellationToken cancellationToken)
        {
            bool aExists = File.Exists(partA);
            bool bExists = File.Exists(partB);

            if (!aExists && !bExists)
                throw new Exception("Neither recording portion was produced; no output to save.");

            if (!aExists)
            {
                // Back-catalog did not complete in time — save the live tail only.
                _progress.LogWarning("Back-catalog did not complete; output contains the live-recorded portion only.");
                File.Copy(partB, outputFile, true);
                return;
            }

            if (!bExists)
            {
                File.Copy(partA, outputFile, true);
                return;
            }

            var listFile = Path.Combine(Path.GetTempPath(), $"tdw_concat_{_options.Id}_{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(listFile,
                $"file '{partA.Replace("'", "'\\''")}'\nfile '{partB.Replace("'", "'\\''")}'\n",
                cancellationToken);

            try
            {
                var args = "-hide_banner -loglevel warning -y -f concat -safe 0 "
                           + $"-i \"{listFile}\" -c copy -fflags +genpts \"{outputFile}\"";

                var errorTail = new Queue<string>();
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _options.FfmpegPath,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is null) return;
                    _progress.LogFfmpeg(e.Data);
                    lock (errorTail)
                    {
                        errorTail.Enqueue(e.Data);
                        while (errorTail.Count > 20) errorTail.Dequeue();
                    }
                };
                process.OutputDataReceived += (_, e) => { if (e.Data != null) _progress.LogFfmpeg(e.Data); };

                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0)
                {
                    string tail;
                    lock (errorTail) tail = string.Join(Environment.NewLine, errorTail);
                    throw new Exception($"Lossless stitch failed (ffmpeg code {process.ExitCode})."
                                        + (string.IsNullOrWhiteSpace(tail) ? "" : Environment.NewLine + tail));
                }
            }
            finally
            {
                TryDelete(listFile);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { /* best effort */ }
        }
    }
}
