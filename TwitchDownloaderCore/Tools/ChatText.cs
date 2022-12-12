using System;
using System.IO;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tools
{
    public class ChatText
    {
        public string FilePath { get; set; }
        public ChatText() { }

        /// <summary>
        /// Serializes a chat plain text file.
        /// </summary>
        public async Task SerializeAsync(ChatRoot chatRoot, TimestampFormat timeFormat)
            => await SerializeAsync(FilePath, chatRoot, timeFormat);

        /// <summary>
        /// Serializes a chat plain text file.
        /// </summary>
        public static async Task SerializeAsync(string filePath, ChatRoot chatRoot, TimestampFormat timeFormat)
        {
            if (filePath is null)
                throw new ArgumentNullException(nameof(filePath));

            using var sw = new StreamWriter(filePath);
            foreach (var comment in chatRoot.comments)
            {
                string username = comment.commenter.display_name;
                string message = comment.message.body;
                if (timeFormat == TimestampFormat.Utc)
                {
                    string timestamp = comment.created_at.ToString("u").Replace("Z", " UTC");
                    await sw.WriteLineAsync(string.Format("[{0}] {1}: {2}", timestamp, username, message));
                }
                else if (timeFormat == TimestampFormat.Relative)
                {
                    TimeSpan time = new TimeSpan(0, 0, (int)comment.content_offset_seconds);
                    string timestamp = time.ToString(@"h\:mm\:ss");
                    await sw.WriteLineAsync(string.Format("[{0}] {1}: {2}", timestamp, username, message));
                }
                else if (timeFormat == TimestampFormat.None)
                {
                    await sw.WriteLineAsync(string.Format("{0}: {1}", username, message));
                }
            }

            await sw.FlushAsync();
            sw.Close();
        }
    }
}
