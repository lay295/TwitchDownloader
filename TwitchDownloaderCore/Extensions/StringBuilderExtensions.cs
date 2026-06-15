using System;
using System.Text;

namespace TwitchDownloaderCore.Extensions
{
    public static class StringBuilderExtensions
    {
        extension(StringBuilder sb)
        {
            public StringBuilder TrimEnd(ReadOnlySpan<char> trimChars)
            {
                var trimLength = 0;
                while (sb.Length - trimLength > 0 && trimChars.Contains(sb[^(trimLength + 1)]))
                {
                    trimLength++;
                }

                return sb.Remove(sb.Length - trimLength, trimLength);
            }
        }
    }
}