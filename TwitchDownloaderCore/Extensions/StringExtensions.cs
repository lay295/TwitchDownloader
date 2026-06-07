using System;
using System.Buffers;

namespace TwitchDownloaderCore.Extensions
{
    public static class StringExtensions
    {
        public static string ReplaceAny(this string str, SearchValues<char> oldChars, char newChar)
        {
            if (string.IsNullOrEmpty(str))
            {
                return str;
            }

            var index = str.IndexOfAny(oldChars);
            if (index == -1)
            {
                return str;
            }

            var span = str.Length <= 512
                ? stackalloc char[str.Length]
                : new char[str.Length];

            str.ReplaceAny(span, oldChars, newChar);
            return span.ToString();
        }
    }
}