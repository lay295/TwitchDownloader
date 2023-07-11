using System;
using System.Runtime.CompilerServices;

namespace TwitchDownloaderCore.Extensions
{
    public static class GenericExtensions
    {
        /// <summary>
        /// Leverages <see cref="DefaultInterpolatedStringHandler"/> to generate a formatted string of an object using an <see cref="ICustomFormatter"/> for when an object's provided
        /// <see cref="Object.ToString"/> method only provides minor culture variation.
        /// </summary>
        /// <returns>A string formatted according to the <paramref name="format"/> specified.</returns>
        /// <remarks>This method only generates a formatted string without boxing, the format specification must be implemented in the <paramref name="provider"/>.</remarks>
        // TODO: Add StringSyntax attribute to 'format' when .NET 7
        public static string ToFormattedString<T>(this T value, string format, IFormatProvider provider = null)
        {
            var interpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1, provider);
            interpolatedStringHandler.AppendFormatted(value, format);
            return interpolatedStringHandler.ToStringAndClear();
        }
    }
}