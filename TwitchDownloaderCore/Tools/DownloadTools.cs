using System;
using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCore.Tools
{
    public static class DownloadTools
    {
        /// <summary>
        /// Downloads the requested <paramref name="url"/> to the <paramref name="destinationFile"/> without storing it in memory.
        /// </summary>
        /// <param name="httpClient">The <see cref="HttpClient"/> to perform the download operation.</param>
        /// <param name="url">The url of the file to download.</param>
        /// <param name="destinationFile">The path to the file where download will be saved.</param>
        /// <param name="throttleKib">The maximum download speed in kibibytes per second, or -1 for no maximum.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="cancellationTokenSource">A <see cref="CancellationTokenSource"/> containing a <see cref="CancellationToken"/> to cancel the operation.</param>
        /// <remarks>The <paramref name="cancellationTokenSource"/> may be canceled by this method.</remarks>
        public static async Task DownloadFileAsync(HttpClient httpClient, Uri url, string destinationFile, int throttleKib, ITaskLogger logger, CancellationTokenSource cancellationTokenSource = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var cancellationToken = cancellationTokenSource?.Token ?? CancellationToken.None;

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Why are we setting a CTS CancelAfter timer? See lay295#265
            const int SIXTY_SECONDS = 60;
            if (throttleKib == -1 || !response.Content.Headers.ContentLength.HasValue)
            {
                cancellationTokenSource?.CancelAfter(TimeSpan.FromSeconds(SIXTY_SECONDS));
            }
            else
            {
                const double ONE_KIBIBYTE = 1024d;
                cancellationTokenSource?.CancelAfter(TimeSpan.FromSeconds(Math.Max(
                    SIXTY_SECONDS,
                    response.Content.Headers.ContentLength!.Value / ONE_KIBIBYTE / throttleKib * 8 // Allow up to 8x the shortest download time given the thread bandwidth
                    )));
            }

            switch (throttleKib)
            {
                case -1:
                {
                    await using var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                    await response.Content.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                    break;
                }
                default:
                {
                    try
                    {
                        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                        await using var throttledStream = new ThrottledStream(contentStream, throttleKib);
                        await using var fs = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.Read);
                        await throttledStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
                    }
                    catch (IOException ex) when (ex.Message.Contains("EOF"))
                    {
                        // If we get an exception for EOF, it may be related to the throttler. Try again without it.
                        logger.LogVerbose($"Unexpected EOF, retrying without bandwidth throttle. Message: {ex.Message}.");
                        await Task.Delay(2_000, cancellationToken);
                        goto case -1;
                    }
                    break;
                }
            }

            // Reset the cts timer so it can be reused for the next download on this thread.
            // Is there a friendlier way to do this? Yes. Does it involve creating and destroying 4,000 CancellationTokenSources that are almost never cancelled? Also Yes.
            cancellationTokenSource?.CancelAfter(TimeSpan.FromMilliseconds(uint.MaxValue - 1));
        }

        /// <summary>
        /// Some old twitch VODs have files with a query string at the end such as 1.ts?offset=blah which isn't a valid filename
        /// </summary>
        public static string RemoveQueryString(string inputString)
        {
            var queryIndex = inputString.IndexOf('?');
            if (queryIndex == -1)
            {
                return inputString;
            }

            return inputString[..queryIndex];
        }

        // https://tsduck.io/download/docs/mpegts-introduction.pdf
        public static async Task<TsVerifyResult> VerifyTransportStream(string filePath, ITaskLogger logger)
        {
            const int TS_PACKET_LENGTH = 188; // MPEG TS packets are made of a header and a body: [ 4B ][   184B   ]
            const int TS_PACKET_FIRST_BYTE = 0x47;

            if (!File.Exists(filePath))
            {
                return TsVerifyResult.NotFound;
            }

            await using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!fs.CanSeek)
            {
                return TsVerifyResult.Unknown;
            }

            if (fs.Length == 0)
            {
                return TsVerifyResult.Empty;
            }

            if (fs.Length % TS_PACKET_LENGTH != 0)
            {
                return TsVerifyResult.CorruptedPackets;
            }

            using var memoryOwner = MemoryPool<byte>.Shared.Rent(1);
            var buffer = memoryOwner.Memory[..1];
            do
            {
                var bytesRead = await fs.ReadAsync(buffer);
                if (bytesRead != 1)
                {
                    logger.LogWarning($"Read {bytesRead} bytes from {filePath}. Expected 1.");
                }

                if (buffer.Span[0] != TS_PACKET_FIRST_BYTE)
                {
                    return TsVerifyResult.CorruptedPackets;
                }

                fs.Seek(TS_PACKET_LENGTH - 1, SeekOrigin.Current);
            } while (fs.Position < fs.Length);

            return TsVerifyResult.Success;
        }
    }
}