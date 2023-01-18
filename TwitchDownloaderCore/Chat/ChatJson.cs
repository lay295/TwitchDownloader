using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Chat
{
    public static class ChatJson
    {
        /// <summary>
        /// Asynchronously deserializes a chat json file.
        /// </summary>
        /// <returns>A <see cref="ChatRoot"/> representation the deserialized chat json file.</returns>
        public static async Task<ChatRoot> DeserializeAsync(string filePath, bool getComments = true, bool getEmbeds = true, CancellationToken cancellationToken = new())
        {
            ArgumentNullException.ThrowIfNull(filePath, nameof(filePath));

            if (!File.Exists(filePath))
                throw new IOException("Json file does not exist");

            ChatRoot returnChatRoot = new ChatRoot();
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var jsonDocument = await JsonDocument.ParseAsync(fs, cancellationToken: cancellationToken);

            if (jsonDocument.RootElement.TryGetProperty("streamer", out JsonElement streamerElement))
            {
                returnChatRoot.streamer = streamerElement.Deserialize<Streamer>();
            }

            if (jsonDocument.RootElement.TryGetProperty("video", out JsonElement videoElement))
            {
                returnChatRoot.video = videoElement.Deserialize<Video>();
            }

            if (getComments)
            {
                if (jsonDocument.RootElement.TryGetProperty("comments", out JsonElement commentsElement))
                {
                    returnChatRoot.comments = commentsElement.Deserialize<List<Comment>>();
                }
            }

            if (getEmbeds)
            {
                if (jsonDocument.RootElement.TryGetProperty("embeddedData", out JsonElement embeddedDataElement))
                {
                    returnChatRoot.embeddedData = embeddedDataElement.Deserialize<EmbeddedData>();
                }
                else if (jsonDocument.RootElement.TryGetProperty("emotes", out JsonElement emotesElement))
                {
                    returnChatRoot.embeddedData = emotesElement.Deserialize<EmbeddedData>();
                }
            }

            return returnChatRoot;
        }

        /// <summary>
        /// Asynchronously serializes a chat json file.
        /// </summary>
        public static async Task SerializeAsync(string filePath, ChatRoot chatRoot, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(chatRoot, nameof(chatRoot));

            using var fs = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fs, chatRoot, new JsonSerializerOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }, cancellationToken);
        }
    }
}
