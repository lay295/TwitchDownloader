using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace TwitchDownloaderCore.Tools
{
    public static class FilenameService
    {
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

        public static string GetFilename(string template, string title, string id, DateTime date, string channel, TimeSpan cropStart, TimeSpan cropEnd, string viewCount, string game)
        {
            var videoLength = cropEnd - cropStart;

            var stringBuilder = new StringBuilder(template)
                .Replace("{title}", RemoveInvalidFilenameChars(title))
                .Replace("{id}", id)
                .Replace("{channel}", RemoveInvalidFilenameChars(channel))
                .Replace("{date}", date.ToString("Mdyy"))
                .Replace("{random_string}", Path.GetRandomFileName().Replace(".", ""))
                .Replace("{crop_start}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", cropStart))
                .Replace("{crop_end}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", cropEnd))
                .Replace("{length}", TimeSpanHFormat.ReusableInstance.Format(@"HH\-mm\-ss", videoLength))
                .Replace("{views}", viewCount)
                .Replace("{game}", game);

            if (template.Contains("{date_custom="))
            {
                var dateRegex = new Regex("{date_custom=\"(.*)\"}");
                var dateDone = false;
                while (!dateDone)
                {
                    var dateMatch = dateRegex.Match(stringBuilder.ToString());
                    if (dateMatch.Success)
                    {
                        var formatString = dateMatch.Groups[1].Value;
                        stringBuilder.Remove(dateMatch.Groups[0].Index, dateMatch.Groups[0].Length);
                        stringBuilder.Insert(dateMatch.Groups[0].Index, RemoveInvalidFilenameChars(date.ToString(formatString)));
                    }
                    else
                    {
                        dateDone = true;
                    }
                }
            }

            if (template.Contains("{crop_start_custom="))
            {
                var cropStartRegex = new Regex("{crop_start_custom=\"(.*)\"}");
                var cropStartDone = false;
                while (!cropStartDone)
                {
                    var cropStartMatch = cropStartRegex.Match(stringBuilder.ToString());
                    if (cropStartMatch.Success)
                    {
                        var formatString = cropStartMatch.Groups[1].Value;
                        stringBuilder.Remove(cropStartMatch.Groups[0].Index, cropStartMatch.Groups[0].Length);
                        stringBuilder.Insert(cropStartMatch.Groups[0].Index, RemoveInvalidFilenameChars(cropStart.ToString(formatString)));
                    }
                    else
                    {
                        cropStartDone = true;
                    }
                }
            }

            if (template.Contains("{crop_end_custom="))
            {
                var cropEndRegex = new Regex("{crop_end_custom=\"(.*)\"}");
                var cropEndDone = false;
                while (!cropEndDone)
                {
                    var cropEndMatch = cropEndRegex.Match(stringBuilder.ToString());
                    if (cropEndMatch.Success)
                    {
                        var formatString = cropEndMatch.Groups[1].Value;
                        stringBuilder.Remove(cropEndMatch.Groups[0].Index, cropEndMatch.Groups[0].Length);
                        stringBuilder.Insert(cropEndMatch.Groups[0].Index, RemoveInvalidFilenameChars(cropEnd.ToString(formatString)));
                    }
                    else
                    {
                        cropEndDone = true;
                    }
                }
            }

            if (template.Contains("{length_custom="))
            {
                var lengthRegex = new Regex("{length_custom=\"(.*)\"}");
                var lengthDone = false;
                while (!lengthDone)
                {
                    var lengthMatch = lengthRegex.Match(stringBuilder.ToString());
                    if (lengthMatch.Success)
                    {
                        var formatString = lengthMatch.Groups[1].Value;
                        stringBuilder.Remove(lengthMatch.Groups[0].Index, lengthMatch.Groups[0].Length);
                        stringBuilder.Insert(lengthMatch.Groups[0].Index, RemoveInvalidFilenameChars(videoLength.ToString(formatString)));
                    }
                    else
                    {
                        lengthDone = true;
                    }
                }
            }

            var fileName = stringBuilder.ToString();
            var additionalSubfolders = GetTemplateSubfolders(ref fileName);
            return Path.Combine(Path.Combine(additionalSubfolders), RemoveInvalidFilenameChars(fileName));
        }

        private static string RemoveInvalidFilenameChars(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return filename;
            }

            if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) == -1)
            {
                return filename;
            }

            return string.Join('_', filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}