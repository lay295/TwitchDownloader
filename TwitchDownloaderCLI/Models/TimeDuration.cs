using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TwitchDownloaderCLI.Models
{
    [DebuggerDisplay("{_timeSpan}")]
    public readonly record struct TimeDuration
    {
        public static TimeDuration MinusOneSeconds { get; } = new(-1 * TimeSpan.TicksPerSecond);

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

            str = str.Trim();

            if (str.Contains(':'))
            {
                var timeSpan = ParseTimeSpan(str);
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

        private static TimeSpan ParseTimeSpan(string str)
        {
            // TimeSpan.Parse interprets '36:01:02' as 36 days, 1 hour, and 2 minutes, so we need to manually parse it ourselves
            var match = Regex.Match(str, @"^(?:(\d{1,})[.:])?(\d{2,}):(\d{1,2}):(\d{1,2})(?:\.(\d{1,3})\d*)?$");
            if (match.Success)
            {
                if (!int.TryParse(match.Groups[1].ValueSpan, out var days)) days = 0;
                if (!int.TryParse(match.Groups[2].ValueSpan, out var hours)) hours = 0;
                if (!int.TryParse(match.Groups[3].ValueSpan, out var minutes)) minutes = 0;
                if (!int.TryParse(match.Groups[4].ValueSpan, out var seconds)) seconds = 0;
                if (!int.TryParse(match.Groups[5].Value.PadRight(3, '0'), out var milliseconds)) milliseconds = 0;

                return new TimeSpan(days, hours, minutes, seconds, milliseconds);
            }

            return TimeSpan.Parse(str); // Parse formats not covered by the regex
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