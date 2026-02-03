using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCore.Services
{
    public static partial class FilenameService
    {
        [GeneratedRegex("""{date_custom="(.*?)"}""")]
        private static partial Regex DateCustomRegex { get; }

        [GeneratedRegex("""{trim_start_custom="(.*?)"}""")]
        private static partial Regex TrimStartCustomRegex { get; }

        [GeneratedRegex("""{trim_end_custom="(.*?)"}""")]
        private static partial Regex TrimEndCustomRegex { get; }

        [GeneratedRegex("""{trim_length_custom="(.*?)"}""")]
        private static partial Regex TrimLengthCustomRegex { get; }

        [GeneratedRegex("""{length_custom="(.*?)"}""")]
        private static partial Regex LengthCustomRegex { get; }

        public static string GetFilename(string template, [AllowNull] string title, [AllowNull] string id, DateTime date, [AllowNull] string channel, [AllowNull] string channelId, TimeSpan trimStart, TimeSpan trimEnd, TimeSpan videoLength, long viewCount,
            [AllowNull] string game, [AllowNull] string clipper = null, [AllowNull] string clipperId = null)
        {
            var trimLength = trimEnd - trimStart;

            var stringBuilder = new StringBuilder(template)
                .Replace("{title}", ReplaceInvalidFilenameChars(title))
                .Replace("{id}", ReplaceInvalidFilenameChars(id))
                .Replace("{channel}", ReplaceInvalidFilenameChars(channel))
                .Replace("{channel_id}", ReplaceInvalidFilenameChars(channelId))
                .Replace("{clipper}", ReplaceInvalidFilenameChars(clipper))
                .Replace("{clipper_id}", ReplaceInvalidFilenameChars(clipperId))
                .Replace("{date}", date.ToString("M-d-yy"))
                .Replace("{random_string}", Path.GetRandomFileName().Remove(8)) // Remove the period
                .Replace("{trim_start}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", trimStart))
                .Replace("{trim_end}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", trimEnd))
                .Replace("{trim_length}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", trimLength))
                .Replace("{length}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", videoLength))
                .Replace("{views}", viewCount.ToString(CultureInfo.CurrentCulture))
                .Replace("{game}", ReplaceInvalidFilenameChars(game));

            if (template.Contains("{date_custom="))
            {
                ReplaceCustomWithFormattable(stringBuilder, DateCustomRegex, date);
            }

            if (template.Contains("{trim_start_custom="))
            {
                ReplaceCustomWithFormattable(stringBuilder, TrimStartCustomRegex, trimStart, TimeSpanHFormat.ReusableInstance);
            }

            if (template.Contains("{trim_end_custom="))
            {
                ReplaceCustomWithFormattable(stringBuilder, TrimEndCustomRegex, trimEnd, TimeSpanHFormat.ReusableInstance);
            }

            if (template.Contains("{trim_length_custom="))
            {
                ReplaceCustomWithFormattable(stringBuilder, TrimLengthCustomRegex, trimLength, TimeSpanHFormat.ReusableInstance);
            }

            if (template.Contains("{length_custom="))
            {
                ReplaceCustomWithFormattable(stringBuilder, LengthCustomRegex, videoLength, TimeSpanHFormat.ReusableInstance);
            }

            var fileName = stringBuilder.ToString();
            var additionalSubfolders = GetTemplateSubfolders(ref fileName);
            return Path.Combine(Path.Combine(additionalSubfolders), ReplaceInvalidFilenameChars(fileName));
        }

        private static void ReplaceCustomWithFormattable<TFormattable>(StringBuilder sb, Regex regex, TFormattable formattable, [AllowNull] IFormatProvider formatProvider = null) where TFormattable : IFormattable
        {
            do
            {
                // There's probably a better way to do this that doesn't require calling ToString()
                var match = regex.Match(sb.ToString());
                if (!match.Success)
                    break;

                var formatString = match.Groups[1].Value;
                var formattedString = formatProvider?.GetFormat(typeof(ICustomFormatter)) is ICustomFormatter customFormatter
                    ? customFormatter.Format(formatString, formattable, formatProvider)
                    : formattable.ToString(formatString, formatProvider);

                sb.Remove(match.Groups[0].Index, match.Groups[0].Length);
                sb.Insert(match.Groups[0].Index, ReplaceInvalidFilenameChars(formattedString));
            } while (true);
        }

        private static string[] GetTemplateSubfolders(ref string fullPath)
        {
            var returnString = fullPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            fullPath = returnString[^1];
            Array.Resize(ref returnString, returnString.Length - 1);

            for (var i = 0; i < returnString.Length; i++)
            {
                returnString[i] = ReplaceInvalidFilenameChars(returnString[i]);
            }

            return returnString;
        }

        private static readonly char[] FilenameInvalidChars = Path.GetInvalidFileNameChars();

        [GeneratedRegex("""(?<=\d):(?=\d\d)""")]
        private static partial Regex TimestampPatternRegex { get; }

        [return: NotNullIfNotNull(nameof(filename))]
        public static string ReplaceInvalidFilenameChars([AllowNull] string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return filename;
            }

            var newName = TimestampPatternRegex.Replace(filename, "_");

            if (newName.AsSpan().IndexOfAny("""*":<>?|/\""") != -1)
            {
                newName = string.Create(filename.Length, filename, (span, str) =>
                {
                    const int FULL_WIDTH_OFFSET = 0xFEE0; // https://en.wikipedia.org/wiki/Halfwidth_and_Fullwidth_Forms_(Unicode_block)
                    for (var i = 0; i < str.Length; i++)
                    {
                        var ch = str[i];
                        span[i] = ch switch
                        {
                            '*' or '"' or ':' or '<' or '>' or '?' or '|' or '/' or '\\' => (char)(ch + FULL_WIDTH_OFFSET),
                            _ => ch
                        };
                    }
                });
            }

            // In case there are additional invalid chars such as control codes
            return newName.ReplaceAny(FilenameInvalidChars, '_');
        }

        [return: MaybeNull]
        public static string GuessVodFileExtension([AllowNull] string qualityString)
        {
            if (string.IsNullOrWhiteSpace(qualityString))
            {
                return ".mp4";
            }

            if (qualityString.Contains("audio", StringComparison.OrdinalIgnoreCase))
            {
                return ".m4a";
            }

            if (char.IsDigit(qualityString[0])
                || qualityString.Contains("source", StringComparison.OrdinalIgnoreCase)
                || qualityString.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                return ".mp4";
            }

            return null;
        }

        public static FileInfo GetNonCollidingName(FileInfo fileInfo)
        {
            fileInfo.Refresh();
            var fi = fileInfo;

            var parentDir = Path.GetDirectoryName(fi.FullName)!;
            var oldName = Path.GetFileNameWithoutExtension(fi.Name.AsSpan());
            var extension = Path.GetExtension(fi.Name.AsSpan());

            var i = 1;
            while (fi.Exists)
            {
                var newName = Path.Combine(parentDir, $"{oldName} ({i}){extension}");
                fi = new FileInfo(newName);
                i++;
            }

            return fi;
        }
    }
}