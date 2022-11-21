using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public class ChatUpdater
    {
        public ChatRoot chatRoot { get; set; } = new ChatRoot();
        private readonly ChatUpdateOptions updateOptions;

        public ChatUpdater(ChatUpdateOptions UpdateOptions)
        {
            updateOptions = UpdateOptions;
        }

        public async Task UpdateAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            string cacheFolder = Path.Combine(string.IsNullOrWhiteSpace(updateOptions.TempFolder) ? Path.GetTempPath() : updateOptions.TempFolder, "TwitchDownloader", "chatupdatecache");

            // Clear working directory if it already exists
            if (Directory.Exists(cacheFolder))
                Directory.Delete(cacheFolder, true);

            // Thirdparty emotes
            if (chatRoot.embeddedData.thirdParty == null || updateOptions.UpdateOldEmbeds)
            {
                chatRoot.embeddedData.thirdParty = new List<EmbedEmoteData>();
            }
            Console.WriteLine("Input third party emote count: " + chatRoot.embeddedData.thirdParty.Count);
            List<TwitchEmote> thirdPartyEmotes = new List<TwitchEmote>();
            thirdPartyEmotes = Task.Run(() => TwitchHelper.GetThirdPartyEmotes(chatRoot.streamer.id, cacheFolder, bttv: updateOptions.BttvEmotes, ffz: updateOptions.FfzEmotes, stv: inputOptions.StvEmotes, embeddedData: chatRoot.embeddedData)).Result;
            foreach (TwitchEmote emote in thirdPartyEmotes)
            {
                EmbedEmoteData newEmote = new EmbedEmoteData();
                newEmote.id = emote.Id;
                newEmote.imageScale = emote.ImageScale;
                newEmote.data = emote.ImageData;
                newEmote.name = emote.Name;
                newEmote.width = emote.Width / emote.ImageScale;
                newEmote.height = emote.Height / emote.ImageScale;
                chatRoot.embeddedData.thirdParty.Add(newEmote);
            }
            Console.WriteLine("Output third party emote count: " + chatRoot.embeddedData.thirdParty.Count);

            // Firstparty emotes
            if (chatRoot.embeddedData.firstParty == null || updateOptions.UpdateOldEmbeds)
            {
                chatRoot.embeddedData.firstParty = new List<EmbedEmoteData>();
            }
            Console.WriteLine("Input first party emote count: " + chatRoot.embeddedData.firstParty.Count);
            List<TwitchEmote> firstPartyEmotes = new List<TwitchEmote>();
            firstPartyEmotes = Task.Run(() => TwitchHelper.GetEmotes(chatRoot.comments, cacheFolder, embeddedData: chatRoot.embeddedData)).Result;
            foreach (TwitchEmote emote in firstPartyEmotes)
            {
                EmbedEmoteData newEmote = new EmbedEmoteData();
                newEmote.id = emote.Id;
                newEmote.imageScale = emote.ImageScale;
                newEmote.data = emote.ImageData;
                newEmote.width = emote.Width / emote.ImageScale;
                newEmote.height = emote.Height / emote.ImageScale;
                chatRoot.embeddedData.firstParty.Add(newEmote);
            }
            Console.WriteLine("Output third party emote count: " + chatRoot.embeddedData.firstParty.Count);

            // Twitch badges
            if (chatRoot.embeddedData.twitchBadges == null || updateOptions.UpdateOldEmbeds)
            {
                chatRoot.embeddedData.twitchBadges = new List<EmbedChatBadge>();
            }
            Console.WriteLine("Input twitch badge count: " + chatRoot.embeddedData.twitchBadges.Count);
            List<ChatBadge> twitchBadges = new List<ChatBadge>();
            twitchBadges = Task.Run(() => TwitchHelper.GetChatBadges(chatRoot.streamer.id, cacheFolder, embeddedData: chatRoot.embeddedData)).Result;
            foreach (ChatBadge badge in twitchBadges)
            {
                EmbedChatBadge newBadge = new EmbedChatBadge();
                newBadge.name = badge.Name;
                newBadge.versions = badge.VersionsData;
                chatRoot.embeddedData.twitchBadges.Add(newBadge);
            }
            Console.WriteLine("Output twitch badge count: " + chatRoot.embeddedData.twitchBadges.Count);

            // Twitch bits / cheers
            if (chatRoot.embeddedData.twitchBits == null || updateOptions.UpdateOldEmbeds)
            {
                chatRoot.embeddedData.twitchBits = new List<EmbedCheerEmote>();
            }
            Console.WriteLine("Input twitch bit count: " + chatRoot.embeddedData.twitchBits.Count);
            List<CheerEmote> twitchBits = new List<CheerEmote>();
            twitchBits = Task.Run(() => TwitchHelper.GetBits(cacheFolder, chatRoot.streamer.id.ToString(), embeddedData: chatRoot.embeddedData)).Result;
            foreach (CheerEmote bit in twitchBits)
            {
                EmbedCheerEmote newBit = new EmbedCheerEmote();
                newBit.prefix = bit.prefix;
                newBit.tierList = new Dictionary<int, EmbedEmoteData>();
                foreach (KeyValuePair<int, TwitchEmote> emotePair in bit.tierList)
                {
                    EmbedEmoteData newEmote = new EmbedEmoteData();
                    newEmote.id = emotePair.Value.Id;
                    newEmote.imageScale = emotePair.Value.ImageScale;
                    newEmote.data = emotePair.Value.ImageData;
                    newEmote.name = emotePair.Value.Name;
                    newEmote.width = emotePair.Value.Width / emotePair.Value.ImageScale;
                    newEmote.height = emotePair.Value.Height / emotePair.Value.ImageScale;
                    newBit.tierList.Add(emotePair.Key, newEmote);
                }
                chatRoot.embeddedData.twitchBits.Add(newBit);
            }
            Console.WriteLine("Input twitch bit count: " + chatRoot.embeddedData.twitchBits.Count);

            // Finally save the output to file!
            // TODO: maybe in the future we could also export as HTML here too?
            if (updateOptions.FileFormat == DownloadFormat.Json)
            {
                using (TextWriter writer = File.CreateText(updateOptions.OutputFile))
                {
                    var serializer = new Newtonsoft.Json.JsonSerializer();
                    serializer.Serialize(writer, chatRoot);
                }
            }

            // Clear our working directory, it's highly unlikely we would reuse it anyways
            if (Directory.Exists(cacheFolder))
                Directory.Delete(cacheFolder, true);
        }

        public async Task<ChatRoot> ParseJson()
        {
            chatRoot = await ChatJsonParser.ParseJsonStatic(updateOptions.InputFile);

            if (chatRoot.streamer == null)
            {
                chatRoot.streamer = new Streamer();
                chatRoot.streamer.id = int.Parse(chatRoot.comments.First().channel_id);
                chatRoot.streamer.name = await TwitchHelper.GetStreamerName(chatRoot.streamer.id);
            }

            return chatRoot;
        }
    }
}
