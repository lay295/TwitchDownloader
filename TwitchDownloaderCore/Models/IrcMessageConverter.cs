using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Models
{
    public static class IrcMessageConverter
    {
        [return: MaybeNull]
        public static Comment ToComment(IrcMessage ircMessage)
        {
            if (ircMessage.Command is not IrcCommand.PrivMsg and not IrcCommand.UserNotice)
            {
                return null;
            }

            if (!ircMessage.TryGetTag("id", out var id) ||
                !ircMessage.TryGetTag("tmi-sent-ts", out var timeSentStr) || !long.TryParse(timeSentStr, out var sentMillis) ||
                !ircMessage.TryGetTag("room-id", out var roomId) ||
                !ircMessage.TryGetTag("user-id", out var userId) ||
                !ircMessage.TryGetTag("display-name", out var displayName))
            {
                return null;
            }

            if (!ircMessage.TryGetTag("color", out var color)) color = null;
            if (!ircMessage.TryGetTag("msg-id", out var msgId)) msgId = null;
            if (!ircMessage.TryGetTag("bits", out var bitsStr) || !int.TryParse(bitsStr, out var bits)) bits = 0;

            var messageBody = BuildMessageBody(ircMessage, out var emoteOffset);
            var badges = BuildBadgeList(ircMessage);
            var emoticons = BuildEmoticonList(ircMessage, emoteOffset);
            var fragments = BuildFragmentList(emoticons, messageBody);

            return new Comment
            {
                _id = id,
                created_at = DateTimeOffset.FromUnixTimeMilliseconds(sentMillis).DateTime,
                channel_id = roomId,
                content_type = "video",
                content_id = null,
                content_offset_seconds = 0,
                commenter = new Commenter
                {
                    display_name = displayName,
                    _id = userId,
                    name = ircMessage.Nickname,
                    bio = null,
                    created_at = default,
                    updated_at = default,
                    logo = null,
                },
                message = new Message
                {
                    body = messageBody,
                    bits_spent = bits,
                    fragments = fragments,
                    user_badges = badges,
                    user_color = color,
                    // There are other message ids that we could support, but highlighted-message is the only one that's used by the renderer
                    user_notice_params = msgId == "highlighted-message"
                        ? new UserNoticeParams { msg_id = "highlighted-message" }
                        : null,
                    emoticons = emoticons
                },
            };
        }

        private static string BuildMessageBody(IrcMessage ircMessage, out int emoteOffset)
        {
            var messageBegin = ircMessage.ParametersRaw.IndexOf(':');
            var messageBody = messageBegin == -1
                ? ""
                : ircMessage.ParametersRaw[(messageBegin + 1)..];

            if (ircMessage.TryGetTag("system-msg", out var systemMsg) && systemMsg != null)
            {
                if (string.IsNullOrEmpty(messageBody))
                {
                    messageBody = systemMsg;
                    emoteOffset = 0;
                }
                else
                {
                    messageBody = $"{systemMsg} {messageBody}";
                    emoteOffset = systemMsg.Length + 1;
                }
            }
            else
            {
                emoteOffset = 0;
            }

            return messageBody;
        }

        private static List<UserBadge> BuildBadgeList(IrcMessage ircMessage)
        {
            var badges = new List<UserBadge>();
            if (ircMessage.TryGetTag("badges", out var ircBadges) && ircBadges != null)
            {
                foreach (var badge in ircBadges.Split(','))
                {
                    var split = badge.Split('/');
                    badges.Add(new UserBadge
                    {
                        _id = string.Intern(split[0]),
                        version = string.Intern(split[1])
                    });
                }
            }

            return badges;
        }

        private static List<Emoticon2> BuildEmoticonList(IrcMessage ircMessage, int emoteOffset)
        {
            var emoticons = new List<Emoticon2>();
            if (ircMessage.TryGetTag("emotes", out var ircEmotes) && ircEmotes != null)
            {
                foreach (var ircEmote in ircEmotes.Split('/'))
                {
                    var emoteSeparator = ircEmote.IndexOf(':');
                    if (emoteSeparator == -1) continue;

                    var emoteName = ircEmote[..emoteSeparator];
                    var ranges = ircEmote[(emoteSeparator + 1)..];
                    foreach (var rangeStr in ranges.Split(','))
                    {
                        var range = rangeStr.Split('-');
                        if (!int.TryParse(range[0], out var begin) || !int.TryParse(range[1], out var end))
                            continue;

                        emoticons.Add(new Emoticon2
                        {
                            _id = emoteName,
                            begin = begin + emoteOffset,
                            end = end + emoteOffset,
                        });
                    }
                }
            }

            emoticons.Sort((a, b) => a.begin.CompareTo(b.begin));

            return emoticons;
        }

        private static List<Fragment> BuildFragmentList(List<Emoticon2> emoticons, string messageBody)
        {
            var fragments = new List<Fragment>();
            var prevFragment = 0;
            foreach (var emoticon in emoticons)
            {
                if (prevFragment != emoticon.begin)
                {
                    var betweenEmotes = messageBody[prevFragment..emoticon.begin];
                    fragments.Add(new Fragment
                    {
                        text = betweenEmotes,
                        emoticon = null,
                    });
                }

                fragments.Add(new Fragment
                {
                    text = string.Intern(messageBody[emoticon.begin..(emoticon.end + 1)]),
                    emoticon = new Emoticon
                    {
                        emoticon_id = emoticon._id
                    }
                });

                prevFragment = emoticon.end + 1;
            }

            if (prevFragment < messageBody.Length)
            {
                var afterEmotes = messageBody[prevFragment..];
                fragments.Add(new Fragment
                {
                    text = afterEmotes,
                    emoticon = null,
                });
            }

            return fragments;
        }
    }
}