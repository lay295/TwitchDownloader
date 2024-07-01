using System;

namespace TwitchDownloaderCore.Extensions
{
    public static class StringExtensions
    {
        public static string ReplaceAny(this string str, ReadOnlySpan<char> oldChars, char newChar)
        {
            if (string.IsNullOrEmpty(str))
                return str;

            var index = str.AsSpan().IndexOfAny(oldChars);
            if (index == -1)
                return str;

            const ushort MAX_STACK_SIZE = 512;
            var span = str.Length <= MAX_STACK_SIZE
                ? stackalloc char[str.Length]
                : str.ToCharArray();

            // Unfortunately this cannot be inlined with the previous statement because a ternary is required for the stackalloc to compile
            if (str.Length <= MAX_STACK_SIZE)
                str.CopyTo(span);

            var tempSpan = span;
            do
            {
                tempSpan[index] = newChar;
                tempSpan = tempSpan[(index + 1)..];

                index = tempSpan.IndexOfAny(oldChars);
                if (index == -1)
                    break;
            } while (true);

            return span.ToString();
        }
    }
}