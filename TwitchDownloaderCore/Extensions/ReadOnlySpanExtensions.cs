using System;

namespace TwitchDownloaderCore.Extensions
{
    public static class ReadOnlySpanExtensions
    {
        /// <summary>Replaces all occurrences of <paramref name="oldChar"/> not prepended by a backslash with <paramref name="newChar"/>.</summary>
        public static bool TryReplaceNonEscaped(this ReadOnlySpan<char> str, Span<char> destination, out int charsWritten, char oldChar, char newChar)
        {
            if (destination.Length < str.Length)
            {
                charsWritten = 0;
                return false;
            }

            str.CopyTo(destination);
            charsWritten = str.Length;

            var firstIndex = destination.IndexOf(oldChar);

            if (firstIndex == -1)
            {
                return true;
            }

            firstIndex = Math.Min(firstIndex, destination.IndexOf('\\'));

            for (var i = firstIndex; i < str.Length; i++)
            {
                var readChar = destination[i];

                if (readChar == '\\' && i + 1 < str.Length)
                {
                    i++;
                    continue;
                }

                if (readChar == oldChar)
                {
                    destination[i] = newChar;
                }
            }

            return true;
        }
    }
}