using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Chat
{
    public static class ChatJson
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Asynchronously deserializes a chat json file.
        /// </summary>
        /// <returns>A <see cref="ChatRoot"/> representation the deserialized chat json file.</returns>
        /// <exception cref="IOException">The file does not exist.</exception>
        /// <exception cref="NotSupportedException">The file is not a valid chat format.</exception>
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
                AllowTrailingCommas = true
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
                    throw new NotSupportedException(Path.GetFileName(filePath) + " is not a valid chat format");
            }

            if (jsonDocument.RootElement.TryGetProperty("FileInfo", out JsonElement fileInfoElement))
            {
                returnChatRoot.FileInfo = fileInfoElement.Deserialize<ChatRootInfo>(options: _jsonSerializerOptions);
            }

            if (jsonDocument.RootElement.TryGetProperty("streamer", out JsonElement streamerElement))
            {
                returnChatRoot.streamer = streamerElement.Deserialize<Streamer>(options: _jsonSerializerOptions);
            }

            if (jsonDocument.RootElement.TryGetProperty("video", out JsonElement videoElement))
            {
                returnChatRoot.video = videoElement.Deserialize<Video>(options: _jsonSerializerOptions);
            }

            if (getComments)
            {
                if (jsonDocument.RootElement.TryGetProperty("comments", out JsonElement commentsElement))
                {
                    returnChatRoot.comments = commentsElement.Deserialize<List<Comment>>(options: _jsonSerializerOptions);
                }
            }

            if (getEmbeds)
            {
                if (jsonDocument.RootElement.TryGetProperty("embeddedData", out JsonElement embeddedDataElement))
                {
                    if (returnChatRoot.FileInfo.Version > new ChatRootVersion(1, 2, 2))
                    {
                        returnChatRoot.embeddedData = embeddedDataElement.Deserialize<EmbeddedData>(options: _jsonSerializerOptions);
                    }
                    else
                    {
                        var legacyEmbeddedData = embeddedDataElement.Deserialize<LegacyEmbeddedData>(options: _jsonSerializerOptions);
                        returnChatRoot.embeddedData = new EmbeddedData
                        {
                            firstParty = legacyEmbeddedData?.firstParty ?? new List<EmbedEmoteData>(),
                            thirdParty = legacyEmbeddedData?.thirdParty ?? new List<EmbedEmoteData>(),
                            twitchBadges = legacyEmbeddedData?.twitchBadges.Select(item => new EmbedChatBadge
                            {
                                name = item.name,
                                versions = item.versions.Select(x => new KeyValuePair<string, ChatBadgeData>(x.Key, new ChatBadgeData { bytes = x.Value })).ToDictionary(k => k.Key, v => v.Value),
                            }).ToList() ?? new List<EmbedChatBadge>(),
                            twitchBits = legacyEmbeddedData?.twitchBits ?? new List<EmbedCheerEmote>()
                        };
                    }
                }
                else if (jsonDocument.RootElement.TryGetProperty("emotes", out JsonElement emotesElement))
                {
                    returnChatRoot.embeddedData = emotesElement.Deserialize<EmbeddedData>(options: _jsonSerializerOptions);
                }
            }

            await UpgradeChatJson(returnChatRoot);

            return returnChatRoot;
        }

        private static async ValueTask UpgradeChatJson(ChatRoot chatRoot)
        {
            const int MAX_STREAM_LENGTH = 172_800; // 48 hours in seconds. https://help.twitch.tv/s/article/broadcast-guidelines
            chatRoot.video ??= new Video
            {
                start = (int)Math.Floor(chatRoot.comments.FirstOrDefault()?.content_offset_seconds ?? 0),
                end = (int)Math.Ceiling(chatRoot.comments.LastOrDefault()?.content_offset_seconds ?? MAX_STREAM_LENGTH)
            };

            if (chatRoot.streamer is null)
            {
                var assumedId = int.Parse(chatRoot.video.user_id ?? chatRoot.comments.FirstOrDefault()?.channel_id ?? "0");
                var assumedName = chatRoot.video.user_name ?? await TwitchHelper.GetStreamerName(assumedId);

                chatRoot.streamer = new Streamer { id = assumedId, name = assumedName };
            }

            if (chatRoot.video.user_name is not null)
                chatRoot.video.user_name = null;

            if (chatRoot.video.user_id is not null)
                chatRoot.video.user_id = null;

            if (chatRoot.video.duration is not null)
            {
                chatRoot.video.length = TimeSpanExtensions.ParseTimeCode(chatRoot.video.duration).TotalSeconds;
                chatRoot.video.end = chatRoot.video.length;
                chatRoot.video.duration = null;
            }

            // Fix incorrect bits_spent value on chats between v5 shutdown and the lay295#520 fix
            if (chatRoot.comments.All(c => c.message.bits_spent == 0))
            {
                foreach (var comment in chatRoot.comments)
                {
                    var bitMatch = TwitchRegex.BitsRegex.Match(comment.message.body);
                    if (bitMatch.Success && int.TryParse(bitMatch.ValueSpan, out var result))
                    {
                        comment.message.bits_spent = result;
                    }
                }
            }
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
                    throw new NotSupportedException($"{compression} is not a supported chat compression.");
            }
        }
    }
}
