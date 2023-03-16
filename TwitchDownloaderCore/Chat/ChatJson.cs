using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Chat
{
    public static class ChatJson
    {
        private static JsonSerializerOptions _jsonSerializerOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
#if DEBUG // To help debug your work
            WriteIndented = true
#endif
        };

        /// <summary>
        /// Asynchronously deserializes a chat json file.
        /// </summary>
        /// <returns>A <see cref="ChatRoot"/> representation the deserialized chat json file.</returns>
        public static async Task<ChatRoot> DeserializeAsync(string filePath, bool getComments = true, bool getEmbeds = true, CancellationToken cancellationToken = new())
        {
            ArgumentNullException.ThrowIfNull(filePath, nameof(filePath));

            if (!File.Exists(filePath))
                throw new IOException("Json file does not exist");

            ChatRoot returnChatRoot = new();

            JsonDocument jsonDocument;
            JsonDocumentOptions deserializationOptions = new()
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            switch (Path.GetExtension(filePath).ToLower())
            {
                case ".gz":
                    await using (var gs = new GZipStream(fs, CompressionMode.Decompress))
                    {
                        jsonDocument = await JsonDocument.ParseAsync(gs, deserializationOptions, cancellationToken);
                    }
                    break;
                case ".json":
                    jsonDocument = await JsonDocument.ParseAsync(fs, deserializationOptions, cancellationToken);
                    break;
                default:
                    throw new NotImplementedException(Path.GetFileName(filePath) + " is not a valid chat format");
            }

            if (jsonDocument.RootElement.TryGetProperty("FileInfo", out JsonElement fileInfoElement))
            {
                returnChatRoot.FileInfo = fileInfoElement.Deserialize<ChatRootInfo>();
            }

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

                    if (returnChatRoot.FileInfo.Version >= ChatRootVersion.CurrentVersion)
                    {
                        returnChatRoot.embeddedData = embeddedDataElement.Deserialize<EmbeddedData>();
                    }
                    else
                    {
                        var legacyEmbeddedData = embeddedDataElement.Deserialize<LegacyEmbeddedData>();
                        returnChatRoot.embeddedData = new()
                        {
                            firstParty = legacyEmbeddedData.firstParty,
                            thirdParty = legacyEmbeddedData.thirdParty,
                            twitchBadges = legacyEmbeddedData.twitchBadges.Select(item => new EmbedChatBadge
                            {
                                name = item.name,
                                versions = item.versions.Select(x => new KeyValuePair<string, ChatBadgeByteData>(x.Key, new ChatBadgeByteData { bytes = x.Value })).ToDictionary(k => k.Key, v => v.Value),
                                urls = item.urls?.Select(x=>new KeyValuePair<string, EmbedChatBadgeData>(x.Key, new EmbedChatBadgeData {url= x.Value})).ToDictionary(k =>k.Key, v=>v.Value),
                            }).ToList(),
                            twitchBits = legacyEmbeddedData.twitchBits
                        };
                    }
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
        public static async Task SerializeAsync(string filePath, ChatRoot chatRoot, ChatCompression compression, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(chatRoot, nameof(chatRoot));

            var outputDirectory = Directory.GetParent(Path.GetFullPath(filePath))!;
            if (!outputDirectory.Exists)
            {
                TwitchHelper.CreateDirectory(outputDirectory.FullName);
            }

            await using var fs = File.Create(filePath);
            switch (compression)
            {
                case ChatCompression.None:
                    await JsonSerializer.SerializeAsync(fs, chatRoot, _jsonSerializerOptions, cancellationToken);
                    break;
                case ChatCompression.Gzip:
                    await using (var gs = new GZipStream(fs, CompressionLevel.SmallestSize))
                    {
                        await JsonSerializer.SerializeAsync(gs, chatRoot, _jsonSerializerOptions, cancellationToken);
                    }
                    break;
                default:
                    throw new NotImplementedException("The requested chat format is not implemented");
            }
        }
    }
}
