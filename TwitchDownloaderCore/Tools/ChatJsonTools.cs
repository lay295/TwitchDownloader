using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TwitchDownloaderCore.Tools
{
    public class ChatJsonTools
    {
        public static async Task<ChatRoot> ParseJsonAsync(string inputJson, CancellationToken cancellationToken = new())
        {
            ChatRoot chatRoot = new ChatRoot();

            using FileStream fs = new FileStream(inputJson, FileMode.Open, FileAccess.Read);
            using var jsonDocument = await JsonDocument.ParseAsync(fs, cancellationToken: cancellationToken);

            if (jsonDocument.RootElement.TryGetProperty("streamer", out JsonElement streamerJson))
            {
                chatRoot.streamer = streamerJson.Deserialize<Streamer>();
            }

            if (jsonDocument.RootElement.TryGetProperty("video", out JsonElement videoJson))
            {
                if (videoJson.TryGetProperty("start", out JsonElement videoStartJson) && videoJson.TryGetProperty("end", out JsonElement videoEndJson))
                {
                    chatRoot.video = videoJson.Deserialize<VideoTime>();
                }
            }

            if (jsonDocument.RootElement.TryGetProperty("embeddedData", out JsonElement embedDataJson))
            {
                chatRoot.embeddedData = embedDataJson.Deserialize<EmbeddedData>();
            }
            else if (jsonDocument.RootElement.TryGetProperty("emotes", out JsonElement emotesJson))
            {
                chatRoot.embeddedData = emotesJson.Deserialize<EmbeddedData>();
            }

            if (jsonDocument.RootElement.TryGetProperty("comments", out JsonElement commentsJson))
            {
                chatRoot.comments = commentsJson.Deserialize<List<Comment>>();
            }

            return chatRoot;
        }

        public static async Task<ChatRoot> ParseJsonInfoAsync(string inputJson, CancellationToken cancellationToken = new())
        {
            ChatRoot chatRoot = new ChatRoot();

            using FileStream fs = new FileStream(inputJson, FileMode.Open, FileAccess.Read);
            using var jsonDocument = await JsonDocument.ParseAsync(fs, cancellationToken: cancellationToken);

            if (jsonDocument.RootElement.TryGetProperty("streamer", out JsonElement streamerJson))
            {
                chatRoot.streamer = streamerJson.Deserialize<Streamer>();
            }

            if (jsonDocument.RootElement.TryGetProperty("video", out JsonElement videoJson))
            {
                if (videoJson.TryGetProperty("start", out _) && videoJson.TryGetProperty("end", out _))
                {
                    chatRoot.video = videoJson.Deserialize<VideoTime>();
                }
            }

            return chatRoot;
        }
    }
}
