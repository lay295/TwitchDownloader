using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.TwitchObjects;
using Newtonsoft = Newtonsoft.Json;

namespace TwitchDownloaderCore.Tools
{
    public class ChatJson
    {
        public string FilePath { get; set; }

        public ChatJson() { }

        /// <summary>
        /// Deserializes a chat json file.
        /// </summary>
        /// <returns>A <see cref="ChatRoot"/> representation the deserialized chat json file.</returns>
        public async Task<ChatRoot> DeserializeAsync(bool getComments = true, bool getEmbeds = true, CancellationToken cancellationToken = new())
            => await DeserializeAsync(FilePath, getComments, getEmbeds, cancellationToken);

        /// <summary>
        /// Deserializes a chat json file.
        /// </summary>
        /// <returns>A <see cref="ChatRoot"/> representation the deserialized chat json file.</returns>
        public static async Task<ChatRoot> DeserializeAsync(string filePath, bool getComments = true, bool getEmbeds = true, CancellationToken cancellationToken = new())
        {
            if (filePath is null)
                throw new ArgumentNullException(nameof(filePath));

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
        /// Serializes a chat json file.
        /// </summary>
        public void Serialize(ChatRoot chatRoot)
            => Serialize(FilePath, chatRoot);

        /// <summary>
        /// Serializes a chat json file.
        /// </summary>
        public static void Serialize(string filePath, ChatRoot chatRoot)
        {
            if (chatRoot is null)
                throw new ArgumentNullException(nameof(chatRoot));

            using TextWriter writer = File.CreateText(filePath);
            Newtonsoft::JsonSerializer serializer = new();
            serializer.Serialize(writer, chatRoot);
        }
    }
}
