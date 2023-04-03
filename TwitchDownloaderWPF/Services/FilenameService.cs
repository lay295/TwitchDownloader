using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderWPF.Services
{
    public static class FilenameService
    {
        private static string[] GetTemplateSubfolders(ref string fullPath)
        {
            string[] returnString = fullPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            fullPath = returnString[^1];
            Array.Resize(ref returnString, returnString.Length - 1);
            return returnString;
        }

        internal static string GetFilename(string template, string title, string id, DateTime date, string channel, TimeSpan cropStart, TimeSpan cropEnd)
        {
            var stringBuilder = new StringBuilder(template)
                .Replace("{title}", title)
                .Replace("{id}", id)
                .Replace("{channel}", channel)
                .Replace("{date}", date.ToString("Mdyy"))
                .Replace("{random_string}", Path.GetFileNameWithoutExtension(Path.GetRandomFileName()))
                .Replace("{crop_start}", string.Format(new TimeSpanHFormat(), @"{0:HH\-mm\-ss}", cropStart))
                .Replace("{crop_end}", string.Format(new TimeSpanHFormat(), @"{0:HH\-mm\-ss}", cropEnd));

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
                        stringBuilder.Insert(dateMatch.Groups[0].Index, date.ToString(formatString));
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
                        stringBuilder.Insert(cropStartMatch.Groups[0].Index, cropStart.ToString(formatString));
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
                        stringBuilder.Insert(cropEndMatch.Groups[0].Index, cropEnd.ToString(formatString));
                    }
                    else
                    {
                        cropEndDone = true;
                    }
                }
            }

            string fileName = stringBuilder.ToString();
            string[] additionalSubfolders = GetTemplateSubfolders(ref fileName);
            return Path.Combine(Path.Combine(additionalSubfolders), RemoveInvalidFilenameChars(fileName));
        }

        private static string RemoveInvalidFilenameChars(string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }
    }
}