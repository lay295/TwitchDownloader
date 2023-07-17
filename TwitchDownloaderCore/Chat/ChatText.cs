using System;
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

            await using var sw = new StreamWriter(filePath);
            foreach (var comment in chatRoot.comments)
            {
                string username = comment.commenter.display_name;
                string message = comment.message.body;
                if (timeFormat == TimestampFormat.Utc)
                {
                    var timestamp = comment.created_at.ToString("yyyy'-'MM'-'dd HH':'mm':'ss 'UTC'");;
                    await sw.WriteLineAsync($"[{timestamp}] {username}: {message}");
                }
                else if (timeFormat == TimestampFormat.UtcMilliseconds)
                {
                    var timestamp = comment.created_at.ToString("yyyy'-'MM'-'dd HH':'mm':'ss.fff 'UTC'");;
                    await sw.WriteLineAsync($"[{timestamp}] {username}: {message}");
                }
                else if (timeFormat == TimestampFormat.Relative)
                {
                    var time = TimeSpan.FromSeconds(comment.content_offset_seconds);
                    await sw.WriteLineAsync(string.Format(new TimeSpanHFormat(), @"[{0:H\:mm\:ss}] {1}: {2}", time, username, message));
                }
                else if (timeFormat == TimestampFormat.None)
                {
                    await sw.WriteLineAsync($"{username}: {message}");
                }
            }

            await sw.FlushAsync();
            sw.Close();
        }
    }
}
