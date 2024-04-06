using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TwitchDownloaderCLI.Models
{
    public readonly record struct Time
    {
        private readonly TimeSpan _timeSpan;

        /// <summary>
        /// Constructor used by CommandLineParser
        /// </summary>
        public Time(string str)
        {
            this = Parse(str);
        }

        public Time(TimeSpan timeSpan)
        {
            _timeSpan = timeSpan;
        }

        public Time(long ticks)
        {
            _timeSpan = TimeSpan.FromTicks(ticks);
        }

        public static Time Parse(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                throw new FormatException();
            }

            if (str.Contains(':'))
            {
                var timeSpan = TimeSpan.Parse(str);
                return new Time(timeSpan);
            }

            var multiplier = GetMultiplier(str, out var span);
            if (double.TryParse(span, NumberStyles.Number, null, out var result))
            {
                var ticks = checked((long)(result * multiplier));
                return new Time(ticks);
            }

            throw new FormatException();
        }

        private static long GetMultiplier(string input, out ReadOnlySpan<char> trimmedInput)
        {
            if (char.IsDigit(input[^1]))
            {
                trimmedInput = input.AsSpan();
                return TimeSpan.TicksPerSecond;
            }

            if (Regex.IsMatch(input, @"\dms$", RegexOptions.RightToLeft))
            {
                trimmedInput = input.AsSpan()[..^2];
                return TimeSpan.TicksPerMillisecond;
            }

            if (Regex.IsMatch(input, @"\ds$", RegexOptions.RightToLeft))
            {
                trimmedInput = input.AsSpan()[..^1];
                return TimeSpan.TicksPerSecond;
            }

            if (Regex.IsMatch(input, @"\dm$", RegexOptions.RightToLeft))
            {
                trimmedInput = input.AsSpan()[..^1];
                return TimeSpan.TicksPerMinute;
            }

            if (Regex.IsMatch(input, @"\dh$", RegexOptions.RightToLeft))
            {
                trimmedInput = input.AsSpan()[..^1];
                return TimeSpan.TicksPerHour;
            }

            throw new FormatException();
        }

        public static implicit operator TimeSpan(Time time) => time._timeSpan;
    }
}