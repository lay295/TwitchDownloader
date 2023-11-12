using System;
using System.Text;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tools
{
    /// <summary>Adds an 'H' parameter to <see cref="TimeSpan"/> string formatting. The 'H' parameter is equivalent to flooring <see cref="TimeSpan"/>.<see cref="TimeSpan.TotalHours"/>.</summary>
    /// <remarks>
    /// This formatter only supports escaping 'H's via '\'.
    /// For optimal memory performance, resulting strings split about any 'H' parameters should be less than 256.
    /// </remarks>
    public class TimeSpanHFormat : IFormatProvider, ICustomFormatter
    {
        public static readonly TimeSpanHFormat ReusableInstance = new();

        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter))
                return this;
            else
                return null;
        }

        public string Format(string format, object arg, IFormatProvider formatProvider = null)
        {
            if (arg is TimeSpan timeSpan)
            {
                return Format(format, timeSpan);
            }

            return HandleOtherFormats(format, arg, formatProvider);
        }

        /// <summary>Provides an identical output to <see cref="Format(string,object,IFormatProvider)"/> but without boxing the <paramref name="timeSpan"/>.</summary>
        /// <remarks>This method is not part of the <see cref="ICustomFormatter"/> interface.</remarks>
        public string Format(string format, TimeSpan timeSpan, IFormatProvider formatProvider = null)
        {
            if (string.IsNullOrEmpty(format))
            {
                return "";
            }

            if (!format.Contains('H'))
            {
                return HandleOtherFormats(format, timeSpan, formatProvider);
            }

            // If the timespan is less than 24 hours, HandleOtherFormats can be up to 3x faster and half the allocations
            if (timeSpan.Days == 0)
            {
                var newFormat = format.Length <= 256 ? stackalloc char[format.Length] : new char[format.Length];
                if (!format.AsSpan().TryReplaceNonEscaped(newFormat, 'H', 'h'))
                {
                    throw new FormatException($"Invalid character escaping in the format string: {format}");
                }

                // If the format contains more than 2 sequential unescaped h's, it will throw a format exception. If so, we can fallback to our parser.
                if (newFormat.IndexOf("hhh") == -1)
                {
                    return HandleOtherFormats(newFormat.ToString(), timeSpan, formatProvider);
                }
            }

            return HandleBigHFormat(format.AsSpan(), timeSpan);
        }

        private static string HandleBigHFormat(ReadOnlySpan<char> format, TimeSpan timeSpan)
        {
            var formatLength = format.Length;
            var sb = new StringBuilder(formatLength);
            var regularFormatCharStart = -1;
            var bigHStart = -1;
            for (var i = 0; i < formatLength; i++)
            {
                var readChar = format[i];

                if (readChar == 'H')
                {
                    if (bigHStart == -1)
                        bigHStart = i;

                    if (regularFormatCharStart != -1)
                    {
                        var formatEnd = i - regularFormatCharStart;
                        AppendRegularFormat(sb, timeSpan, format.Slice(regularFormatCharStart, formatEnd));
                        regularFormatCharStart = -1;
                    }
                }
                else
                {
                    if (regularFormatCharStart == -1)
                        regularFormatCharStart = i;

                    if (bigHStart != -1)
                    {
                        var bigHCount = i - bigHStart;
                        AppendBigHFormat(sb, timeSpan, bigHCount);
                        bigHStart = -1;
                    }

                    switch (readChar)
                    {
                        // If the current char is an escape we can skip the next char
                        case '\\' when i + 1 < formatLength:
                            i++;
                            continue;
                        // If the current char is a quote we can skip the next quote, if it exists
                        case '\'' when i + 1 < formatLength:
                        case '\"' when i + 1 < formatLength:
                        {
                            i = FindCloseQuoteMark(format, i, formatLength, readChar);

                            if (i == -1)
                            {
                                throw new FormatException($"Invalid character escaping in the format string: {format}");
                            }

                            continue;
                        }
                    }
                }
            }

            if (regularFormatCharStart != -1)
            {
                var formatEnd = format.Length - regularFormatCharStart;
                AppendRegularFormat(sb, timeSpan, format.Slice(regularFormatCharStart, formatEnd));
            }
            else if (bigHStart != -1)
            {
                var bigHCount = format.Length - bigHStart;
                AppendBigHFormat(sb, timeSpan, bigHCount);
            }

            return sb.ToString();
        }

        private static int FindCloseQuoteMark(ReadOnlySpan<char> format, int openQuoteIndex, int endIndex, char readChar)
        {
            var i = openQuoteIndex + 1;
            var quoteFound = false;
            while (i < endIndex)
            {
                var readCharQuote = format[i];
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

        private static void AppendRegularFormat(StringBuilder sb, TimeSpan timeSpan, ReadOnlySpan<char> format)
        {
            Span<char> destination = stackalloc char[256];

            if (timeSpan.TryFormat(destination, out var charsWritten, format))
            {
                sb.Append(destination[..charsWritten]);
            }
            else
            {
                sb.Append(timeSpan.ToString(format.ToString()));
            }
        }

        private static void AppendBigHFormat(StringBuilder sb, TimeSpan timeSpan, int count)
        {
            const int TIMESPAN_MAX_HOURS_LENGTH = 9; // The maximum integer hours a TimeSpan can hold is 256204778.
            Span<char> destination = stackalloc char[TIMESPAN_MAX_HOURS_LENGTH];
            Span<char> format = stackalloc char[count];
            format.Fill('0');

            if (((int)timeSpan.TotalHours).TryFormat(destination, out var charsWritten, format))
            {
                sb.Append(destination[..charsWritten]);
            }
            else
            {
                sb.Append(((int)timeSpan.TotalHours).ToString(format.ToString()));
            }
        }

        private static string HandleOtherFormats(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg is IFormattable formattable)
                return formattable.ToString(format, formatProvider);
            else if (arg != null)
                return arg.ToString();
            else
                return "";
        }

        private static string HandleOtherFormats(string format, TimeSpan arg, IFormatProvider formatProvider) => arg.ToString(format, formatProvider);
    }
}