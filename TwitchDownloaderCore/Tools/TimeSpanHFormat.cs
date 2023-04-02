using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace TwitchDownloaderCore.Tools
{
    /// <summary>
    /// Adds an 'H' parameter to TimeSpan string formatting. The 'H' parameter is equivalent to flooring <see cref="TimeSpan"/>.TotalHours.
    /// </summary>
    /// <remarks>
    /// The fact that this is not part of .NET is stupid.
    /// </remarks>
    public class TimeSpanHFormat : IFormatProvider, ICustomFormatter
    {
        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter))
                return this;
            else
                return null;
        }

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (!(arg is TimeSpan timeSpan))
            {
                return HandleOtherFormats(format, arg);
            }

            if (!format.Contains('H'))
            {
                return HandleOtherFormats(format, arg);
            }

            var reader = new StringReader(format);
            var builder = new StringBuilder(format.Length);
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
                        builder.Append(timeSpan.ToString(format.Substring(regularFormatCharStart, position - regularFormatCharStart)));
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
                        var formatString = "";
                        for (var i = 0; i < position - bigHStart; i++)
                        {
                            formatString += "0";
                        }

                        builder.Append(((int)timeSpan.TotalHours).ToString(formatString));
                        bigHStart = -1;
                    }
                }
            } while (reader.Peek() != -1);

            position++;
            if (regularFormatCharStart != -1)
            {
                builder.Append(timeSpan.ToString(format.Substring(regularFormatCharStart, position - regularFormatCharStart)));
            }
            else if (bigHStart != -1)
            {
                var formatString = "";
                for (var i = 0; i < position - bigHStart; i++)
                {
                    formatString += "0";
                }

                builder.Append(((int)timeSpan.TotalHours).ToString(formatString));
            }

            return builder.ToString();
        }

        private string HandleOtherFormats(string format, object arg)
        {
            if (arg is IFormattable)
                return ((IFormattable)arg).ToString(format, CultureInfo.CurrentCulture);
            else if (arg != null)
                return arg.ToString();
            else
                return "";
        }
    }
}