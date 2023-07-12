using System;
using System.IO;
using System.Text;

namespace TwitchDownloaderCore.Tools
{
    /// <summary>
    /// Adds an 'H' parameter to <see cref="TimeSpan"/> string formatting. The 'H' parameter is equivalent to flooring <see cref="TimeSpan"/>.<see cref="TimeSpan.TotalHours"/>.
    /// </summary>
    /// <remarks>
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

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format))
            {
                return "";
            }

            if (!(arg is TimeSpan timeSpan))
            {
                return HandleOtherFormats(format, arg, formatProvider);
            }

            if (!format.Contains('H'))
            {
                return HandleOtherFormats(format, arg, formatProvider);
            }

            using var reader = new StringReader(format);
            var formattedStringBuilder = new StringBuilder(format.Length);
            var regularFormatCharStart = -1;
            var bigHStart = -1;
            var position = -1;
            do
            {
                var readChar = reader.Read();
                position++;

                if (readChar == 'H')
                {
                    if (bigHStart == -1)
                    {
                        bigHStart = position;
                    }

                    if (regularFormatCharStart != -1)
                    {
                        AppendRegularFormat(formattedStringBuilder, timeSpan, format, regularFormatCharStart, position - regularFormatCharStart);
                        regularFormatCharStart = -1;
                    }
                }
                else
                {
                    if (regularFormatCharStart == -1)
                    {
                        regularFormatCharStart = position;
                    }

                    if (bigHStart != -1)
                    {
                        AppendBigHFormat(formattedStringBuilder, timeSpan, position - bigHStart);
                        bigHStart = -1;
                    }

                    // If the current char is an escape and the next char is an H, we need to escape it
                    if (readChar == '\\' && reader.Peek() == 'H')
                    {
                        _ = reader.Read();
                        position++;
                    }
                }
            } while (reader.Peek() != -1);

            position++;
            if (regularFormatCharStart != -1)
            {
                AppendRegularFormat(formattedStringBuilder, timeSpan, format, regularFormatCharStart, position - regularFormatCharStart);
            }
            else if (bigHStart != -1)
            {
                AppendBigHFormat(formattedStringBuilder, timeSpan, position - bigHStart);
            }

            return formattedStringBuilder.ToString();
        }

        private static void AppendRegularFormat(StringBuilder formattedStringBuilder, TimeSpan timeSpan, string formatString, int start, int length)
        {
            Span<char> destination = stackalloc char[256];
            var format = formatString.AsSpan(start, length);

            if (timeSpan.TryFormat(destination, out var charsWritten, format))
            {
                formattedStringBuilder.Append(destination[..charsWritten]);
            }
            else
            {
                formattedStringBuilder.Append(timeSpan.ToString(format.ToString()));
            }
        }

        private static void AppendBigHFormat(StringBuilder formattedStringBuilder, TimeSpan timeSpan, int count)
        {
            Span<char> destination = stackalloc char[8];
            Span<char> format = stackalloc char[count];
            format.Fill('0');

            if (((int)timeSpan.TotalHours).TryFormat(destination, out var charsWritten, format))
            {
                formattedStringBuilder.Append(destination[..charsWritten]);
            }
            else
            {
                formattedStringBuilder.Append(((int)timeSpan.TotalHours).ToString(format.ToString()));
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
    }
}