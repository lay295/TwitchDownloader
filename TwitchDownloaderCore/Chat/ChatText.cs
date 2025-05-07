using System;
using System.IO;
using System.Threading.Tasks;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Chat
{
    public static class ChatText
    {
        /// <summary>
        /// Serializes a chat plain text file.
        /// </summary>
        public static async Task SerializeAsync(Stream outputStream, ChatRoot chatRoot, TimestampFormat timeFormat)
        {
            await using var sw = new StreamWriter(outputStream, leaveOpen: true);
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
                    if (time.Ticks < 24 * TimeSpan.TicksPerHour)
                    {
                        await sw.WriteLineAsync(@$"[{time:h\:mm\:ss}] {username}: {message}");
                    }
                    else
                    {
                        await sw.WriteLineAsync(string.Create(TimeSpanHFormat.ReusableInstance, @$"[{time:H\:mm\:ss}] {username}: {message}"));
                    }
                }
                else if (timeFormat == TimestampFormat.None)
                {
                    await sw.WriteLineAsync($"{username}: {message}");
                }
            }
        }
    }
}
