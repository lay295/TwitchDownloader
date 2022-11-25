using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace TwitchDownloaderCore.Tools
{
    public class ChatFileTools
    {
        /// <summary>
        /// Parses an input chat json file with the most up-to-date parsing
        /// </summary>
        /// <returns>The <paramref name="inputJson"/> file as a <see cref="ChatRoot"/> object</returns>
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
                chatRoot.video = videoJson.Deserialize<VideoTime>();
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

        /// <summary>
        /// Parses an input chat json file with the most up-to-date parsing
        /// </summary>
        /// <returns>The <see cref="Streamer"/> and <see cref="VideoTime"/> properties of the <paramref name="inputJson"/> </returns>
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
                chatRoot.video = videoJson.Deserialize<VideoTime>();
            }

            return chatRoot;
        }

        /// <summary>
        /// Parses an input chat json file with older properties
        /// </summary>
        /// <returns>The <paramref name="inputJson"/> file as a <see cref="ChatRoot"/> object</returns>
        public static async Task<ChatRoot> ParseLegacyJsonAsync(string inputJson, CancellationToken cancellationToken = new())
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

        public static async Task WriteJsonChatAsync(string outputFile, ChatRoot chatRoot)
        {
            using TextWriter writer = File.CreateText(outputFile);
            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(writer, chatRoot);
        }
        public static async Task WriteTextChatAsync(string outputFile, TimestampFormat timeFormat, ChatRoot chatRoot)
        {
            using StreamWriter sw = new StreamWriter(outputFile);
            foreach (var comment in chatRoot.comments)
            {
                string username = comment.commenter.display_name;
                string message = comment.message.body;
                if (timeFormat == TimestampFormat.Utc)
                {
                    string timestamp = comment.created_at.ToString("u").Replace("Z", " UTC");
                    sw.WriteLine(String.Format("[{0}] {1}: {2}", timestamp, username, message));
                }
                else if (timeFormat == TimestampFormat.Relative)
                {
                    TimeSpan time = new TimeSpan(0, 0, (int)comment.content_offset_seconds);
                    string timestamp = time.ToString(@"h\:mm\:ss");
                    sw.WriteLine(String.Format("[{0}] {1}: {2}", timestamp, username, message));
                }
                else if (timeFormat == TimestampFormat.None)
                {
                    sw.WriteLine(String.Format("{0}: {1}", username, message));
                }
            }

            sw.Flush();
            sw.Close();
        }

        // TODO: Add support for embedding Twitch bits and Twitch badges in HTML chats
        public static async Task WriteHtmlChatAsync(string outputFile, bool embedData, ChatRoot chatRoot)
        {
            Dictionary<string, EmbedEmoteData> thirdEmoteData = null;
            EmoteResponse emotes = await TwitchHelper.GetThirdPartyEmoteData(chatRoot.streamer.id.ToString(), true, true, true);
            thirdEmoteData = new Dictionary<string, EmbedEmoteData>();
            List<EmoteResponseItem> itemList = new List<EmoteResponseItem>();
            itemList.AddRange(emotes.BTTV);
            itemList.AddRange(emotes.FFZ);
            itemList.AddRange(emotes.STV);

            foreach (var item in itemList)
            {
                if (!thirdEmoteData.ContainsKey(item.Code))
                {
                    if (embedData)
                    {
                        EmbedEmoteData embedEmoteData = chatRoot.embeddedData.thirdParty.FirstOrDefault(x => x.id == item.Id);
                        if (embedEmoteData != null)
                        {
                            embedEmoteData.url = item.ImageUrl.Replace("[scale]", "1");
                            thirdEmoteData[item.Code] = embedEmoteData;
                        }
                    }
                    else
                    {
                        EmbedEmoteData embedEmoteData = new EmbedEmoteData();
                        embedEmoteData.url = item.ImageUrl.Replace("[scale]", "1");
                        thirdEmoteData[item.Code] = embedEmoteData;
                    }
                }
            }

            List<string> templateStrings = new List<string>(Properties.Resources.template.Split('\n'));
            StringBuilder finalString = new StringBuilder();

            for (int i = 0; i < templateStrings.Count; i++)
            {
                switch (templateStrings[i].TrimEnd('\r', '\n'))
                {
                    case "<!-- TITLE -->":
                        finalString.AppendLine(HttpUtility.HtmlEncode(Path.GetFileNameWithoutExtension(outputFile)));
                        break;
                    case "/* [CUSTOM CSS] */":
                        if (embedData)
                        {
                            foreach (var emote in chatRoot.embeddedData.firstParty)
                            {
                                finalString.AppendLine(".first-" + emote.id + " { content:url(\"data:image/png;base64, " + Convert.ToBase64String(emote.data) + "\"); }");
                            }
                            foreach (var emote in chatRoot.embeddedData.thirdParty)
                            {
                                finalString.AppendLine(".third-" + emote.id + " { content:url(\"data:image/png;base64, " + Convert.ToBase64String(emote.data) + "\"); }");
                            }
                        }
                        break;
                    case "<!-- CUSTOM HTML -->":
                        foreach (Comment comment in chatRoot.comments)
                        {
                            TimeSpan time = new TimeSpan(0, 0, (int)comment.content_offset_seconds);
                            string timestamp = time.ToString(@"h\:mm\:ss");
                            finalString.Append($"<pre class=\"comment-root\">[{timestamp}] <a href=\"https://www.twitch.tv/{comment.commenter.name}\" target=\"_blank\"><span class=\"comment-author\" {(comment.message.user_color == null ? "" : $"style=\"color: {comment.message.user_color}\"")}>{(comment.commenter.display_name.Any(x => x > 127) ? ($"{comment.commenter.display_name} ({comment.commenter.name})") : comment.commenter.display_name)}</span></a><span class=\"comment-message\">: {GetMessageHtml(embedData, thirdEmoteData, chatRoot, comment)}</span></pre>\n");
                        }
                        break;
                    default:
                        finalString.AppendLine(templateStrings[i].TrimEnd('\r', '\n'));
                        break;
                }
            }
            templateStrings.Clear();

            File.WriteAllText(outputFile, finalString.ToString(), Encoding.Unicode);
            GC.Collect();
        }

        internal static string GetMessageHtml(bool embedEmotes, Dictionary<string, EmbedEmoteData> thirdEmoteData, ChatRoot chatRoot, Comment comment)
        {
            StringBuilder message = new StringBuilder();

            if (comment.message.fragments == null)
            {
                comment.message.fragments = new List<Fragment>();
                comment.message.fragments.Add(new Fragment() { text = comment.message.body });
            }

            foreach (var fragment in comment.message.fragments)
            {
                if (fragment.emoticon == null)
                {
                    List<string> wordList = new List<string>(fragment.text.Split(' '));

                    foreach (var word in wordList)
                    {
                        if (thirdEmoteData.ContainsKey(word))
                        {
                            if (embedEmotes)
                            {
                                message.Append($"<img width=\"{thirdEmoteData[word].width}\" height=\"{thirdEmoteData[word].height}\" class=\"emote-image third-{thirdEmoteData[word].id}\" title=\"{word}\"\"><div class=\"invis-text\">{word}</div> ");
                            }
                            else
                            {
                                message.Append($"<img class=\"emote-image\" title=\"{word}\" src=\"{thirdEmoteData[word].url}\"><div class=\"invis-text\">{word}</div> ");
                            }
                        }
                        else if (word != "")
                            message.Append(HttpUtility.HtmlEncode(word) + " ");
                    }
                }
                else
                {
                    if (embedEmotes && chatRoot.embeddedData.firstParty.Any(x => x.id == fragment.emoticon.emoticon_id))
                    {
                        message.Append($"<img class=\"emote-image first-{fragment.emoticon.emoticon_id}\" title=\"{fragment.text}\"><div class=\"invis-text\">{fragment.text}</div> ");
                    }
                    else
                    {
                        message.Append($"<img class=\"emote-image\" src=\"https://static-cdn.jtvnw.net/emoticons/v2/{fragment.emoticon.emoticon_id}/default/dark/1.0\" title=\"{fragment.text}\"><div class=\"invis-text\">{fragment.text}</div> ");
                    }
                }
            }
            return message.ToString();
        }
    }
}
