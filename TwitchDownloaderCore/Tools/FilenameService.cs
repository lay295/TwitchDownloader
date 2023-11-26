using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderCore.Tools
{
    public static class FilenameService
    {
        public static string GetFilename(string template, string title, string id, DateTime date, string channel, TimeSpan cropStart, TimeSpan cropEnd, string viewCount, string game)
        {
            var videoLength = cropEnd - cropStart;

            var stringBuilder = new StringBuilder(template)
                .Replace("{title}", RemoveInvalidFilenameChars(title))
                .Replace("{id}", id)
                .Replace("{channel}", RemoveInvalidFilenameChars(channel))
                .Replace("{date}", date.ToString("M-d-yy"))
                .Replace("{random_string}", Path.GetRandomFileName().Remove(8)) // Remove the period
                .Replace("{crop_start}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", cropStart))
                .Replace("{crop_end}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", cropEnd))
                .Replace("{length}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", videoLength))
                .Replace("{views}", viewCount)
                .Replace("{game}", RemoveInvalidFilenameChars(game));

            if (template.Contains("{date_custom="))
            {
                var dateRegex = new Regex("{date_custom=\"(.*?)\"}");
                ReplaceCustomWithFormattable(stringBuilder, dateRegex, date);
            }

            if (template.Contains("{crop_start_custom="))
            {
                var cropStartRegex = new Regex("{crop_start_custom=\"(.*?)\"}");
                ReplaceCustomWithFormattable(stringBuilder, cropStartRegex, cropStart);
            }

            if (template.Contains("{crop_end_custom="))
            {
                var cropEndRegex = new Regex("{crop_end_custom=\"(.*?)\"}");
                ReplaceCustomWithFormattable(stringBuilder, cropEndRegex, cropEnd);
            }

            if (template.Contains("{length_custom="))
            {
                var lengthRegex = new Regex("{length_custom=\"(.*?)\"}");
                ReplaceCustomWithFormattable(stringBuilder, lengthRegex, videoLength);
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
                // However we need .NET7+ for span support in the regex matcher.
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
    }
}