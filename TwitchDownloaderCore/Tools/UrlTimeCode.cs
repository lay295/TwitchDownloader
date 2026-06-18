using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TwitchDownloaderCore.Tools
{
    public static class UrlTimeCode
    {
        /// <summary>
        /// Converts the span representation of a time interval in the format of '2d21h11m9s' to its <see cref="TimeSpan"/> equivalent.
        /// </summary>
        /// <param name="input">A span containing the characters that represent the time interval to convert.</param>
        /// <returns>The <see cref="TimeSpan"/> equivalent to the time interval contained in the <paramref name="input"/> span.</returns>
        public static TimeSpan Parse(ReadOnlySpan<char> input)
        {
            input = input.Trim();

            var returnTimespan = TimeSpan.Zero;
            var units = new List<char>("dhms");
            while (true)
            {
                var unitIndex = input.IndexOfAny(CollectionsMarshal.AsSpan(units));
                if (unitIndex < 1)
                {
                    return input.IsEmpty
                        ? returnTimespan // Successful parse
                        : TimeSpan.Zero; // Invalid format
                }

                if (!uint.TryParse(input[..unitIndex], out var time))
                {
                    // Invalid format
                    return TimeSpan.Zero;
                }

                var unit = input[unitIndex];
                returnTimespan += unit switch
                {
                    'd' => TimeSpan.FromDays(time),
                    'h' => TimeSpan.FromHours(time),
                    'm' => TimeSpan.FromMinutes(time),
                    's' => TimeSpan.FromSeconds(time),
                    _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
                };

                // Only parse the unit once
                units.Remove(unit);

                input = input[(unitIndex + 1)..];
            }
        }
    }
}