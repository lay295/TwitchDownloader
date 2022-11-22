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

                chatRoot.embeddedData ??= new EmbeddedData();

                List<Task> embedTasks = new List<Task>
                {
                    FirstPartyEmoteTask(progress),
                    ThirdPartyEmoteTask(progress),
                    ChatBadgeTask(progress),
                    CheermoteBadgeTask(progress)
                };

                await Task.WhenAll(embedTasks);
            }

            // If we are editing the chat crop
            if (updateOptions.CropBeginning || updateOptions.CropEnding)
            {
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Updating Chat Crop" });

                chatRoot.video ??= new VideoTime();

                bool cropTaskVodExpired = false;
                var cropTaskProgress = new Progress<ProgressReport>(report =>
                {
                    if (report.data.ToString().ToLower().Contains("vod is expired"))
                    {
                        // If the user is moving both crops at once, we only want to propagate this report once 
                        if (cropTaskVodExpired)
                        {
                            return;
                        }
                        cropTaskVodExpired = true;
                        progress.Report(report);
                    }
                    else
                    {
                        progress.Report(report);
                    }
                });

                // TODO: uncomment fetching video id from json (requires https://github.com/lay295/TwitchDownloader/pull/440)
                List<Task> chatCropTasks = new List<Task>
                {
                    ChatBeginningCropTask(cropTaskProgress, cancellationToken),
                    ChatEndingCropTask(cropTaskProgress, cancellationToken)
                };

                await Task.WhenAll(chatCropTasks);
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

        private async Task FirstPartyEmoteTask(IProgress<ProgressReport> progress)
        {
            firstPartyEmoteList = await TwitchHelper.GetEmotes(chatRoot.comments, updateOptions.TempFolder, chatRoot.embeddedData);

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
        }

        private async Task ThirdPartyEmoteTask(IProgress<ProgressReport> progress)
        {
            thirdPartyEmoteList = await TwitchHelper.GetThirdPartyEmotes(chatRoot.streamer.id, updateOptions.TempFolder, chatRoot.embeddedData, updateOptions.BttvEmotes, updateOptions.FfzEmotes, updateOptions.StvEmotes);

            if (chatRoot.embeddedData.thirdParty == null || updateOptions.ReplaceEmbeds)
            {
                chatRoot.embeddedData.thirdParty = new List<EmbedEmoteData>();
            }
            int inputCount = chatRoot.embeddedData.thirdParty.Count;
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
        }

        private async Task ChatBadgeTask(IProgress<ProgressReport> progress)
        {
            badgeList = await TwitchHelper.GetChatBadges(chatRoot.streamer.id, updateOptions.TempFolder, chatRoot.embeddedData);

            if (chatRoot.embeddedData.twitchBadges == null || updateOptions.ReplaceEmbeds)
            {
                chatRoot.embeddedData.twitchBadges = new List<EmbedChatBadge>();
            }
            int inputCount = chatRoot.embeddedData.twitchBadges.Count;
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
        }

        private async Task CheermoteBadgeTask(IProgress<ProgressReport> progress)
        {
            bitList = await TwitchHelper.GetBits(updateOptions.TempFolder, chatRoot.streamer.id.ToString(), chatRoot.embeddedData);

            if (chatRoot.embeddedData.twitchBits == null || updateOptions.ReplaceEmbeds)
            {
                chatRoot.embeddedData.twitchBits = new List<EmbedCheerEmote>();
            }
            int inputCount = chatRoot.embeddedData.twitchBits.Count;
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

        private async Task ChatBeginningCropTask(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            if (!updateOptions.CropBeginning)
            {
                return;
            }

            string tempFile = Path.Combine(updateOptions.TempFolder, Path.GetRandomFileName());
            ChatDownloadOptions downloadOptions = new ChatDownloadOptions()
            {
                Id = null,
                DownloadFormat = updateOptions.FileFormat,
                Filename = tempFile,
                ConnectionCount = 4,
                EmbedData = false,
                BttvEmotes = false,
                FfzEmotes = false,
                StvEmotes = false,
                TempFolder = null
            };

            //if (chatRoot.video.id != null)
            //{
            //    downloadOptions.Id == chatRoot.video.id;
            //}

            try
            {
                // Only download missing comments if new start crop is less than old start crop
                // TODO: extract newly aquired comments from tempfile into chatRoot
                if (downloadOptions.Id != null && updateOptions.CropBeginningTime < chatRoot.video.start)
                {
                    downloadOptions.CropBeginning = true;
                    downloadOptions.CropBeginningTime = updateOptions.CropBeginningTime;
                    downloadOptions.CropEnding = true;
                    downloadOptions.CropEndingTime = chatRoot.video.start;
                    ChatDownloader chatDownloader = new(downloadOptions);
                    await chatDownloader.DownloadAsync(progress, cancellationToken);
                }
            }
            catch (NullReferenceException)
            {
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Unable to fetch possible missing comments: source VOD is expired or embedded ID is corrupt" });
            }

            // Adjust the crop parameter
            chatRoot.video.start = Math.Max(updateOptions.CropBeginningTime, 0.0);

            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        private async Task ChatEndingCropTask(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            if (!updateOptions.CropEnding)
            {
                return;
            }

            string tempFile = Path.Combine(updateOptions.TempFolder, Path.GetRandomFileName());
            ChatDownloadOptions downloadOptions = new ChatDownloadOptions()
            {
                Id = null,
                DownloadFormat = updateOptions.FileFormat,
                Filename = tempFile,
                ConnectionCount = 4,
                EmbedData = false,
                BttvEmotes = false,
                FfzEmotes = false,
                StvEmotes = false,
                TempFolder = null
            };

            //if (chatRoot.video.id != null)
            //{
            //    downloadOptions.Id == chatRoot.video.id;
            //}

            try
            {
                // Only download missing comments if new end crop is greater than old end crop
                // TODO: extract newly aquired comments from tempfile into chatRoot
                if (downloadOptions.Id != null && updateOptions.CropEndingTime > chatRoot.video.end)
                {
                    downloadOptions.CropBeginning = true;
                    downloadOptions.CropBeginningTime = chatRoot.video.end;
                    downloadOptions.CropEnding = true;
                    downloadOptions.CropEndingTime = updateOptions.CropEndingTime;
                    ChatDownloader chatDownloader = new(downloadOptions);
                    await chatDownloader.DownloadAsync(progress, cancellationToken);
                }
            }
            catch (NullReferenceException)
            {
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Unable to fetch possible missing comments: source VOD is expired or embedded ID is corrupt" });
            }

            // Adjust the crop parameter
            double endingCropClamp = 172_800; // max vod length (48 hours) in seconds. https://help.twitch.tv/s/article/broadcast-guidelines
            //if (chatRoot.video.length != null)
            //{
            //    endingCropClamp = chatRoot.video.length;
            //}
            chatRoot.video.end = Math.Min(updateOptions.CropEndingTime, endingCropClamp);

            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
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
