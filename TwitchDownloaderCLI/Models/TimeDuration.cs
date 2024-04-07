using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TwitchDownloaderCLI.Models
{
    [DebuggerDisplay("{_timeSpan}")]
    public readonly record struct TimeDuration
    {
        private readonly TimeSpan _timeSpan;

        /// <summary>
        /// Constructor used by CommandLineParser
        /// </summary>
        public TimeDuration(string str)
        {
            this = Parse(str);
        }

        public TimeDuration(TimeSpan timeSpan)
        {
            _timeSpan = timeSpan;
        }

        public TimeDuration(long ticks)
        {
            _timeSpan = TimeSpan.FromTicks(ticks);
        }

        public static TimeDuration Parse(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                throw new FormatException();
            }

            if (str.Contains(':'))
            {
                var timeSpan = TimeSpan.Parse(str);
                return new TimeDuration(timeSpan);
            }

            var multiplier = GetMultiplier(str, out var span);
            if (decimal.TryParse(span, NumberStyles.Number, null, out var result))
            {
                var ticks = (long)(result * multiplier);
                return new TimeDuration(ticks);
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

        public static implicit operator TimeSpan(TimeDuration timeDuration) => timeDuration._timeSpan;
    }
}