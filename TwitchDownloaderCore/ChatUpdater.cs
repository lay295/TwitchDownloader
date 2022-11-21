using Newtonsoft.Json;
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
            // If we are updating/replacing embeds
            if (updateOptions.EmbedMissing || updateOptions.ReplaceEmbeds)
            {
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Fetching Images" });
                GetDataToEmbed().Wait(cancellationToken);

                chatRoot.embeddedData ??= new EmbeddedData();

                // Firstparty emotes
                if (chatRoot.embeddedData.firstParty == null || updateOptions.ReplaceEmbeds)
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
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = string.Format("Input first party emote count: {0}. Output count: {1}", inputCount, chatRoot.embeddedData.firstParty.Count) });

                // Thirdparty emotes
                if (chatRoot.embeddedData.thirdParty == null || updateOptions.ReplaceEmbeds)
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
                if (chatRoot.embeddedData.twitchBadges == null || updateOptions.ReplaceEmbeds)
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
                if (chatRoot.embeddedData.twitchBits == null || updateOptions.ReplaceEmbeds)
                {
                    chatRoot.embeddedData.twitchBits = new List<EmbedCheerEmote>();
                }
                inputCount = chatRoot.embeddedData.twitchBits.Count;
                foreach (CheerEmote bit in bitList)
                {
                    if (chatRoot.embeddedData.twitchBits.Any(x => x.prefix.Equals(bit.prefix)))
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
            }

            // If we are editing the chat crop
            if (updateOptions.CropBeginning || updateOptions.CropEnding)
            {
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Updating Chat Crop" });

                chatRoot.video ??= new VideoTime();

                string tempFile = Path.Combine(updateOptions.TempFolder, Path.GetRandomFileName());
                ChatDownloadOptions downloadOptions = new ChatDownloadOptions()
                {
                    Id = null,
                    DownloadFormat = updateOptions.FileFormat,
                    EmbedData = false,
                    Filename = tempFile,
                    ConnectionCount = 4,
                    BttvEmotes = false,
                    FfzEmotes = false,
                    StvEmotes = false,
                    TempFolder = null
                };

                // TODO: uncomment fetching additional video info from json (requires https://github.com/lay295/TwitchDownloader/pull/440)
                // TODO: extract newly aquired comments from tempfile into chatRoot
                //if (chatRoot.video.id != null)
                //{
                //    downloadOptions.Id == chatRoot.video.id;
                //}

                try
                {
                    // Only download missing comments if new start crop is less than old start crop
                    if (downloadOptions.Id != null && updateOptions.CropBeginning && updateOptions.CropBeginningTime < chatRoot.video.start)
                    {
                        downloadOptions.CropBeginning = true;
                        downloadOptions.CropBeginningTime = updateOptions.CropBeginningTime;
                        downloadOptions.CropEnding = true;
                        downloadOptions.CropEndingTime = chatRoot.video.start;
                        ChatDownloader chatDownloader = new(downloadOptions);
                        chatDownloader.DownloadAsync(progress, cancellationToken).Wait();
                    }

                    // Only download missing comments if new end crop is greater than old end crop
                    if (downloadOptions.Id != null && updateOptions.CropEnding && updateOptions.CropEndingTime > chatRoot.video.end)
                    {
                        downloadOptions.CropBeginning = true;
                        downloadOptions.CropBeginningTime = chatRoot.video.end;
                        downloadOptions.CropEnding = true;
                        downloadOptions.CropEndingTime = updateOptions.CropEndingTime;
                        ChatDownloader chatDownloader = new(downloadOptions);
                        chatDownloader.DownloadAsync(progress, cancellationToken).Wait();
                    }
                }
                catch (NullReferenceException ex) when (ex.ToString().ToLower().Contains("expired"))
                {
                    progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Source VOD is expired, unable to fetch possible missing comments" });
                }

                // Finally adjust the crop parameters
                if (updateOptions.CropBeginning)
                {
                    chatRoot.video.start = Math.Max(updateOptions.CropBeginningTime, 0.0);
                }
                if (updateOptions.CropEnding)
                {
                    double endingCropClamp = 172_800; // max vod length (48 hours) in seconds. https://help.twitch.tv/s/article/broadcast-guidelines
                    //if (chatRoot.video.length != null)
                    //{
                    //    endingCropClamp = chatRoot.video.length;
                    //}
                    chatRoot.video.end = Math.Min(updateOptions.CropEndingTime, endingCropClamp);
                }
            }

            // Finally save the output to file!
            // TODO: maybe in the future we could also export as HTML here too?
            if (updateOptions.FileFormat == DownloadFormat.Json)
            {
                using TextWriter writer = File.CreateText(updateOptions.OutputFile);
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(writer, chatRoot);
            }
        }

        private async Task GetDataToEmbed()
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
            chatRoot = await ChatJsonParser.ParseJsonAsync(updateOptions.InputFile);

            chatRoot.streamer ??= new Streamer
            {
                id = int.Parse(chatRoot.comments.First().channel_id),
                name = await TwitchHelper.GetStreamerName(int.Parse(chatRoot.comments.First().channel_id))
            };

            return chatRoot;
        }
    }
}
