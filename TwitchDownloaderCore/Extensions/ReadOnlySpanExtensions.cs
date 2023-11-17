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
                        i = FindCloseQuoteMark(destination, i, lastIndex, readChar);

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

        private static int FindCloseQuoteMark(ReadOnlySpan<char> destination, int openQuoteIndex, int endIndex, char readChar)
        {
            var i = openQuoteIndex + 1;
            var quoteFound = false;
            while (i < endIndex)
            {
                var readCharQuote = destination[i];
                i++;

                if (readCharQuote == '\\')
                {
                    i++;
                    continue;
                }

                if (readCharQuote == readChar)
                {
                    i--;
                    quoteFound = true;
                    break;
                }
            }

            return quoteFound ? i : -1;
        }

        public static int UnEscapedIndexOf(this ReadOnlySpan<char> str, char character)
        {
            if (character is '\\' or '\'' or '\"')
                throw new ArgumentException("Escape characters are not supported.", nameof(character));

            var length = str.Length;
            for (var i = 0; i < length; i++)
            {
                var readChar = str[i];

                switch (readChar)
                {
                    case '\\':
                        i++;
                        break;
                    case '\'':
                    case '\"':
                    {
                        var closeQuoteMark = FindCloseQuoteMark(str, i, length, readChar);
                        if (closeQuoteMark == -1)
                            throw new FormatException($"Unbalanced quote mark at {i}.");

                        i = closeQuoteMark;

                        break;
                    }
                    default:
                    {
                        if (readChar == character)
                        {
                            return i;
                        }

                        break;
                    }
                }
            }

            return -1;
        }

        public static int Count(this ReadOnlySpan<char> str, char character)
        {
            if (str.IsEmpty)
                return -1;

            var count = 0;
            var temp = str;
            int index;

            while ((index = temp.IndexOf(character)) != -1)
            {
                count++;
                temp = temp[(index + 1)..];
            }

            return count == 0 ? -1 : count;
        }
    }
}