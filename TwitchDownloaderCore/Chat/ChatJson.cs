using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Asynchronously deserializes a chat json file.
        /// </summary>
        /// <returns>A <see cref="ChatRoot"/> representation the deserialized chat json file.</returns>
        /// <exception cref="IOException">The file does not exist.</exception>
        /// <exception cref="NotSupportedException">The file is not a valid chat format.</exception>
        public static async Task<ChatRoot> DeserializeAsync(string filePath, bool getComments = true, bool onlyFirstAndLastComments = false, bool getEmbeds = true, CancellationToken cancellationToken = default)
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

            await using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                jsonDocument = await GetJsonDocumentAsync(fs, filePath, deserializationOptions, cancellationToken);
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
                    returnChatRoot.comments = onlyFirstAndLastComments
                        ? commentsElement.DeserializeFirstAndLastFromList<Comment>(options: _jsonSerializerOptions)
                        : commentsElement.Deserialize<List<Comment>>(options: _jsonSerializerOptions);
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
                            firstParty = legacyEmbeddedData?.firstParty ?? [],
                            thirdParty = legacyEmbeddedData?.thirdParty ?? [],
                            twitchBadges = legacyEmbeddedData?.twitchBadges.Select(item => new EmbedChatBadge
                            {
                                name = item.name,
                                versions = item.versions.Select(x => new KeyValuePair<string, ChatBadgeData>(x.Key, new ChatBadgeData { bytes = x.Value })).ToDictionary(k => k.Key, v => v.Value),
                            }).ToList() ?? [],
                            twitchBits = legacyEmbeddedData?.twitchBits ?? []
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

        private static async Task<JsonDocument> GetJsonDocumentAsync(Stream stream, string filePath, JsonDocumentOptions deserializationOptions, CancellationToken cancellationToken = default)
        {
            if (!stream.CanSeek)
            {
                // We aren't able to verify the file type. Pretend it's JSON.
                return await JsonDocument.ParseAsync(stream, deserializationOptions, cancellationToken);
            }

            const int RENT_LENGTH = 4;
            var rentedBuffer = ArrayPool<byte>.Shared.Rent(RENT_LENGTH);
            try
            {
                if (await stream.ReadAsync(rentedBuffer.AsMemory(0, RENT_LENGTH), cancellationToken) != RENT_LENGTH)
                {
                    throw new EndOfStreamException($"{Path.GetFileName(filePath)} is not a valid chat format.");
                }

                stream.Seek(-RENT_LENGTH, SeekOrigin.Current);

                // https://en.wikipedia.org/wiki/Byte_order_mark#Byte_order_marks_by_encoding
                switch (rentedBuffer)
                {
                    case [0x1F, 0x8B, ..]: // https://docs.fileformat.com/compression/gz/#gz-file-header
                    {
                        await using var gs = new GZipStream(stream, CompressionMode.Decompress);
                        return await GetJsonDocumentAsync(gs, filePath, deserializationOptions, cancellationToken);
                    }
                    case [0x00, 0x00, 0xFE, 0xFF]: // UTF-32 BE
                    case [0xFF, 0xFE, 0x00, 0x00]: // UTF-32 LE
                    {
                        using var sr = new StreamReader(stream, Encoding.UTF32);
                        var jsonString = await sr.ReadToEndAsync(cancellationToken);
                        return JsonDocument.Parse(jsonString.AsMemory(), deserializationOptions);
                    }
                    case [0xFE, 0xFF, ..]: // UTF-16 BE
                    case [0xFF, 0xFE, ..]: // UTF-16 LE
                    {
                        using var sr = new StreamReader(stream, Encoding.Unicode);
                        var jsonString = await sr.ReadToEndAsync(cancellationToken);
                        return JsonDocument.Parse(jsonString.AsMemory(), deserializationOptions);
                    }
                    case [0xEF, 0xBB, 0xBF, ..]: // UTF-8
                    case [(byte)'{', ..]: // Starts with a '{', probably JSON
                    {
                        return await JsonDocument.ParseAsync(stream, deserializationOptions, cancellationToken);
                    }
                    default:
                    {
                        throw new NotSupportedException($"{Path.GetFileName(filePath)} is not a valid chat format.");
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }

#pragma warning disable CS0618 // Type or member is obsolete
        private static async Task UpgradeChatJson(ChatRoot chatRoot)
        {
            const int MAX_STREAM_LENGTH = 172_800; // 48 hours in seconds. https://help.twitch.tv/s/article/broadcast-guidelines

            var firstComment = chatRoot.comments.FirstOrDefault();
            var lastComment = chatRoot.comments.LastOrDefault();

            chatRoot.video ??= new Video
            {
                start = (int)Math.Floor(firstComment?.content_offset_seconds ?? 0),
                end = (int)Math.Ceiling(lastComment?.content_offset_seconds ?? MAX_STREAM_LENGTH)
            };

            chatRoot.video.id ??= firstComment?.content_id;

            if (chatRoot.video.created_at == default)
                chatRoot.video.created_at = firstComment?.created_at - TimeSpan.FromSeconds(firstComment?.content_offset_seconds ?? 0) ?? default;

            if (chatRoot.streamer is null)
            {
                var broadcasterComment = chatRoot.comments
                    .Where(x => x.message.user_badges != null)
                    .FirstOrDefault(x => x.message.user_badges.Any(b => b._id.Equals("broadcaster")));

                if (!int.TryParse(chatRoot.video.user_id, out var assumedId))
                {
                    if (chatRoot.comments.FirstOrDefault(x => int.TryParse(x.channel_id, out assumedId)) is null)
                    {
                        if (!int.TryParse(broadcasterComment?.commenter._id, out assumedId))
                        {
                            assumedId = 0;
                        }
                    }
                }

                var assumedName = chatRoot.video.user_name ?? broadcasterComment?.commenter.display_name;
                var assumedLogin = broadcasterComment?.commenter.name;

                if ((assumedName is null || assumedLogin is null) && assumedId != 0)
                {
                    try
                    {
                        var userInfo = await TwitchHelper.GetUserInfo([assumedId.ToString()]);
                        assumedName ??= userInfo.data.users.FirstOrDefault()?.displayName;
                        assumedLogin ??= userInfo.data.users.FirstOrDefault()?.login;
                    }
                    catch { /* ignored */ }
                }

                chatRoot.streamer = new Streamer
                {
                    name = assumedName,
                    login = assumedLogin,
                    id = assumedId
                };
            }

            if (chatRoot.streamer.login is null && chatRoot.streamer.id != 0)
            {
                try
                {
                    var userInfo = await TwitchHelper.GetUserInfo([chatRoot.streamer.id.ToString()]);
                    chatRoot.streamer.login = userInfo.data.users.FirstOrDefault()?.login;
                }
                catch { /* ignored */ }
            }

            if (chatRoot.video.user_name is not null)
                chatRoot.video.user_name = null;

            if (chatRoot.video.user_id is not null)
                chatRoot.video.user_id = null;

            if (chatRoot.video.duration is not null)
            {
                chatRoot.video.length = UrlTimeCode.Parse(chatRoot.video.duration).TotalSeconds;
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

            // 300x300 avatars are overkill for chat rendering
            foreach (var comment in chatRoot.comments)
            {
                if (comment.commenter?.logo?.Contains("300x300") ?? false)
                {
                    comment.commenter.logo = comment.commenter.logo.Replace("300x300", "70x70");
                }
            }
        }
#pragma warning restore CS0618 // Type or member is obsolete

        /// <summary>
        /// Asynchronously serializes a chat json file.
        /// </summary>
        public static Task SerializeAsync(Stream outputStream, ChatRoot chatRoot, CancellationToken cancellationToken)
        {
            return JsonSerializer.SerializeAsync(outputStream, chatRoot, _jsonSerializerOptions, cancellationToken);
        }
    }
}