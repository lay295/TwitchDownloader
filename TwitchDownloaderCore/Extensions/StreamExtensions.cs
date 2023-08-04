using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.Extensions
{
    public record struct StreamCopyProgress(long SourceLength, long BytesCopied);

    public static class StreamExtensions
    {
        // The default size from Stream.GetCopyBufferSize() is 81_920.
        private const int STREAM_DEFAULT_BUFFER_LENGTH = 81_920;

        public static async Task ProgressCopyToAsync(this Stream source, Stream destination, long? sourceLength, IProgress<StreamCopyProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (!sourceLength.HasValue)
            {
                await source.CopyToAsync(destination, cancellationToken);
                return;
            }

            var rentedBuffer = ArrayPool<byte>.Shared.Rent(STREAM_DEFAULT_BUFFER_LENGTH);
            var buffer = rentedBuffer.AsMemory(0, STREAM_DEFAULT_BUFFER_LENGTH);
            var totalBytesRead = 0L;

            try
            {
                var bytesRead = 0;
                do
                {
                    bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (bytesRead == 0)
                        continue;

                    await destination.WriteAsync(buffer[..bytesRead], cancellationToken).ConfigureAwait(false);
                    totalBytesRead += bytesRead;

                    progress?.Report(new StreamCopyProgress(sourceLength!.Value, totalBytesRead));
                } while (bytesRead != 0);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }
}