using System.Text;
using System.Text.Json;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    public class IrcParserTests
    {
        [Theory]
        [InlineData(
            "@emote-only=0;followers-only=-1;r9k=0;room-id=123465789;slow=0;subs-only=0 :tmi.twitch.tv ROOMSTATE #streamer8",
            "{\"emote-only\":\"0\",\"followers-only\":\"-1\",\"r9k\":\"0\",\"room-id\":\"123465789\",\"slow\":\"0\",\"subs-only\":\"0\"}",
            "tmi.twitch.tv",
            "",
            "",
            IrcCommand.RoomState,
            "#streamer8")]
        [InlineData(
            "@badge-info=subscriber/4;badges=subscriber/3;color=#008000;custom-reward-id=;display-name=Viewer8;emotes=;first-msg=0;flags=11-15:S.6;id=96452401-2707-4d42-b373-8b2777217cc4;mod=0;msg-id=;returning-chatter=0;room-id=123465789;subscriber=1;tmi-sent-ts=1754700560200;turbo=0;user-id=123456789;user-type= :viewer8!viewer8@viewer8.tmi.twitch.tv PRIVMSG #streamer8 :@viewer8 This is a message",
            "{\"badge-info\":\"subscriber/4\",\"badges\":\"subscriber/3\",\"color\":\"#008000\",\"custom-reward-id\":null,\"display-name\":\"Viewer8\",\"emotes\":null,\"first-msg\":\"0\",\"flags\":\"11-15:S.6\",\"id\":\"96452401-2707-4d42-b373-8b2777217cc4\",\"mod\":\"0\",\"msg-id\":null,\"returning-chatter\":\"0\",\"room-id\":\"123465789\",\"subscriber\":\"1\",\"tmi-sent-ts\":\"1754700560200\",\"turbo\":\"0\",\"user-id\":\"123456789\",\"user-type\":null}",
            "viewer8",
            "viewer8",
            "viewer8.tmi.twitch.tv",
            IrcCommand.PrivMsg,
            "#streamer8 :@viewer8 This is a message")]
        [InlineData(
            "@id=123AB;rose :viewer8!viewer8@viewer8.tmi.twitch.tv PRIVMSG #streamer8 :This is a message",
            "{\"id\":\"123AB\",\"rose\":null}",
            "viewer8",
            "viewer8",
            "viewer8.tmi.twitch.tv",
            IrcCommand.PrivMsg,
            "#streamer8 :This is a message")]
        public void CorrectlyParsesMessage_WithTags(string ircRaw, string tagsJson, string serverOrNick, string user, string host, IrcCommand command, string parameters)
        {
            var tags = JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson);
            var parser = new IrcParser(StubTaskProgress.Instance);
            var ircBytes = Encoding.UTF8.GetBytes(ircRaw);

            var parsed = parser.Parse(ircBytes);

            Assert.NotNull(parsed);
            Assert.Equal(1, parsed.Count);
            Assert.Equal(serverOrNick, parsed[0].Nickname);
            Assert.Equal(user, parsed[0].User);
            Assert.Equal(host, parsed[0].Host);
            Assert.Equal(command, parsed[0].Command);
            Assert.Equal(tags, parsed[0].Tags);
            Assert.Equal(parameters, parsed[0].ParametersRaw);
        }

        [Theory]
        [InlineData(":tmi.twitch.tv CAP * ACK :twitch.tv/commands twitch.tv/tags", "tmi.twitch.tv", "", "", IrcCommand.Cap, "* ACK :twitch.tv/commands twitch.tv/tags")]
        [InlineData(":tmi.twitch.tv 001 justinfan1234 :Welcome, GLHF!", "tmi.twitch.tv", "", "", IrcCommand.RplWelcome, "justinfan1234 :Welcome, GLHF!")]
        [InlineData(":tmi.twitch.tv 002 justinfan1234 :Your host is tmi.twitch.tv", "tmi.twitch.tv", "", "", IrcCommand.RplYourHost, "justinfan1234 :Your host is tmi.twitch.tv")]
        [InlineData(":tmi.twitch.tv 003 justinfan1234 :This server is rather new", "tmi.twitch.tv", "", "", IrcCommand.RplCreated, "justinfan1234 :This server is rather new")]
        [InlineData(":tmi.twitch.tv 004 justinfan1234 :-", "tmi.twitch.tv", "", "", IrcCommand.RplMyInfo, "justinfan1234 :-")]
        [InlineData(":tmi.twitch.tv 375 justinfan1234 :-", "tmi.twitch.tv", "", "", IrcCommand.RplMotdStart, "justinfan1234 :-")]
        [InlineData(":tmi.twitch.tv 372 justinfan1234 :You are in a maze of twisty passages, all alike.", "tmi.twitch.tv", "", "", IrcCommand.RplMotd, "justinfan1234 :You are in a maze of twisty passages, all alike.")]
        [InlineData(":tmi.twitch.tv 376 justinfan1234 :>", "tmi.twitch.tv", "", "", IrcCommand.RplEndOfMod, "justinfan1234 :>")]
        [InlineData(":justinfan1234!justinfan1234@justinfan1234.tmi.twitch.tv JOIN #streamer8", "justinfan1234", "justinfan1234", "justinfan1234.tmi.twitch.tv", IrcCommand.Join, "#streamer8")]
        [InlineData(":justinfan1234.tmi.twitch.tv 353 justinfan1234 = #streamer8 :justinfan1234", "justinfan1234.tmi.twitch.tv", "", "", IrcCommand.RplNameReply, "justinfan1234 = #streamer8 :justinfan1234")]
        [InlineData(":justinfan1234.tmi.twitch.tv 366 justinfan1234 #streamer8 :End of /NAMES list", "justinfan1234.tmi.twitch.tv", "", "", IrcCommand.RplEndOfNames, "justinfan1234 #streamer8 :End of /NAMES list")]
        [InlineData(":justinfan1234!justinfan1234@justinfan1234.tmi.twitch.tv PART #streamer8", "justinfan1234", "justinfan1234", "justinfan1234.tmi.twitch.tv", IrcCommand.Part, "#streamer8")]
        [InlineData(":tmi.twitch.tv ROOMSTATE #streamer8", "tmi.twitch.tv", "", "", IrcCommand.RoomState, "#streamer8")]
        [InlineData(":viewer8!viewer8@viewer8.tmi.twitch.tv PRIVMSG #streamer8 :@viewer8 This is a message", "viewer8", "viewer8", "viewer8.tmi.twitch.tv", IrcCommand.PrivMsg, "#streamer8 :@viewer8 This is a message")]
        [InlineData(":tmi.twitch.tv RECONNECT", "tmi.twitch.tv", "", "", IrcCommand.Reconnect, "")]
        [InlineData("PING :tmi.twitch.tv", "", "", "", IrcCommand.Ping, ":tmi.twitch.tv")]
        [InlineData("PONG :tmi.twitch.tv", "", "", "", IrcCommand.Pong, ":tmi.twitch.tv")]
        [InlineData("PING", "", "", "", IrcCommand.Ping, "")]
        [InlineData("PONG", "", "", "", IrcCommand.Pong, "")]
        public void CorrectlyParsesMessage_WithoutTags(string ircRaw, string serverOrNick, string user, string host, IrcCommand command, string parameters)
        {
            var parser = new IrcParser(StubTaskProgress.Instance);
            var ircBytes = Encoding.UTF8.GetBytes(ircRaw);

            var parsed = parser.Parse(ircBytes);

            Assert.NotNull(parsed);
            Assert.Equal(1, parsed.Count);
            Assert.Equal(serverOrNick, parsed[0].Nickname);
            Assert.Equal(user, parsed[0].User);
            Assert.Equal(host, parsed[0].Host);
            Assert.Equal(command, parsed[0].Command);
            Assert.Null(parsed[0].Tags);
            Assert.Equal(parameters, parsed[0].ParametersRaw);
        }

        [Theory]
        [InlineData("")]
        [InlineData("\r\n")]
        [InlineData("abc123")]
        [InlineData("abc123\r\n")]
        [InlineData("\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\r\n")]
        public void DoesNotParseBadFormat(string ircRaw)
        {
            var parser = new IrcParser(StubTaskProgress.Instance);
            var ircBytes = Encoding.UTF8.GetBytes(ircRaw);

            var parsed = parser.Parse(ircBytes);

            Assert.Empty(parsed);
        }
    }
}