using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tools
{
    public class ChatHtml
    {
        public string FilePath { get; set; }

        public ChatHtml() { }

        /// <summary>
        /// Serializes a chat Html file.
        /// </summary>
        public async Task SerializeAsync(ChatRoot chatRoot, bool embedData = true)
            => await SerializeAsync(FilePath, chatRoot, embedData);

        // TODO: Add support for embedding Twitch bits and Twitch badges in HTML chats
        /// <summary>
        /// Serializes a chat Html file.
        /// </summary>
        public static async Task SerializeAsync(string filePath, ChatRoot chatRoot, bool embedData = true)
        {
            if (filePath is null)
                throw new ArgumentNullException(nameof(filePath));

            Dictionary<string, EmbedEmoteData> thirdEmoteData = new Dictionary<string, EmbedEmoteData>();
            EmoteResponse emotes = await TwitchHelper.GetThirdPartyEmoteData(chatRoot.streamer.id.ToString(), true, true, true);
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
                        finalString.AppendLine(HttpUtility.HtmlEncode(Path.GetFileNameWithoutExtension(filePath)));
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

            File.WriteAllText(filePath, finalString.ToString(), Encoding.Unicode);
            GC.Collect();
        }

        internal static string GetMessageHtml(bool embedEmotes, Dictionary<string, EmbedEmoteData> thirdEmoteData, ChatRoot chatRoot, Comment comment)
        {
            StringBuilder message = new StringBuilder();

            comment.message.fragments ??= new List<Fragment> { new Fragment() { text = comment.message.body } };

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
                        {
                            message.Append(HttpUtility.HtmlEncode(word) + " ");
                        }
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
