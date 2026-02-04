using System.Text;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tests.ModelTests
{
    public class IrcMessageConverterTests
    {
        [Theory]
        [InlineData(
            "@badge-info=subscriber/14;badges=subscriber/12,clips-leader/2;client-nonce=d489646132ba48949421248ab4896c7e;color=#D2691E;display-name=Viewer8;emotes=emotesv2_2ec465e593e04c7a90c5c77ea7d0005e:0-14;first-msg=0;flags=;id=96452401-2707-4d42-b373-8b2777217cc4;mod=0;returning-chatter=0;room-id=123465789;subscriber=1;tmi-sent-ts=1754165165511;turbo=0;user-id=123465789;user-type= :viewer8!viewer8@viewer8.tmi.twitch.tv PRIVMSG #streamer8 :cerbyMinaArrive i'm back, what did i miss?  ( coffe was cold thanks Orb OMEGALUL )",
            new[] { "cerbyMinaArrive", " i'm back, what did i miss?  ( coffe was cold thanks Orb OMEGALUL )" })]
        [InlineData(
            @"@badge-info=;badges=sub-gifter/1;color=;display-name=Viewer8;emotes=;flags=;id=96452401-2707-4d42-b373-8b2777217cc4;login=viewer8;mod=0;msg-id=submysterygift;msg-param-community-gift-id=16416846846841616122;msg-param-goal-contribution-type=SUB_POINTS;msg-param-goal-current-contributions=11758;msg-param-goal-target-contributions=1000000;msg-param-goal-user-contributions=1;msg-param-mass-gift-count=1;msg-param-origin-id=16416846846841616122;msg-param-sender-count=1;msg-param-sub-plan=1000;room-id=12345678;subscriber=0;system-msg=Viewer8\sis\sgifting\s1\sTier\s1\sSubs\sto\sStreamer8's\scommunity!\sThey've\sgifted\sa\stotal\sof\s1\sin\sthe\schannel!;tmi-sent-ts=1754884571468;user-id=123456789;user-type=;vip=0 :tmi.twitch.tv USERNOTICE #stramer8",
            new[] { "Viewer8 is gifting 1 Tier 1 Subs to Streamer8's community! They've gifted a total of 1 in the channel!" })]
        [InlineData(
            @"@badge-info=subscriber/4;badges=subscriber/3,premium/1;color=#5F9EA0;display-name=Viewer8;emotes=;flags=;id=96452401-2707-4d42-b373-8b2777217cc4;login=viewer8;mod=0;msg-id=resub;msg-param-cumulative-months=4;msg-param-months=0;msg-param-multimonth-duration=1;msg-param-multimonth-tenure=0;msg-param-should-share-streak=0;msg-param-sub-plan-name=Subscribe\sto\sAlpharad!!;msg-param-sub-plan=Prime;msg-param-was-gifted=false;room-id=12345678;subscriber=1;system-msg=Viewer8\ssubscribed\swith\sPrime.\sThey've\ssubscribed\sfor\s4\smonths!;tmi-sent-ts=1754848616514;user-id=12345678;user-type=;vip=0 :tmi.twitch.tv USERNOTICE #streamer8 :Silly little frien",
            new[] { "Viewer8 subscribed with Prime. They've subscribed for 4 months! Silly little frien" })]
        [InlineData(
            @"@badge-info=subscriber/44;badges=subscriber/42;color=#00FF7F;display-name=Viewer8;emotes=emotesv2_6900a38dd1a046a786cde8610a777681:73-84;flags=;id=96452401-2707-4d42-b373-8b2777217cc4;login=viewer8;mod=0;msg-id=resub;msg-param-cumulative-months=44;msg-param-months=0;msg-param-multimonth-duration=24;msg-param-multimonth-tenure=20;msg-param-should-share-streak=0;msg-param-sub-plan-name=Gym\sMembership;msg-param-sub-plan=1000;msg-param-was-gifted=false;room-id=123456789;subscriber=1;system-msg=Viewer8\ssubscribed\sat\sTier\s1.\sThey've\ssubscribed\sfor\s44\smonths!;tmi-sent-ts=1716578989750;user-id=123456789;user-type=;vip=0 :tmi.twitch.tv USERNOTICE #streamer8 :wow i am so happy that i can soon legaly pirate movies from the internet buffpupHypeE",
            new[] { "Viewer8 subscribed at Tier 1. They've subscribed for 44 months! wow i am so happy that i can soon legaly pirate movies from the internet ", "buffpupHypeE" })]
        [InlineData(
            "@badge-info=subscriber/4;badges=subscriber/3;color=#008000;custom-reward-id=;display-name=Viewer8;emotes=;first-msg=0;flags=11-15:S.6;id=96452401-2707-4d42-b373-8b2777217cc4;mod=0;msg-id=;returning-chatter=0;room-id=123465789;subscriber=1;tmi-sent-ts=1754700560200;turbo=0;user-id=123456789;user-type= :viewer8!viewer8@viewer8.tmi.twitch.tv PRIVMSG #streamer8 :@viewer8 This is a message",
            new[] { "@viewer8 This is a message" })]
        [InlineData(
            @"@badge-info=;badges=;color=#00FF7F;display-name=Viewer8;emotes=;flags=;id=96452401-2707-4d42-b373-8b2777217cc4;login=viewer8;mod=0;msg-id=raid;msg-param-displayName=Viewer8;msg-param-login=viewer8;msg-param-profileImageURL=https://static-cdn.jtvnw.net/jtv_user_pictures/96452401-2707-4d42-b373-8b2777217cc4-profile_image-%s.jpeg;msg-param-viewerCount=1;room-id=123465789;subscriber=0;system-msg=1\sraiders\sfrom\sViewer8\shave\sjoined!;tmi-sent-ts=1754700560200;user-id=123456789;user-type=;vip=0 :tmi.twitch.tv USERNOTICE #streamer8",
            new[] { "1 raiders from Viewer8 have joined!" })]
        [InlineData(
            "@badge-info=subscriber/1;badges=subscriber/0;client-nonce=d489646132ba48949421248ab4896c7e;color=;display-name=Viewer8;emotes=425618:88-90,92-94/emotesv2_e8c6f82e506c4085a3489ca7926d21d4:24-45/301413216:47-61/emotesv2_47d5f86662374ef892d32788a1f69504:63-85;first-msg=0;flags=;id=96452401-2707-4d42-b373-8b2777217cc4;mod=0;returning-chatter=0;room-id=123456789;subscriber=1;tmi-sent-ts=1755187864566;turbo=0;user-id=1234567890;user-type= :viewer8!viewer8@viewer8.tmi.twitch.tv PRIVMSG #streamer8 :bro you suspended me... brucegFlappingwingleft brucegGoosehead brucegFlappingwingright  LUL LUL",
            new[] { "bro you suspended me... ", "brucegFlappingwingleft", " ", "brucegGoosehead", " ", "brucegFlappingwingright", "  ", "LUL", " ", "LUL" })]
        [InlineData(
            @"@badge-info=subscriber/5;badges=subscriber/0,sub-gifter/10;color=#8A2BE2;display-name=Viewer8;emotes=emotesv2_ab5eedefc77e439fbbabd4479f4af915:0-8,10-18,20-28,30-38,40-48,50-58,60-68,97-105,124-132/emotesv2_a612059103944007bf9889e9caddb9fc:70-77,79-86,88-95,107-114/emotesv2_28143b6747f24b1088972a200fc4bd49:116-122/emotesv2_a9ffa45e43774e4aad336dd7462494b7:134-142/emotesv2_a579d8f33ad14ab9ab2e430a57698a53:144-153;flags=;id=96452401-2707-4d42-b373-8b2777217cc4;login=viewer8;mod=0;msg-id=resub;msg-param-cumulative-months=5;msg-param-months=0;msg-param-multimonth-duration=3;msg-param-multimonth-tenure=2;msg-param-should-share-streak=0;msg-param-sub-plan-name=Subscription\s(uwu_to_owo);msg-param-sub-plan=1000;msg-param-was-gifted=false;room-id=123456789;subscriber=1;system-msg=Viewer8\ssubscribed\sat\sTier\s1.\sThey've\ssubscribed\sfor\s5\smonths!;tmi-sent-ts=1755116515476;user-id=123456789;user-type=;vip=0 :tmi.twitch.tv USERNOTICE #streamer8 :uwoWiggly uwoWiggly uwoWiggly uwoWiggly uwoWiggly uwoWiggly uwoWiggly uwoDance uwoDance uwoDance uwoWiggly uwoDance uwoSpin uwoWiggly uwoDansen uwoExplode",
            new[]
            {
                "Viewer8 subscribed at Tier 1. They've subscribed for 5 months! ", "uwoWiggly", " ", "uwoWiggly", " ", "uwoWiggly", " ", "uwoWiggly", " ", "uwoWiggly", " ", "uwoWiggly", " ", "uwoWiggly", " ", "uwoDance", " ", "uwoDance",
                " ", "uwoDance", " ", "uwoWiggly", " ", "uwoDance", " ", "uwoSpin", " ", "uwoWiggly", " ", "uwoDansen", " ", "uwoExplode"
            })]
        public void ProducesCorrectMessageOutputs(string ircRaw, string[] fragments)
        {
            var parser = new IrcParser(StubTaskProgress.Instance);
            var parsed = parser.Parse(Encoding.UTF8.GetBytes(ircRaw)).FirstOrDefault();
            Assert.NotNull(parsed);

            var converted = IrcMessageConverter.ToComment(parsed);
            Assert.NotNull(converted);
            Assert.NotNull(converted.message);

            Assert.NotNull(converted.message.fragments);
            Assert.Equal(fragments, converted.message.fragments.Select(x => x.text));

            var messageBody = string.Concat(converted.message.fragments.Select(x => x.text));
            Assert.Equal(messageBody, converted.message.body);
        }
    }
}