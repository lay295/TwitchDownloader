using System;
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
                case SKTextEncoding.Utf8:
                {
                    Span<byte> byteSpan = stackalloc byte[Encoding.UTF8.GetByteCount(text)];
                    Encoding.UTF8.GetBytes(text, byteSpan);
                    buffer.AddUtf8(byteSpan);
                    break;
                }
                case SKTextEncoding.Utf16:
                    buffer.AddUtf16(text);
                    break;
                case SKTextEncoding.Utf32:
                {
                    Span<byte> byteSpan = stackalloc byte[Encoding.UTF8.GetByteCount(text)];
                    Encoding.UTF32.GetBytes(text, byteSpan);
                    buffer.AddUtf32(byteSpan);
                    break;
                }
                default:
                    throw new NotSupportedException("TextEncoding of type GlyphId is not supported.");
            }

            buffer.GuessSegmentProperties();
        }
    }
}