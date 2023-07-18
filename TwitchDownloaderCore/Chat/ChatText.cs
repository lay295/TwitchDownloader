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
                var username = comment.commenter.display_name;
                var message = comment.message.body;
                if (timeFormat == TimestampFormat.Utc)
                {
                    var time = comment.created_at;
                    await sw.WriteLineAsync($"[{time:yyyy'-'MM'-'dd HH':'mm':'ss 'UTC'}] {username}: {message}");
                }
                else if (timeFormat == TimestampFormat.UtcFull)
                {
                    var time = comment.created_at;
                    await sw.WriteLineAsync($"[{time:yyyy'-'MM'-'dd HH':'mm':'ss.fff 'UTC'}] {username}: {message}");
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
