using Newtonsoft.Json;
using SkiaSharp;
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
        public ChatRoot chatRoot = new();
        private readonly ChatUpdateOptions updateOptions = new();
        private List<TwitchEmote> firstPartyEmoteList = new();
        private List<TwitchEmote> thirdPartyEmoteList = new();
        private List<ChatBadge> badgeList = new();
        private List<CheerEmote> bitList = new();

        public ChatUpdater(ChatUpdateOptions UpdateOptions)
        {
            updateOptions = UpdateOptions;
            updateOptions.TempFolder = Path.Combine(string.IsNullOrWhiteSpace(updateOptions.TempFolder) ? Path.GetTempPath() : updateOptions.TempFolder, "TwitchDownloader");
        }

        public async Task UpdateAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            GetDataToEmbed().Wait(cancellationToken);

            chatRoot.embeddedData ??= new EmbeddedData();

            // Firstparty emotes
            if (chatRoot.embeddedData.firstParty == null || updateOptions.UpdateOldEmbeds)
            {
                chatRoot.embeddedData.firstParty = new List<EmbedEmoteData>();
            }
            int inputCount = chatRoot.embeddedData.firstParty.Count;
            foreach (TwitchEmote emote in firstPartyEmoteList)
            {
                if (chatRoot.embeddedData.firstParty.Any(x => x.id.Equals(emote.Id)))
                    continue;

                EmbedEmoteData newEmote = new EmbedEmoteData();
                newEmote.id = emote.Id;
                newEmote.imageScale = emote.ImageScale;
                newEmote.data = emote.ImageData;
                newEmote.width = emote.Width / emote.ImageScale;
                newEmote.height = emote.Height / emote.ImageScale;
                chatRoot.embeddedData.firstParty.Add(newEmote);
            }
            progress.Report(new ProgressReport() { reportType = ReportType.Message, data = string.Format("Input firsty party emote count: {0}. Output count: {1}", inputCount, chatRoot.embeddedData.firstParty.Count) });

            // Thirdparty emotes
            if (chatRoot.embeddedData.thirdParty == null || updateOptions.UpdateOldEmbeds)
            {
                chatRoot.embeddedData.thirdParty = new List<EmbedEmoteData>();
            }
            inputCount = chatRoot.embeddedData.thirdParty.Count;
            foreach (TwitchEmote emote in thirdPartyEmoteList)
            {
                if (chatRoot.embeddedData.thirdParty.Any(x => x.id.Equals(emote.Id)))
                    continue;

                EmbedEmoteData newEmote = new EmbedEmoteData();
                newEmote.id = emote.Id;
                newEmote.imageScale = emote.ImageScale;
                newEmote.data = emote.ImageData;
                newEmote.name = emote.Name;
                newEmote.width = emote.Width / emote.ImageScale;
                newEmote.height = emote.Height / emote.ImageScale;
                chatRoot.embeddedData.thirdParty.Add(newEmote);
            }
            progress.Report(new ProgressReport() { reportType = ReportType.Message, data = string.Format("Input third party emote count: {0}. Output count: {1}", inputCount, chatRoot.embeddedData.thirdParty.Count) });

            // Twitch badges
            if (chatRoot.embeddedData.twitchBadges == null || updateOptions.UpdateOldEmbeds)
            {
                chatRoot.embeddedData.twitchBadges = new List<EmbedChatBadge>();
            }
            inputCount = chatRoot.embeddedData.twitchBadges.Count;
            foreach (ChatBadge badge in badgeList)
            {
                if (chatRoot.embeddedData.twitchBadges.Any(x => x.name.Equals(badge.Name)))
                    continue;

                EmbedChatBadge newBadge = new EmbedChatBadge();
                newBadge.name = badge.Name;
                newBadge.versions = badge.VersionsData;
                chatRoot.embeddedData.twitchBadges.Add(newBadge);
            }
            progress.Report(new ProgressReport() { reportType = ReportType.Message, data = string.Format("Input badge count: {0}. Output count: {1}", inputCount, chatRoot.embeddedData.twitchBadges.Count) });

            // Twitch bits / cheers
            if (chatRoot.embeddedData.twitchBits == null || updateOptions.UpdateOldEmbeds)
            {
                chatRoot.embeddedData.twitchBits = new List<EmbedCheerEmote>();
            }
            inputCount = chatRoot.embeddedData.twitchBits.Count;
            foreach (CheerEmote bit in bitList)
            {
                if (!chatRoot.embeddedData.twitchBits.Any(x => x.prefix.Equals(bit.prefix)))
                    continue;

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
            progress.Report(new ProgressReport() { reportType = ReportType.Message, data = string.Format("Input cheermote emote count: {0}. Output count: {1}", inputCount, chatRoot.embeddedData.twitchBits.Count) });

            // Finally save the output to file!
            // TODO: maybe in the future we could also export as HTML here too?
            if (updateOptions.FileFormat == DownloadFormat.Json)
            {
                using (TextWriter writer = File.CreateText(updateOptions.OutputFile))
                {
                    var serializer = new JsonSerializer();
                    serializer.Serialize(writer, chatRoot);
                }
            }
        }

        public async Task GetDataToEmbed()
        {
            Task<List<TwitchEmote>> emoteTask = Task.Run(() => TwitchHelper.GetEmotes(chatRoot.comments, updateOptions.TempFolder, chatRoot.embeddedData));
            Task<List<TwitchEmote>> emoteThirdTask = Task.Run(() => TwitchHelper.GetThirdPartyEmotes(chatRoot.streamer.id, updateOptions.TempFolder, chatRoot.embeddedData, updateOptions.BttvEmotes, updateOptions.FfzEmotes, updateOptions.StvEmotes));
            Task<List<ChatBadge>> badgeTask = Task.Run(() => TwitchHelper.GetChatBadges(chatRoot.streamer.id, updateOptions.TempFolder, chatRoot.embeddedData));
            Task<List<CheerEmote>> bitTask = Task.Run(() => TwitchHelper.GetBits(updateOptions.TempFolder, chatRoot.streamer.id.ToString(), chatRoot.embeddedData));

            await Task.WhenAll(emoteTask, emoteThirdTask, badgeTask, bitTask);

            firstPartyEmoteList = emoteTask.Result;
            thirdPartyEmoteList = emoteThirdTask.Result;
            badgeList = badgeTask.Result;
            bitList = bitTask.Result;
        }

        public async Task<ChatRoot> ParseJsonAsync()
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
