using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Chat
{
    public static class ChatText
    {
        /// <summary>
        /// Serializes a chat plain text file.
        /// </summary>
        public static async Task SerializeAsync(string filePath, ChatRoot chatRoot, TimestampFormat timeFormat)
        {
            ArgumentNullException.ThrowIfNull(filePath, nameof(filePath));

            var outputDirectory = Directory.GetParent(Path.GetFullPath(filePath))!;
            if (!outputDirectory.Exists)
            {
                TwitchHelper.CreateDirectory(outputDirectory.FullName);
            }

            var utcTimestampFormat = timeFormat is TimestampFormat.Utc ? DateTimeFormatInfo.CurrentInfo.UniversalSortableDateTimePattern.Replace("Z", " UTC") : null;
            await using var sw = new StreamWriter(filePath);
            foreach (var comment in chatRoot.comments)
            {
                var username = comment.commenter.display_name;
                var message = comment.message.body;
                if (timeFormat == TimestampFormat.Utc)
                {
                    // This branch could be optimized even more but the loss of readability isn't worth it
                    var timestamp = comment.created_at.ToString(utcTimestampFormat);
                    await sw.WriteLineAsync($"[{timestamp}] {username}: {message}");
                }
                else if (timeFormat == TimestampFormat.Relative)
                {
                    var time = TimeSpan.FromSeconds(comment.content_offset_seconds);
                    await sw.WriteLineAsync(string.Create(TimeSpanHFormat.ReusableInstance, @$"[{time:H\:mm\:ss}] {username}: {message}"));
                }
                else if (timeFormat == TimestampFormat.None)
                {
                    await sw.WriteLineAsync($"{username}: {message}");
                }
            }
        }
    }
}
