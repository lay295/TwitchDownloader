using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.Models
{
    // Modified from https://stackoverflow.com/a/32724000
    public class ThrottledStream : Stream
    {
        public readonly Stream BaseStream;
        public readonly int MaximumBytesPerSecond;
        private Stopwatch _watch;
        private long _totalBytesRead;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThrottledStream"/> class
        /// </summary>
        /// <param name="in">The base stream to be read from in a throttled manner</param>
        /// <param name="throttleKib">The maximum read bandwidth in kibibytes per second, capped at gigabit</param>
        public ThrottledStream(Stream @in, int throttleKib)
        {
            const int ONE_GIGABIT_IN_KIBIBYTES = 122_070;
            MaximumBytesPerSecond = Math.Min(throttleKib, ONE_GIGABIT_IN_KIBIBYTES) * 1024;
            BaseStream = @in;
        }

        public override bool CanRead => BaseStream.CanRead;

        public override bool CanSeek => BaseStream.CanSeek;

        public override bool CanWrite => false;

        public override void Flush() { }

        public override long Length => BaseStream.Length;

        public override long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var newCount = GetBytesToReturn(count);
            var read = BaseStream.Read(buffer, offset, newCount);
            Interlocked.Add(ref _totalBytesRead, read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var newCount = GetBytesToReturn(buffer.Length);
            var read = BaseStream.Read(buffer[..newCount]);
            Interlocked.Add(ref _totalBytesRead, read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return BaseStream.Seek(offset, origin);
        }

        public override void SetLength(long value) { }

        public override void Write(byte[] buffer, int offset, int count) { }

        public override void Write(ReadOnlySpan<byte> buffer) { }

        private int GetBytesToReturn(int count)
        {
            return GetBytesToReturnAsync(count).GetAwaiter().GetResult();
        }

        private async Task<int> GetBytesToReturnAsync(int count)
        {
            if (MaximumBytesPerSecond <= 0)
                return count;

            _watch ??= Stopwatch.StartNew();

            var canSend = (long)(_watch.ElapsedMilliseconds * (MaximumBytesPerSecond / 1000.0));

            var diff = (int)(canSend - _totalBytesRead);

            if (diff <= 0)
            {
                var waitInSec = ((diff * -1.0) / (MaximumBytesPerSecond));

                await Task.Delay((int)(waitInSec * 1000)).ConfigureAwait(false);
            }

            if (diff >= count) return count;

            return diff > 0 ? diff : Math.Min(1024 * 8, count);
        }
    }
}