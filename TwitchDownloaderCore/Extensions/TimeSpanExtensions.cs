using System;

namespace TwitchDownloaderCore.Extensions
{
    public static class TimeSpanExtensions
    {
        /// <summary>
        /// Converts the span representation of a time interval in the format of '2d21h11m9s' to its <see cref="TimeSpan"/> equivalent.
        /// </summary>
        /// <param name="input">A span containing the characters that represent the time interval to convert.</param>
        /// <returns>The <see cref="TimeSpan"/> equivalent to the time interval contained in the <paramref name="input"/> span.</returns>
        public static TimeSpan ParseTimeCode(ReadOnlySpan<char> input)
        {
            var dayIndex = input.IndexOf('d');
            var hourIndex = input.IndexOf('h');
            var minuteIndex = input.IndexOf('m');
            var secondIndex = input.IndexOf('s');
            var returnTimespan = TimeSpan.Zero;

            if (dayIndex != -1 && int.TryParse(input[..dayIndex], out var days))
            {
                returnTimespan = returnTimespan.Add(TimeSpan.FromDays(days));
            }

            dayIndex++;

            if (hourIndex != -1 && int.TryParse(input[dayIndex..hourIndex], out var hours))
            {
                returnTimespan = returnTimespan.Add(TimeSpan.FromHours(hours));
            }

            hourIndex++;

            if (minuteIndex != -1 && int.TryParse(input[hourIndex..minuteIndex], out var minutes))
            {
                returnTimespan = returnTimespan.Add(TimeSpan.FromMinutes(minutes));
            }

            minuteIndex++;

            if (secondIndex != -1 && int.TryParse(input[minuteIndex..secondIndex], out var seconds))
            {
                returnTimespan = returnTimespan.Add(TimeSpan.FromSeconds(seconds));
            }

            return returnTimespan;
        }
    }
}