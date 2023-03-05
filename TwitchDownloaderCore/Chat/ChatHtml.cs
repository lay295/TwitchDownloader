﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
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

            string[] templateStrings = Properties.Resources.template.Split('\n');

            var outputDirectory = Directory.GetParent(Path.GetFullPath(filePath))!;
            if (!outputDirectory.Exists)
            {
                TwitchHelper.CreateDirectory(outputDirectory.FullName);
            }

            await using var fs = File.Create(filePath);
            await using var sw = new StreamWriter(fs, Encoding.Unicode);

            for (int i = 0; i < templateStrings.Length; i++)
            {
                switch (templateStrings[i].TrimEnd('\r', '\n'))
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
                                    await sw.WriteLineAsync(".badge-" + badge.name + "-" + version + " { content:url(\"data:image/png;base64, " + Convert.ToBase64String(badgeData) + "\"); }");
                                }
                            }
                        }
                        break;
                    case "<!-- CUSTOM HTML -->":
                        foreach (Comment comment in chatRoot.comments)
                        {
                            var relativeTime = new TimeSpan(0, 0, (int)comment.content_offset_seconds);
                            string timestamp = relativeTime.ToString(@"h\:mm\:ss");
                            await sw.WriteAsync($"<pre class=\"comment-root\">[{timestamp}] {GetChatBadgesHtml(embedData, chatBadgeData, chatRoot, comment)} <a href=\"https://www.twitch.tv/{comment.commenter.name}\" target=\"_blank\"><span class=\"comment-author\" {(comment.message.user_color == null ? "" : $"style=\"color: {comment.message.user_color}\"")}>{(comment.commenter.display_name.Any(x => x > 127) ? $"{comment.commenter.display_name} ({comment.commenter.name})" : comment.commenter.display_name)}</span></a><span class=\"comment-message\">: {GetMessageHtml(embedData, thirdEmoteData, chatRoot, comment)}</span></pre>\n");
                        }
                        break;
                    default:
                        await sw.WriteLineAsync(templateStrings[i].TrimEnd('\r', '\n'));
                        break;
                }
            }
        }

        private static async Task BuildThirdPartyDictionary(ChatRoot chatRoot, bool embedData, Dictionary<string, EmbedEmoteData> thirdEmoteData, CancellationToken cancellationToken)
        {
            EmoteResponse emotes = await TwitchHelper.GetThirdPartyEmoteData(chatRoot.streamer.id, true, true, true, true, cancellationToken);
            List<EmoteResponseItem> itemList = new();
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
                        EmbedEmoteData embedEmoteData = new();
                        embedEmoteData.url = item.ImageUrl.Replace("[scale]", "1");
                        thirdEmoteData[item.Code] = embedEmoteData;
                    }
                }
            }
        }

        private static async Task BuildChatBadgesDictionary(ChatRoot chatRoot, bool embedData, Dictionary<string, EmbedChatBadge> chatBadgeData, CancellationToken cancellationToken)
        {
            // No need to build the dictionary if badges are embeded
            if (embedData)
                return;

            List<EmbedChatBadge> badges = await TwitchHelper.GetChatBadgesData(chatRoot.comments, chatRoot.streamer.id, cancellationToken);

            foreach (var badge in badges)
            {
                chatBadgeData[badge.name] = badge;
            }
        }

        private static string GetChatBadgesHtml(bool embedData, Dictionary<string, EmbedChatBadge> chatBadgeData, ChatRoot chatRoot, Comment comment)
        {
            if (comment.message.user_badges.Count == 0)
                return string.Empty;

            List<string> badgesHtml = new List<string>();

            foreach (var messageBadge in comment.message.user_badges)
            {
                if (embedData)
                {
                    badgesHtml.Add($"<img class=\"emote-image badge-{messageBadge._id}-{messageBadge.version}\" title=\"{messageBadge._id}\"\"><div class=\"invis-text\">{messageBadge._id}</div>");
                }
                else
                {
                    if (!chatBadgeData.ContainsKey(messageBadge._id))
                        continue;
                    
                    if (!chatBadgeData[messageBadge._id].urls.ContainsKey(messageBadge.version))
                        continue;

                    badgesHtml.Add($"<img class=\"emote-image\" title=\"{messageBadge._id}\" src=\"{chatBadgeData[messageBadge._id].urls[messageBadge.version]}\"><div class=\"invis-text\">{messageBadge._id}</div>");
                }
            }

            return string.Join(" ", badgesHtml);
        }

        private static string GetMessageHtml(bool embedEmotes, Dictionary<string, EmbedEmoteData> thirdEmoteData, ChatRoot chatRoot, Comment comment)
        {
            StringBuilder message = new();

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
