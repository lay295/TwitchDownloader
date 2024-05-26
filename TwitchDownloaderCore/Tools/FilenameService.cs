using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tools
{
    public static partial class FilenameService
    {
        [GeneratedRegex("{date_custom=\"(.*?)\"}")]
        private static partial Regex DateCustomRegex();

        [GeneratedRegex("{trim_start_custom=\"(.*?)\"}")]
        private static partial Regex TrimStartCustomRegex();

        [GeneratedRegex("{trim_end_custom=\"(.*?)\"}")]
        private static partial Regex TrimEndCustomRegex();

        [GeneratedRegex("{length_custom=\"(.*?)\"}")]
        private static partial Regex LengthCustomRegex();

        public static string GetFilename(string template, string title, string id, DateTime date, string channel, TimeSpan trimStart, TimeSpan trimEnd, string viewCount, string game)
        {
            var videoLength = trimEnd - trimStart;

            var stringBuilder = new StringBuilder(template)
                .Replace("{title}", RemoveInvalidFilenameChars(title))
                .Replace("{id}", id)
                .Replace("{channel}", RemoveInvalidFilenameChars(channel))
                .Replace("{date}", date.ToString("M-d-yy"))
                .Replace("{random_string}", Path.GetRandomFileName().Remove(8)) // Remove the period
                .Replace("{trim_start}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", trimStart))
                .Replace("{trim_end}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", trimEnd))
                .Replace("{length}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", videoLength))
                .Replace("{views}", viewCount)
                .Replace("{game}", RemoveInvalidFilenameChars(game));

            if (template.Contains("{date_custom="))
            {
                ReplaceCustomWithFormattable(stringBuilder, DateCustomRegex(), date);
            }

            if (template.Contains("{trim_start_custom="))
            {
                ReplaceCustomWithFormattable(stringBuilder, TrimStartCustomRegex(), trimStart);
            }

            if (template.Contains("{trim_end_custom="))
            {
                ReplaceCustomWithFormattable(stringBuilder, TrimEndCustomRegex(), trimEnd);
            }

            if (template.Contains("{length_custom="))
            {
                ReplaceCustomWithFormattable(stringBuilder, LengthCustomRegex(), videoLength);
            }

            var fileName = stringBuilder.ToString();
            var additionalSubfolders = GetTemplateSubfolders(ref fileName);
            return Path.Combine(Path.Combine(additionalSubfolders), RemoveInvalidFilenameChars(fileName));
        }

        private static void ReplaceCustomWithFormattable(StringBuilder sb, Regex regex, IFormattable formattable, IFormatProvider formatProvider = null)
        {
            do
            {
                // There's probably a better way to do this that doesn't require calling ToString()
                var match = regex.Match(sb.ToString());
                if (!match.Success)
                    break;

                var formatString = match.Groups[1].Value;
                sb.Remove(match.Groups[0].Index, match.Groups[0].Length);
                sb.Insert(match.Groups[0].Index, RemoveInvalidFilenameChars(formattable.ToString(formatString, formatProvider)));
            } while (true);
        }

        private static string[] GetTemplateSubfolders(ref string fullPath)
        {
            var returnString = fullPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            fullPath = returnString[^1];
            Array.Resize(ref returnString, returnString.Length - 1);

            for (var i = 0; i < returnString.Length; i++)
            {
                returnString[i] = RemoveInvalidFilenameChars(returnString[i]);
            }

            return returnString;
        }

        private static readonly char[] FilenameInvalidChars = Path.GetInvalidFileNameChars();

        private static string RemoveInvalidFilenameChars(string filename) => filename.ReplaceAny(FilenameInvalidChars, '_');

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