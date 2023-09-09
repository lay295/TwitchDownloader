using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Chat
{
    public static class ChatHtml
    {
        // TODO: Add support for embedding Twitch bits in HTML chats
        /// <summary>
        /// Serializes a chat Html file.
        /// </summary>
        public static async Task SerializeAsync(string filePath, ChatRoot chatRoot, bool embedData = true, CancellationToken cancellationToken = new())
        {
            ArgumentNullException.ThrowIfNull(filePath, nameof(filePath));

            Dictionary<string, EmbedEmoteData> thirdEmoteData = new();
            await BuildThirdPartyDictionary(chatRoot, embedData, thirdEmoteData, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<string, EmbedChatBadge> chatBadgeData = new();
            await BuildChatBadgesDictionary(chatRoot, embedData, chatBadgeData, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            using var templateStream = new MemoryStream(Properties.Resources.chat_template);
            using var templateReader = new StreamReader(templateStream);

            var outputDirectory = Directory.GetParent(Path.GetFullPath(filePath))!;
            if (!outputDirectory.Exists)
            {
                TwitchHelper.CreateDirectory(outputDirectory.FullName);
            }

            await using var fs = File.Create(filePath);
            await using var sw = new StreamWriter(fs);

            while (!templateReader.EndOfStream)
            {
                var line = await templateReader.ReadLineAsync();
                switch (line)
                {
                    case "<!-- TITLE -->":
                        await sw.WriteLineAsync(HttpUtility.HtmlEncode(Path.GetFileNameWithoutExtension(filePath)));
                        break;
                    case "/* [CUSTOM CSS] */":
                        if (embedData)
                        {
                            foreach (var emote in chatRoot.embeddedData.firstParty)
                            {
                                await sw.WriteLineAsync(".first-" + emote.id + " { content:url(\"data:image/png;base64, " + Convert.ToBase64String(emote.data) + "\"); }");
                            }
                            foreach (var emote in chatRoot.embeddedData.thirdParty)
                            {
                                await sw.WriteLineAsync(".third-" + emote.id + " { content:url(\"data:image/png;base64, " + Convert.ToBase64String(emote.data) + "\"); }");
                            }
                            foreach (var badge in chatRoot.embeddedData.twitchBadges)
                            {
                                foreach(var (version, badgeData) in badge.versions)
                                {
                                    await sw.WriteLineAsync(".badge-" + badge.name + "-" + version + " { content:url(\"data:image/png;base64, " + Convert.ToBase64String(badgeData.bytes) + "\"); }");
                                }
                            }
                        }
                        break;
                    case "<!-- CUSTOM HTML -->":
                        foreach (var comment in chatRoot.comments)
                        {
                            var relativeTime = TimeSpan.FromSeconds(comment.content_offset_seconds);
                            var timestamp = TimeSpanHFormat.ReusableInstance.Format(@"H\:mm\:ss", relativeTime);
                            await sw.WriteLineAsync($"<pre class=\"comment-root\">[{timestamp}] {GetChatBadgesHtml(embedData, chatBadgeData, comment)}<a href=\"https://twitch.tv/{comment.commenter.name}\"><span class=\"comment-author\" {(comment.message.user_color == null ? "" : $"style=\"color: {comment.message.user_color}\"")}>{(comment.commenter.display_name.Any(x => x > 127) ? $"{comment.commenter.display_name} ({comment.commenter.name})" : comment.commenter.display_name)}</span></a><span class=\"comment-message\">: {GetMessageHtml(embedData, thirdEmoteData, chatRoot, comment)}</span></pre>");
                        }
                        break;
                    default:
                        await sw.WriteLineAsync(line);
                        break;
                }
            }
        }

        private static async Task BuildThirdPartyDictionary(ChatRoot chatRoot, bool embedData, Dictionary<string, EmbedEmoteData> thirdEmoteData, CancellationToken cancellationToken)
        {
            EmoteResponse emotes = await TwitchHelper.GetThirdPartyEmotesMetadata(chatRoot.streamer.id, true, true, true, true, cancellationToken);
            List<EmoteResponseItem> itemList = new();
            itemList.AddRange(emotes.BTTV);
            itemList.AddRange(emotes.FFZ);
            itemList.AddRange(emotes.STV);

            foreach (var item in itemList.Where(item => !thirdEmoteData.ContainsKey(item.Code)))
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
                    EmbedEmoteData embedEmoteData = new();
                    embedEmoteData.url = item.ImageUrl.Replace("[scale]", "1");
                    thirdEmoteData[item.Code] = embedEmoteData;
                }
            }
        }

        private static async Task BuildChatBadgesDictionary(ChatRoot chatRoot, bool embedData, Dictionary<string, EmbedChatBadge> chatBadgeData, CancellationToken cancellationToken)
        {
            // No need to build the dictionary if badges are embedded
            if (embedData)
                return;

            List<EmbedChatBadge> badges = await TwitchHelper.GetChatBadgesData(chatRoot.comments, chatRoot.streamer.id, cancellationToken);

            foreach (var badge in badges)
            {
                chatBadgeData[badge.name] = badge;
            }
        }

        private static string GetChatBadgesHtml(bool embedData, IReadOnlyDictionary<string, EmbedChatBadge> chatBadgeData, Comment comment)
        {
            if (comment.message.user_badges is null || comment.message.user_badges.Count == 0)
                return "";

            var badgesHtml = new List<string>(comment.message.user_badges!.Count);

            foreach (var messageBadge in comment.message.user_badges)
            {
                if (embedData)
                {
                    badgesHtml.Add($"<img class=\"badge-image badge-{messageBadge._id}-{messageBadge.version}\" title=\"{messageBadge._id}\"><span class=\"text-hide\">{messageBadge._id}</span>");
                }
                else
                {
                    if (!chatBadgeData.TryGetValue(messageBadge._id, out var badgeId))
                        continue;

                    if (!badgeId.versions.TryGetValue(messageBadge.version, out var badge))
                        continue;

                    badgesHtml.Add($"<img class=\"badge-image\" title=\"{messageBadge._id}\" src=\"{badge.url}\"><span class=\"text-hide\">{messageBadge._id}</span>");
                }
            }

            badgesHtml.Add(""); // Ensure the html string ends with a space
            return string.Join(' ', badgesHtml);
        }

        private static string GetMessageHtml(bool embedEmotes, IReadOnlyDictionary<string, EmbedEmoteData> thirdEmoteData, ChatRoot chatRoot, Comment comment)
        {
            var message = new StringBuilder(comment.message.body.Length);

            comment.message.fragments ??= new List<Fragment> { new() { text = comment.message.body } };

            foreach (var fragment in comment.message.fragments)
            {
                if (fragment.emoticon == null)
                {
                    foreach (var word in fragment.text.Split(' '))
                    {
                        if (thirdEmoteData.ContainsKey(word))
                        {
                            if (embedEmotes)
                            {
                                message.Append($"<img class=\"emote-image third-{thirdEmoteData[word].id}\" title=\"{word}\"><span class=\"text-hide\">{word}</span> ");
                            }
                            else
                            {
                                message.Append($"<img class=\"emote-image\" title=\"{word}\" src=\"{thirdEmoteData[word].url}\"><span class=\"text-hide\">{word}</span> ");
                            }
                        }
                        else if (word != "")
                        {
                            message.Append(HttpUtility.HtmlEncode(word));
                            message.Append(' ');
                        }
                    }
                }
                else
                {
                    if (embedEmotes && chatRoot.embeddedData.firstParty.Any(x => x.id == fragment.emoticon.emoticon_id))
                    {
                        message.Append($"<img class=\"emote-image first-{fragment.emoticon.emoticon_id}\" title=\"{fragment.text}\"><span class=\"text-hide\">{fragment.text}</span> ");
                    }
                    else
                    {
                        message.Append($"<img class=\"emote-image\" src=\"https://static-cdn.jtvnw.net/emoticons/v2/{fragment.emoticon.emoticon_id}/default/dark/1.0\" title=\"{fragment.text}\"><span class=\"text-hide\">{fragment.text}</span> ");
                    }
                }
            }

            return message.ToString();
        }
    }
}
