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
                if (!format.AsSpan().TryReplaceNonEscaped(newFormat, out var charsWritten, 'H', 'h'))
                {
                    throw new Exception("Failed to generate ToString() compatible format. This should not have been possible.");
                }

                // If the format contains more than 2 sequential unescaped h's, it will throw a format exception. If so, we can fallback to our parser.
                if (newFormat.IndexOf("hhh") == -1)
                {
                    return HandleOtherFormats(newFormat[..charsWritten].ToString(), timeSpan, formatProvider);
                }
            }

            var sb = new StringBuilder(format.Length);
            var regularFormatCharStart = -1;
            var bigHStart = -1;
            var formatSpan = format.AsSpan();
            for (var i = 0; i < formatSpan.Length; i++)
            {
                var readChar = formatSpan[i];

                if (readChar == 'H')
                {
                    if (bigHStart == -1)
                    {
                        bigHStart = i;
                    }

                    if (regularFormatCharStart != -1)
                    {
                        AppendRegularFormat(sb, timeSpan, format, regularFormatCharStart, i - regularFormatCharStart);
                        regularFormatCharStart = -1;
                    }
                }
                else
                {
                    if (regularFormatCharStart == -1)
                    {
                        regularFormatCharStart = i;
                    }

                    if (bigHStart != -1)
                    {
                        AppendBigHFormat(sb, timeSpan, i - bigHStart);
                        bigHStart = -1;
                    }

                    // If the current char is an escape we can skip the next char
                    if (readChar == '\\' && i + 1 < formatSpan.Length)
                    {
                        i++;
                    }
                }
            }

            if (regularFormatCharStart != -1)
            {
                AppendRegularFormat(sb, timeSpan, format, regularFormatCharStart, formatSpan.Length - regularFormatCharStart);
            }
            else if (bigHStart != -1)
            {
                AppendBigHFormat(sb, timeSpan, formatSpan.Length - bigHStart);
            }

            return sb.ToString();
        }

        private static void AppendRegularFormat(StringBuilder sb, TimeSpan timeSpan, string formatString, int start, int length)
        {
            Span<char> destination = stackalloc char[256];
            var format = formatString.AsSpan(start, length);

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
            Span<char> destination = stackalloc char[8];
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