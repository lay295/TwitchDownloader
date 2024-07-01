using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TwitchDownloaderCLI.Models;

[DebuggerDisplay("{_timeSpan}")]
public readonly record struct TimeDuration {

    private readonly TimeSpan _timeSpan;

    /// <summary>
    ///     Constructor used by CommandLineParser
    /// </summary>
    public TimeDuration(string str) => this = Parse(str);

    public TimeDuration(TimeSpan timeSpan) => this._timeSpan = timeSpan;

    public TimeDuration(long ticks) => this._timeSpan = TimeSpan.FromTicks(ticks);
    public static TimeDuration MinusOneSeconds { get; } = new(-1 * TimeSpan.TicksPerSecond);

    public static TimeDuration Parse(string str) {
        if (string.IsNullOrWhiteSpace(str))
            throw new FormatException();

        if (str.Contains(':')) {
            var timeSpan = TimeSpan.Parse(str);
            return new(timeSpan);
        }

        var multiplier = GetMultiplier(str, out var span);
        if (!decimal.TryParse(span, NumberStyles.Number, null, out var result))
            throw new FormatException();

        var ticks = (long)(result * multiplier);
        return new(ticks);

    }

    private static long GetMultiplier(string input, out ReadOnlySpan<char> trimmedInput) {
        if (char.IsDigit(input[^1])) {
            trimmedInput = input.AsSpan();
            return TimeSpan.TicksPerSecond;
        }

        if (Regex.IsMatch(input, @"\dms$", RegexOptions.RightToLeft)) {
            trimmedInput = input.AsSpan()[..^2];
            return TimeSpan.TicksPerMillisecond;
        }

        if (Regex.IsMatch(input, @"\ds$", RegexOptions.RightToLeft)) {
            trimmedInput = input.AsSpan()[..^1];
            return TimeSpan.TicksPerSecond;
        }

        if (Regex.IsMatch(input, @"\dm$", RegexOptions.RightToLeft)) {
            trimmedInput = input.AsSpan()[..^1];
            return TimeSpan.TicksPerMinute;
        }

        if (!Regex.IsMatch(input, @"\dh$", RegexOptions.RightToLeft))
            throw new FormatException();

        trimmedInput = input.AsSpan()[..^1];
        return TimeSpan.TicksPerHour;

    }

    public static implicit operator TimeSpan(TimeDuration timeDuration) => timeDuration._timeSpan;
}
