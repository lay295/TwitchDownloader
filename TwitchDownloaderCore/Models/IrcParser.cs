using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCore.Models
{
    public class IrcParser
    {
        private readonly ITaskLogger _logger;

        public IrcParser(ITaskLogger logger)
        {
            _logger = logger;
        }

        public List<IrcMessage> Parse(ReadOnlySpan<byte> text)
        {
            var messages = new List<IrcMessage>();

            var textStart = -1;
            var textEnd = text.Length;
            var lineEnd = -1;
            var iterations = 0;
            var maxIterations = text.Count((byte)'\n') + 1;
            do
            {
                textStart++;
                iterations++;
                if (iterations > maxIterations)
                    throw new Exception("Infinite loop encountered while decoding IRC messages.");

                if (textStart >= textEnd)
                    break;

                var workingSlice = text[textStart..];
                lineEnd = workingSlice.IndexOf((byte)'\n');
                if (lineEnd != -1)
                    workingSlice = workingSlice[..lineEnd].TrimEnd((byte)'\r');

                if (workingSlice.IsWhiteSpace())
                {
                    continue;
                }

                if (!TryParseMessage(workingSlice, out var newMessage))
                {
                    continue;
                }

                messages.Add(newMessage);

                if (lineEnd == -1)
                {
                    break;
                }
            } while (lineEnd != -1 && (textStart += lineEnd) < textEnd);

            return messages;
        }

        private bool TryParseMessage(ReadOnlySpan<byte> text, out IrcMessage newMessage)
        {
            var workingSlice = text;

            Dictionary<string, string> tags = null;
            if (workingSlice.StartsWith("@"u8))
            {
                tags = new Dictionary<string, string>();
                if (!TryParseTags(ref workingSlice, tags))
                {
                    newMessage = null;
                    return false;
                }
            }

            ReadOnlySpan<byte> nick = default;
            ReadOnlySpan<byte> user = default;
            ReadOnlySpan<byte> host = default;
            if (workingSlice.StartsWith(":"u8))
            {
                if (!TryParseSource(ref workingSlice, out nick, out user, out host))
                {
                    newMessage = null;
                    return false;
                }
            }

            if (!TryParseCommand(ref workingSlice, out var command))
            {
                newMessage = null;
                return false;
            }

            var parametersRaw = workingSlice;

            newMessage = new IrcMessage
            {
                Command = command,
                Nickname = string.Intern(Encoding.UTF8.GetString(nick)),
                User = string.Intern(Encoding.UTF8.GetString(user)),
                Host = string.Intern(Encoding.UTF8.GetString(host)),
                ParametersRaw = Encoding.UTF8.GetString(parametersRaw),
                Tags = tags,
            };

            return true;
        }

        // https://modern.ircdocs.horse/#tags
        // https://ircv3.net/specs/extensions/message-tags.html#format
        // This doesn't support valueless tags (e.g. @id=123AB;rose ->  {"id": "123AB", "rose": ""}) but Twitch doesn't seem to use them
        private bool TryParseTags(scoped ref ReadOnlySpan<byte> text, Dictionary<string, string> tags)
        {
            var workingSlice = text.TrimStart("@"u8);

            var done = false;
            do
            {
                // Get tag name
                var keyValueSeparator = workingSlice.IndexOf("="u8);
                if (keyValueSeparator == -1)
                {
                    _logger.LogWarning($"Invalid tag found in message: {Encoding.UTF8.GetString(text)}");
                    return false;
                }

                var tagName = workingSlice[..keyValueSeparator];
                workingSlice = workingSlice[(keyValueSeparator + 1)..];

                // Get tag separator
                var tagSeparator = workingSlice.IndexOfAny("; "u8);
                if (tagSeparator == -1)
                {
                    _logger.LogWarning($"Invalid tag found in message: {Encoding.UTF8.GetString(text)}");
                    return false;
                }

                var tagValue = workingSlice[..tagSeparator];
                done = workingSlice[tagSeparator] == ' ';
                workingSlice = workingSlice[(tagSeparator + 1)..];

                // Add to dictionary
                var nameString = string.Intern(Encoding.UTF8.GetString(tagName));
                var valueString = tagValue.IsEmpty
                    ? null
                    : UnEscapeTagValue(Encoding.UTF8.GetString(tagValue));

                if (!tags.TryAdd(nameString, valueString))
                {
                    _logger.LogWarning($"Duplicate tags found in message: {Encoding.UTF8.GetString(text)}");
                }
            } while (!done);

            text = workingSlice;
            return true;
        }

        // https://ircv3.net/specs/extensions/message-tags.html#escaping-values
        private static string UnEscapeTagValue(string value)
        {
            if (!value.Contains('\\'))
                return value;

            var sb = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (c == '\\')
                {
                    i++;

                    if (i >= value.Length)
                        break;

                    c = value[i] switch
                    {
                        ':' => ';',
                        's' => ' ',
                        'r' => '\r',
                        'n' => '\n',
                        _ => value[i]
                    };
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        // https://modern.ircdocs.horse/#source
        private bool TryParseSource(scoped ref ReadOnlySpan<byte> text, out ReadOnlySpan<byte> nick, out ReadOnlySpan<byte> user, out ReadOnlySpan<byte> host)
        {
            nick = user = host = default;

            var text2 = text.TrimStart(":"u8);

            var workingSlice = text2;
            var sourceEnd = workingSlice.IndexOf(" "u8);
            if (sourceEnd == -1)
            {
                _logger.LogWarning($"Invalid source tag found in message: {Encoding.UTF8.GetString(text)}");
                return false;
            }

            workingSlice = workingSlice[..sourceEnd];

            List<byte> separators = [(byte)'!', (byte)'@'];
            while (true)
            {
                var next = workingSlice.LastIndexOfAny(CollectionsMarshal.AsSpan(separators));
                if (next < 1)
                {
                    nick = workingSlice;
                    break;
                }

                var c = workingSlice[next];
                switch (c)
                {
                    case (byte)'!':
                        user = workingSlice[(next + 1)..];
                        break;
                    case (byte)'@':
                        host = workingSlice[(next + 1)..];
                        break;
                }

                separators.Remove(c);
                workingSlice = workingSlice[..next];
            }

            text = text2[(sourceEnd + 1)..];

            return true;
        }

        private bool TryParseCommand(ref ReadOnlySpan<byte> text, out IrcCommand command)
        {
            var commandLen = text.IndexOf(" "u8);
            if (commandLen == -1)
            {
                _logger.LogWarning($"Invalid command found in message: {Encoding.UTF8.GetString(text)}");
                command = IrcCommand.Unknown;
                return false;
            }

            var workingSlice = text[..commandLen];

            var charCount = Encoding.UTF8.GetCharCount(workingSlice);
            var commandText = charCount < 256
                ? stackalloc char[charCount]
                : new char[charCount];

            Encoding.UTF8.GetChars(workingSlice, commandText);

            command = commandText switch
            {
                "CLEARCHAT" => IrcCommand.ClearChat,
                "CLEARMSG" => IrcCommand.ClearMsg,
                "GLOBALUSERSTATE" => IrcCommand.GlobalUserState,
                "NOTICE" => IrcCommand.Notice,
                "JOIN" => IrcCommand.Join,
                "PART" => IrcCommand.Part,
                "PING" => IrcCommand.Ping,
                "PONG" => IrcCommand.Pong,
                "PRIVMSG" => IrcCommand.PrivMsg,
                "RECONNECT" => IrcCommand.Reconnect,
                "ROOMSTATE" => IrcCommand.RoomState,
                "USERNOTICE" => IrcCommand.UserNotice,
                "USERSTATE" => IrcCommand.UserState,
                "CAP" => IrcCommand.Cap,
                "001" => IrcCommand.RplWelcome,
                "002" => IrcCommand.RplYourHost,
                "003" => IrcCommand.RplCreated,
                "004" => IrcCommand.RplMyInfo,
                "353" => IrcCommand.RplNameReply,
                "366" => IrcCommand.RplEndOfNames,
                "372" => IrcCommand.RplMotd,
                "375" => IrcCommand.RplMotdStart,
                "376" => IrcCommand.RplEndOfMod,
                _ => IrcCommand.Unknown
            };

            text = text[(commandLen + 1)..];
            return command != IrcCommand.Unknown;
        }
    }
}