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
        public static async Task SerializeAsync(FileStream fileStream, ChatRoot chatRoot, TimestampFormat timeFormat)
        {
            await using var sw = new StreamWriter(fileStream);
            foreach (var comment in chatRoot.comments)
            {
                var username = comment.commenter.display_name;
                var message = comment.message.body;
                switch (timeFormat) {
                    case TimestampFormat.Utc: {
                        var time = comment.created_at;
                        await sw.WriteLineAsync($"[{time:yyyy'-'MM'-'dd HH':'mm':'ss 'UTC'}] {username}: {message}");
                        break;
                    }

                    case TimestampFormat.UtcFull: {
                        var time = comment.created_at;
                        await sw.WriteLineAsync($"[{time:yyyy'-'MM'-'dd HH':'mm':'ss.fff 'UTC'}] {username}: {message}");
                        break;
                    }

                    case TimestampFormat.Relative: {
                        var time = TimeSpan.FromSeconds(comment.content_offset_seconds);
                        if (time.Ticks < 24 * TimeSpan.TicksPerHour)
                        {
                            await sw.WriteLineAsync(@$"[{time:h\:mm\:ss}] {username}: {message}");
                        }
                        else
                        {
                            await sw.WriteLineAsync(string.Create(TimeSpanHFormat.ReusableInstance, @$"[{time:H\:mm\:ss}] {username}: {message}"));
                        }

                        break;
                    }

                    case TimestampFormat.None: await sw.WriteLineAsync($"{username}: {message}");
                        break;

                    default: throw new ArgumentOutOfRangeException(nameof(timeFormat), timeFormat, null);
                }
            }
        }
    }
}
