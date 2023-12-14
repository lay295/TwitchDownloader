using System;

namespace TwitchDownloaderCore.Extensions
{
    public static class ReadOnlySpanExtensions
    {
        /// <summary>Replaces all occurrences of <paramref name="oldChar"/> not prepended by a backslash or contained within quotation marks with <paramref name="newChar"/>.</summary>
        public static bool TryReplaceNonEscaped(this ReadOnlySpan<char> str, Span<char> destination, char oldChar, char newChar)
        {
            const string ESCAPE_CHARS = @"\'""";

            if (oldChar is '\\' or '\'' or '\"')
                return false;

            if (destination.Length < str.Length)
                return false;

            str.CopyTo(destination);

            var firstIndex = destination.IndexOf(oldChar);
            if (firstIndex == -1)
                return true;

            var firstEscapeIndex = destination.IndexOfAny(ESCAPE_CHARS);
            if (firstEscapeIndex != -1 && firstEscapeIndex < firstIndex)
                firstIndex = firstEscapeIndex;

            var lastIndex = destination.LastIndexOf(oldChar);
            var lastEscapeIndex = destination.LastIndexOfAny(ESCAPE_CHARS);
            if (lastEscapeIndex != -1 && lastEscapeIndex > lastIndex)
                lastIndex = lastEscapeIndex;

            lastIndex++;
            for (var i = firstIndex; i < lastIndex; i++)
            {
                var readChar = destination[i];

                switch (readChar)
                {
                    case '\\':
                        i++;
                        break;
                    case '\'':
                    case '\"':
                    {
                        i = FindCloseQuoteChar(destination, i, lastIndex, readChar);

                        if (i == -1)
                        {
                            destination.Clear();
                            return false;
                        }

                        break;
                    }
                    default:
                    {
                        if (readChar == oldChar)
                        {
                            destination[i] = newChar;
                        }

                        break;
                    }
                }
            }

            return true;
        }

        private static int FindCloseQuoteChar(ReadOnlySpan<char> destination, int openQuoteIndex, int endIndex, char openQuoteChar)
        {
            var i = openQuoteIndex + 1;
            while (i < endIndex)
            {
                var readChar = destination[i];
                i++;

                if (readChar == '\\')
                {
                    i++;
                    continue;
                }

                if (readChar == openQuoteChar)
                {
                    i--;
                    return i;
                }
            }

            return -1;
        }
    }
}