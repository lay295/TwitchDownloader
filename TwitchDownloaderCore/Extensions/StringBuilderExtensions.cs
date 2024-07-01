using System;
using System.Text;

namespace TwitchDownloaderCore.Extensions;

public static class StringBuilderExtensions {
    public static StringBuilder TrimEnd(this StringBuilder sb, ReadOnlySpan<char> trimChars) {
        var trimLength = 0;
        while (sb.Length - trimLength > 0 && trimChars.Contains(sb[^(trimLength + 1)]))
            ++trimLength;

        return sb.Remove(sb.Length - trimLength, trimLength);
    }
}
