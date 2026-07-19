using System.Diagnostics;
using System.Globalization;
using NeoSmart.Unicode;

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
                            // The rest of the string is escaped
                            return true;
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

        public static int UnEscapedIndexOf(this ReadOnlySpan<char> str, char character)
        {
            if (character is '\\' or '\'' or '\"')
                throw new ArgumentOutOfRangeException(nameof(character), character, "Searching for escape characters is not supported.");

            var firstIndex = str.IndexOf(character);
            if (firstIndex == -1)
                return firstIndex;

            var firstEscapeIndex = str.IndexOfAny(@"\'""");
            if (firstEscapeIndex == -1 || firstEscapeIndex > firstIndex)
                return firstIndex;

            var length = str.Length;
            for (var i = firstEscapeIndex; i < length; i++)
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
                        var closeQuoteMark = FindCloseQuoteChar(str, i, length, readChar);

                        if (closeQuoteMark == -1)
                        {
                            // The rest of the string is escaped
                            return -1;
                        }

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

        public static int UnEscapedIndexOfAny(this ReadOnlySpan<char> str, ReadOnlySpan<char> characters)
        {
            const string ESCAPE_CHARS = @"\'""";

            if (characters.IndexOfAny(ESCAPE_CHARS) != -1)
                throw new ArgumentOutOfRangeException(nameof(characters), characters.ToString(), "Searching for escape characters is not supported.");

            var firstIndex = str.IndexOfAny(characters);
            if (firstIndex == -1)
                return firstIndex;

            var firstEscapeIndex = str.IndexOfAny(ESCAPE_CHARS);
            if (firstEscapeIndex == -1 || firstEscapeIndex > firstIndex)
                return firstIndex;

            var length = str.Length;
            for (var i = firstEscapeIndex; i < length; i++)
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
                        var closeQuoteMark = FindCloseQuoteChar(str, i, length, readChar);

                        if (closeQuoteMark == -1)
                        {
                            // The rest of the string is escaped
                            return -1;
                        }

                        i = closeQuoteMark;

                        break;
                    }
                    default:
                    {
                        if (characters.Contains(readChar))
                        {
                            return i;
                        }

                        break;
                    }
                }
            }

            return -1;
        }

        /// <inheritdoc cref="string.IndexOf(char, int)"/>
        public static int IndexOf<T>(this ReadOnlySpan<T> str, T value, int startIndex) where T : IEquatable<T>
        {
            var result = str[startIndex..].IndexOf(value);

            return result < 0 ? result : result + startIndex;
        }

        /// <inheritdoc cref="string.LastIndexOf(char, int)"/>
        public static int LastIndexOf<T>(this ReadOnlySpan<T> str, T value, int startIndex) where T : IEquatable<T>
        {
            return str[..(startIndex + 1)].LastIndexOf(value);
        }

        /// <summary>Returns the N-th occurrence of a substring between 2 occurrences of a <paramref name="delimiter"/> or the start/end of a span.</summary>
        /// <example>
        /// <see cref="GetNthOccurrence"/>("1/2/3", '/', 0) -> "1"
        /// <see cref="GetNthOccurrence"/>("1/2/3", '/', 1) -> "2"
        /// <see cref="GetNthOccurrence"/>("1/2/3", '/', 2) -> "3"
        /// <see cref="GetNthOccurrence"/>("1/2/3", '/', ^1) -> "3"
        /// <see cref="GetNthOccurrence"/>("1/2/3", '/', ^2) -> "2"
        /// <see cref="GetNthOccurrence"/>("1/2/3", '/', ^3) -> "1"
        /// <see cref="GetNthOccurrence"/>("1/2/3", '/', 10) -> <see cref="ArgumentOutOfRangeException"/>
        /// <see cref="GetNthOccurrence"/>("123", '/', 0) -> "123"
        /// </example>
        /// <param name="str">The source span.</param>
        /// <param name="delimiter">The delimiter.</param>
        /// <param name="index">The N-th delimited range to extract.</param>
        /// <returns>A slice of the source span.</returns>
        public static ReadOnlySpan<char> GetNthOccurrence(this ReadOnlySpan<char> str, char delimiter, Index index)
        {
            if (str.IsEmpty)
            {
                return str;
            }

            var idxB = index.IsFromEnd ? str.Length : -1;
            var indexValue = index.IsFromEnd ? index.Value - 1 : index.Value;
            var currentIndex = 0;
            while (true)
            {
                int idxA;
                if (index.IsFromEnd)
                {
                    idxA = idxB > 0
                        ? str.LastIndexOf(delimiter, idxB - 1)
                        : -1;
                }
                else
                {
                    idxA = str.IndexOf(delimiter, idxB + 1);
                }

                if (currentIndex == indexValue)
                {
                    // If no delimiters were found and Index is 0, return the whole string
                    if (currentIndex == 0 && idxA == -1)
                    {
                        return str;
                    }

                    if (index.IsFromEnd)
                    {
                        return str[(idxA + 1)..idxB];
                    }

                    if (idxA == -1) idxA = str.Length;
                    return str[(idxB + 1)..idxA];
                }

                // Index is out of range of delimiter chars, throw
                if (idxA < 0 || currentIndex > indexValue)
                {
                    throw new ArgumentOutOfRangeException();
                }

                idxB = idxA;
                currentIndex++;
            }
        }

        public static int LengthInTextElements(this ReadOnlySpan<char> str)
        {
            var length = 0;

            var slice = str;
            while (!slice.IsEmpty)
            {
                var elementLength = char.IsAscii(slice[0])
                    ? 1
                    : StringInfo.GetNextTextElementLength(slice);

                slice = slice[elementLength..];
                length++;
            }

            Debug.Assert(length == new StringInfo(str.ToString()).LengthInTextElements);
            return length;
        }

        public static bool StartsWith(this ReadOnlySpan<char> str, IEnumerable<Codepoint> codepoints)
        {
            var slice = str;

            using var enumerator = codepoints.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                var currentLength = current.Value < ushort.MaxValue ? 1 : 2;

                if (slice.Length < currentLength)
                {
                    return false;
                }

                var codepointSpan = slice[..currentLength];
                slice = slice[currentLength..];

                var codepoint = currentLength > 1 && char.IsHighSurrogate(codepointSpan[0]) && char.IsLowSurrogate(codepointSpan[1])
                    ? char.ConvertToUtf32(codepointSpan[0], codepointSpan[1])
                    : codepointSpan[0];

                if (codepoint != current)
                {
                    return false;
                }
            }

            return true;
        }
    }
}