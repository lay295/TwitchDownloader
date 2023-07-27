using System;
using System.Buffers;
using System.Text;
using SkiaSharp;
using Buffer = HarfBuzzSharp.Buffer;

namespace TwitchDownloaderCore.Extensions
{
    public static class BufferExtensions
    {
        public static void Add(this Buffer buffer, ReadOnlySpan<char> text, SKTextEncoding textEncoding)
        {
            switch (textEncoding)
            {
                // Encoding.GetBytes(ReadOnlySpan<char>, Span<byte>) internally allocates arrays, so we may as well use ArrayPools to reduce the GC footprint
                case SKTextEncoding.Utf8:
                {
                    var byteCount = Encoding.UTF8.GetByteCount(text);
                    var encodedBytes = ArrayPool<byte>.Shared.Rent(byteCount);

                    var textChars = ArrayPool<char>.Shared.Rent(text.Length);
                    text.CopyTo(textChars);

                    Encoding.UTF8.GetBytes(textChars, 0, text.Length, encodedBytes, 0);
                    buffer.AddUtf8(encodedBytes.AsSpan(0, byteCount));

                    ArrayPool<byte>.Shared.Return(encodedBytes);
                    ArrayPool<char>.Shared.Return(textChars);
                    break;
                }
                case SKTextEncoding.Utf16:
                    buffer.AddUtf16(text);
                    break;
                case SKTextEncoding.Utf32:
                {
                    var byteCount = Encoding.UTF32.GetByteCount(text);
                    var encodedBytes = ArrayPool<byte>.Shared.Rent(byteCount);

                    var textChars = ArrayPool<char>.Shared.Rent(text.Length);
                    text.CopyTo(textChars);

                    Encoding.UTF32.GetBytes(textChars, 0, text.Length, encodedBytes, 0);
                    buffer.AddUtf32(encodedBytes.AsSpan(0, byteCount));

                    ArrayPool<byte>.Shared.Return(encodedBytes);
                    ArrayPool<char>.Shared.Return(textChars);
                    break;
                }
                default:
                    throw new NotSupportedException("TextEncoding of type GlyphId is not supported.");
            }

            buffer.GuessSegmentProperties();
        }
    }
}