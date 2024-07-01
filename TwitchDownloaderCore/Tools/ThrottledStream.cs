using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.Tools;

// Modified from https://stackoverflow.com/a/32724000
public class ThrottledStream : Stream {
    public readonly Stream BaseStream;
    public readonly int MaximumBytesPerSecond;
    private long _totalBytesRead;
    private Stopwatch _watch;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ThrottledStream" /> class
    /// </summary>
    /// <param name="in">The base stream to be read from in a throttled manner</param>
    /// <param name="throttleKib">The maximum read bandwidth in kibibytes per second, capped at gigabit</param>
    public ThrottledStream(Stream @in, int throttleKib) {
        const int ONE_GIGABIT_IN_KIBIBYTES = 122_070;
        this.MaximumBytesPerSecond = Math.Min(throttleKib, ONE_GIGABIT_IN_KIBIBYTES) * 1024;
        this.BaseStream = @in;
    }

    public override bool CanRead => this.BaseStream.CanRead;

    public override bool CanSeek => this.BaseStream.CanSeek;

    public override bool CanWrite => false;

    public override long Length => this.BaseStream.Length;

    public override long Position {
        get => this.BaseStream.Position;
        set => this.BaseStream.Position = value;
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) {
        var newCount = this.GetBytesToReturn(count);
        var read = this.BaseStream.Read(buffer, offset, newCount);
        Interlocked.Add(ref this._totalBytesRead, read);
        return read;
    }

    public override int Read(Span<byte> buffer) {
        var newCount = this.GetBytesToReturn(buffer.Length);
        var read = this.BaseStream.Read(buffer[..newCount]);
        Interlocked.Add(ref this._totalBytesRead, read);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => this.BaseStream.Seek(offset, origin);

    public override void SetLength(long value) { }

    public override void Write(byte[] buffer, int offset, int count) { }

    public override void Write(ReadOnlySpan<byte> buffer) { }

    private int GetBytesToReturn(int count) => this.GetBytesToReturnAsync(count).GetAwaiter().GetResult();

    private async Task<int> GetBytesToReturnAsync(int count) {
        if (this.MaximumBytesPerSecond <= 0)
            return count;

        this._watch ??= Stopwatch.StartNew();

        var canSend = (long)(this._watch.ElapsedMilliseconds * (this.MaximumBytesPerSecond / 1000.0));

        var diff = (int)(canSend - this._totalBytesRead);

        if (diff <= 0) {
            var waitInSec = diff * -1.0 / this.MaximumBytesPerSecond;

            await Task.Delay((int)(waitInSec * 1000)).ConfigureAwait(false);
        }

        if (diff >= count) return count;

        return diff > 0 ? diff : Math.Min(1024 * 8, count);
    }
}
