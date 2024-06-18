using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tools
{
    public static class FilenameService
    {
        public static string GetFilename(string template, [AllowNull] string title, [AllowNull] string id, DateTime date, [AllowNull] string channel, TimeSpan trimStart, TimeSpan trimEnd, long viewCount, [AllowNull] string game)
        {
            var videoLength = trimEnd - trimStart;

            var stringBuilder = new StringBuilder(template)
                .Replace("{title}", ReplaceInvalidFilenameChars(title))
                .Replace("{id}", ReplaceInvalidFilenameChars(id))
                .Replace("{channel}", ReplaceInvalidFilenameChars(channel))
                .Replace("{date}", date.ToString("M-d-yy"))
                .Replace("{random_string}", Path.GetRandomFileName().Remove(8)) // Remove the period
                .Replace("{trim_start}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", trimStart))
                .Replace("{trim_end}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", trimEnd))
                .Replace("{length}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", videoLength))
                .Replace("{views}", viewCount.ToString(CultureInfo.CurrentCulture))
                .Replace("{game}", ReplaceInvalidFilenameChars(game));

            if (template.Contains("{date_custom="))
            {
                var dateRegex = new Regex("{date_custom=\"(.*?)\"}");
                ReplaceCustomWithFormattable(stringBuilder, dateRegex, date);
            }

            if (template.Contains("{trim_start_custom="))
            {
                var trimStartRegex = new Regex("{trim_start_custom=\"(.*?)\"}");
                ReplaceCustomWithFormattable(stringBuilder, trimStartRegex, trimStart);
            }

            if (template.Contains("{trim_end_custom="))
            {
                var trimEndRegex = new Regex("{trim_end_custom=\"(.*?)\"}");
                ReplaceCustomWithFormattable(stringBuilder, trimEndRegex, trimEnd);
            }

            if (template.Contains("{length_custom="))
            {
                var lengthRegex = new Regex("{length_custom=\"(.*?)\"}");
                ReplaceCustomWithFormattable(stringBuilder, lengthRegex, videoLength);
            }

            var fileName = stringBuilder.ToString();
            var additionalSubfolders = GetTemplateSubfolders(ref fileName);
            return Path.Combine(Path.Combine(additionalSubfolders), ReplaceInvalidFilenameChars(fileName));
        }

        private static void ReplaceCustomWithFormattable(StringBuilder sb, Regex regex, IFormattable formattable, IFormatProvider formatProvider = null)
        {
            do
            {
                // There's probably a better way to do this that doesn't require calling ToString()
                // However we need .NET7+ for span support in the regex matcher.
                var match = regex.Match(sb.ToString());
                if (!match.Success)
                    break;

                var formatString = match.Groups[1].Value;
                sb.Remove(match.Groups[0].Index, match.Groups[0].Length);
                sb.Insert(match.Groups[0].Index, ReplaceInvalidFilenameChars(formattable.ToString(formatString, formatProvider)));
            } while (true);
        }

        private static string[] GetTemplateSubfolders(ref string fullPath)
        {
            var returnString = fullPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            fullPath = returnString[^1];
            Array.Resize(ref returnString, returnString.Length - 1);

            for (var i = 0; i < returnString.Length; i++)
            {
                returnString[i] = ReplaceInvalidFilenameChars(returnString[i]);
            }

            return returnString;
        }

        private static readonly char[] FilenameInvalidChars = Path.GetInvalidFileNameChars();

        [return: NotNullIfNotNull(nameof(filename))]
        public static string ReplaceInvalidFilenameChars([AllowNull] string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return filename;
            }

            const string TIMESTAMP_PATTERN = /*lang=regex*/ @"(?<=\d):(?=\d\d)";
            var newName = Regex.Replace(filename, TIMESTAMP_PATTERN, "_");

            if (newName.AsSpan().IndexOfAny("\"*:<>?|/\\") != -1)
            {
                newName = string.Create(filename.Length, filename, (span, str) =>
                {
                    const int FULL_WIDTH_OFFSET = 0xFEE0; // https://en.wikipedia.org/wiki/Halfwidth_and_Fullwidth_Forms_(Unicode_block)
                    for (var i = 0; i < str.Length; i++)
                    {
                        var ch = str[i];
                        span[i] = ch switch
                        {
                            '\"' or '*' or ':' or '<' or '>' or '?' or '|' or '/' or '\\' => (char)(ch + FULL_WIDTH_OFFSET),
                            _ => ch
                        };
                    }
                });
            }

            // In case there are additional invalid chars such as control codes
            return newName.ReplaceAny(FilenameInvalidChars, '_');
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