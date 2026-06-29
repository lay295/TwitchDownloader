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

        /// <summary>Replaces the N-th occurrence of a string between 2 occurrences of a <paramref name="delimiter"/> or the start/end of a string.</summary>
        /// <example>
        /// <see cref="ReplaceNthOccurrence"/>("1/2/3", '/', 0, "A") -> "A/2/3"
        /// <see cref="ReplaceNthOccurrence"/>("1/2/3", '/', 1, "A") -> "1/A/3"
        /// <see cref="ReplaceNthOccurrence"/>("1/2/3", '/', 2, "A") -> "1/2/A"
        /// <see cref="ReplaceNthOccurrence"/>("1/2/3", '/', ^1, "A") -> "1/2/A"
        /// <see cref="ReplaceNthOccurrence"/>("1/2/3", '/', ^2, "A") -> "1/A/3"
        /// <see cref="ReplaceNthOccurrence"/>("1/2/3", '/', ^3, "A") -> "A/2/3"
        /// <see cref="ReplaceNthOccurrence"/>("1/2/3", '/', 10, "A") -> "1/2/3"
        /// <see cref="ReplaceNthOccurrence"/>("123", '/', 0, "A") -> "A"
        /// </example>
        /// <param name="str">The source string.</param>
        /// <param name="delimiter">The delimiter.</param>
        /// <param name="index">The N-th delimited range to replace.</param>
        /// <param name="newValue">The value to be inserted between the delimiters.</param>
        /// <returns>The modified string.</returns>
        public static string ReplaceNthOccurrence(this string str, char delimiter, Index index, ReadOnlySpan<char> newValue)
        {
            if (string.IsNullOrEmpty(str))
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
                    // If no delimiters were found and Index is 0, return the new value
                    if (currentIndex == 0 && idxA == -1)
                    {
                        return newValue.ToString();
                    }

                    if (index.IsFromEnd)
                    {
                        return string.Concat(
                            str.AsSpan(0, idxA + 1),
                            newValue,
                            str.AsSpan(idxB)
                        );
                    }

                    if (idxA == -1) idxA = str.Length;
                    return string.Concat(
                        str.AsSpan(0, idxB + 1),
                        newValue,
                        str.AsSpan(idxA)
                    );

                }

                // Index is out of range of delimiter chars, return original string
                if (idxA < 0)
                {
                    return str;
                }

                idxB = idxA;
                currentIndex++;
            }
        }
    }
}