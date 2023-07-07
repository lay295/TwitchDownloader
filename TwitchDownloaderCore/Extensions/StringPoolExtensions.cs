using System;
using CommunityToolkit.HighPerformance.Buffers;

namespace TwitchDownloaderCore.Extensions
{
    public static class StringPoolExtensions
    {
        public static string ConcatAndGetOrAdd(this StringPool stringPool, string str1, string str2)
        {
            var span = (Span<char>)stackalloc char[str1.Length + str2.Length];

            str1.CopyTo(span);

            var span2 = span[str1.Length..];
            str2.CopyTo(span2);

            return stringPool.GetOrAdd(span);
        }

        public static string ConcatAndGetOrAdd(this StringPool stringPool, string str1, string str2, string str3)
        {
            var span = (Span<char>)stackalloc char[str1.Length + str2.Length + str3.Length];

            str1.CopyTo(span);

            var span2 = span.Slice(str1.Length, str2.Length);
            str2.CopyTo(span2);

            span2 = span[(str1.Length + str2.Length)..];
            str3.CopyTo(span2);

            return stringPool.GetOrAdd(span);
        }
    }
}