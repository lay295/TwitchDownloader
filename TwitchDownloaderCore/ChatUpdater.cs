using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public sealed class ChatUpdater
    {
        public ChatRoot chatRoot { get; internal set; } = new();

        private readonly ChatUpdateOptions _updateOptions = new();

        public ChatUpdater(ChatUpdateOptions updateOptions)
        {
            _updateOptions = updateOptions;
            _updateOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(_updateOptions.TempFolder) ? Path.GetTempPath() : _updateOptions.TempFolder,
                "TwitchDownloader");
        }

        internal static class SharedObjects
        {
            internal static object CropChatRootLock = new();
        }

        public async Task UpdateAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            chatRoot.FileInfo = new() { Version = ChatRootVersion.CurrentVersion, CreatedAt = chatRoot.FileInfo.CreatedAt, UpdatedAt = DateTime.Now };
            if (Path.GetExtension(_updateOptions.InputFile).ToLower() != ".json")
            {
                throw new NotImplementedException("Only JSON chat files can be used as update input. HTML support may come in the future.");
            }

            // Dynamic step count setup
            int currentStep = 0;
            int totalSteps = 1;
            if (_updateOptions.CropBeginning || _updateOptions.CropEnding) totalSteps++;
            if (_updateOptions.EmbedMissing || _updateOptions.ReplaceEmbeds) totalSteps++;

            // If we are editing the chat crop
            if (_updateOptions.CropBeginning || _updateOptions.CropEnding)
            {
                progress.Report(new ProgressReport(ReportType.SameLineStatus, string.Format("Updating Chat Crop [{0}/{1}]", ++currentStep, totalSteps)));
                progress.Report(new ProgressReport(totalSteps / currentStep));

                chatRoot.video ??= new Video();

                bool cropTaskVodExpired = false;
                var cropTaskProgress = new Progress<ProgressReport>(report =>
                {
                    if (report.Data.ToString().ToLower().Contains("vod is expired"))
                    {
                        // If the user is moving both crops in one command, we only want to propagate a 'vod expired/id corrupt' report once 
                        if (cropTaskVodExpired)
                        {
                            return;
                        }
                        cropTaskVodExpired = true;
                    }
                    progress.Report(report);
                });

                int inputCommentCount = chatRoot.comments.Count;

                List<Task> chatCropTasks = new List<Task>
                {
                    ChatBeginningCropTask(cropTaskProgress, cancellationToken),
                    ChatEndingCropTask(cropTaskProgress, cancellationToken)
                };

                await Task.WhenAll(chatCropTasks);
                cancellationToken.ThrowIfCancellationRequested();

                // If the comment count didn't change, it probably failed so don't report the counts
                if (inputCommentCount != chatRoot.comments.Count)
                {
                    progress.Report(new ProgressReport(ReportType.Log, string.Format("Input comment count: {0}. Output count: {1}", inputCommentCount, chatRoot.comments.Count)));
                }
            }

            // If we are updating/replacing embeds
            if (_updateOptions.EmbedMissing || _updateOptions.ReplaceEmbeds)
            {
                progress.Report(new ProgressReport(ReportType.NewLineStatus, string.Format("Updating Embeds [{0}/{1}]", ++currentStep, totalSteps)));
                progress.Report(new ProgressReport(totalSteps / currentStep));

                chatRoot.embeddedData ??= new EmbeddedData();

                List<Task> embedTasks = new List<Task>
                {
                    FirstPartyEmoteTask(progress),
                    ThirdPartyEmoteTask(progress),
                    ChatBadgeTask(progress),
                    BitTask(progress)
                };

                await Task.WhenAll(embedTasks);
                cancellationToken.ThrowIfCancellationRequested();

            }

            // Finally save the output to file!
            progress.Report(new ProgressReport(ReportType.NewLineStatus, string.Format("Writing Output File [{0}/{1}]", ++currentStep, totalSteps)));
            progress.Report(new ProgressReport(totalSteps / currentStep));

            switch (_updateOptions.OutputFormat)
            {
                case ChatFormat.Json:
                    await ChatJson.SerializeAsync(_updateOptions.OutputFile, chatRoot, _updateOptions.Compression, cancellationToken);
                    break;
                case ChatFormat.Html:
                    await ChatHtml.SerializeAsync(_updateOptions.OutputFile, chatRoot, chatRoot.embeddedData != null, cancellationToken);
                    break;
                case ChatFormat.Text:
                    await ChatText.SerializeAsync(_updateOptions.OutputFile, chatRoot, _updateOptions.TextTimestampFormat);
                    break;
                default:
                    throw new NotImplementedException("Requested output chat format is not implemented");
            }
        }

        private async Task FirstPartyEmoteTask(IProgress<ProgressReport> progress = null)
        {
            List<TwitchEmote> firstPartyEmoteList = await TwitchHelper.GetEmotes(chatRoot.comments, _updateOptions.TempFolder, _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData);

            int inputCount = chatRoot.embeddedData.firstParty.Count;
            chatRoot.embeddedData.firstParty = new List<EmbedEmoteData>();
            foreach (TwitchEmote emote in firstPartyEmoteList)
            {
                EmbedEmoteData newEmote = new EmbedEmoteData();
                newEmote.id = emote.Id;
                newEmote.imageScale = emote.ImageScale;
                newEmote.data = emote.ImageData;
                newEmote.width = emote.Width / emote.ImageScale;
                newEmote.height = emote.Height / emote.ImageScale;
                chatRoot.embeddedData.firstParty.Add(newEmote);
            }
            progress?.Report(new ProgressReport(ReportType.Log, string.Format("Input 1st party emote count: {0}. Output count: {1}", inputCount, chatRoot.embeddedData.firstParty.Count)));
        }

        private async Task ThirdPartyEmoteTask(IProgress<ProgressReport> progress = null)
        {
            List<TwitchEmote> thirdPartyEmoteList = await TwitchHelper.GetThirdPartyEmotes(chatRoot.streamer.id, _updateOptions.TempFolder, _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData, _updateOptions.BttvEmotes, _updateOptions.FfzEmotes, _updateOptions.StvEmotes);

            int inputCount = chatRoot.embeddedData.thirdParty.Count;
            chatRoot.embeddedData.thirdParty = new List<EmbedEmoteData>();
            foreach (TwitchEmote emote in thirdPartyEmoteList)
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
            progress?.Report(new ProgressReport(ReportType.Log, string.Format("Input 3rd party emote count: {0}. Output count: {1}", inputCount, chatRoot.embeddedData.thirdParty.Count)));
        }

        private async Task ChatBadgeTask(IProgress<ProgressReport> progress = null)
        {
            List<ChatBadge> badgeList = await TwitchHelper.GetChatBadges(chatRoot.streamer.id, _updateOptions.TempFolder, _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData);

            int inputCount = chatRoot.embeddedData.twitchBadges.Count;
            chatRoot.embeddedData.twitchBadges = new List<EmbedChatBadge>();
            foreach (ChatBadge badge in badgeList)
            {
                EmbedChatBadge newBadge = new EmbedChatBadge();
                newBadge.name = badge.Name;
                newBadge.versions = badge.VersionsData;
                chatRoot.embeddedData.twitchBadges.Add(newBadge);
            }
            progress?.Report(new ProgressReport(ReportType.Log, string.Format("Input badge count: {0}. Output count: {1}", inputCount, chatRoot.embeddedData.twitchBadges.Count)));
        }

        private async Task BitTask(IProgress<ProgressReport> progress = null)
        {
            List<CheerEmote> bitList = await TwitchHelper.GetBits(_updateOptions.TempFolder, chatRoot.streamer.id.ToString(), _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData);

            int inputCount = chatRoot.embeddedData.twitchBits.Count;
            chatRoot.embeddedData.twitchBits = new List<EmbedCheerEmote>();
            foreach (CheerEmote bit in bitList)
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
            progress?.Report(new ProgressReport(ReportType.Log, string.Format("Input bit emote count: {0}. Output count: {1}", inputCount, chatRoot.embeddedData.twitchBits.Count)));
        }

        private async Task ChatBeginningCropTask(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            if (!_updateOptions.CropBeginning)
            {
                return;
            }

            string tempFile = Path.Combine(_updateOptions.TempFolder, Path.GetRandomFileName());

            try
            {
                // Only download missing comments if new start crop is less than old start crop
                if (_updateOptions.CropBeginningTime < chatRoot.video.start)
                {
                    ChatDownloadOptions downloadOptions = GetCropDownloadOptions(chatRoot.video.id, tempFile, _updateOptions.CropBeginningTime, chatRoot.video.start);
                    await AppendCommentSection(downloadOptions, tempFile, cancellationToken);
                }
            }
            catch (NullReferenceException)
            {
                progress.Report(new ProgressReport(ReportType.Log, "Unable to fetch possible missing comments: source VOD is expired or embedded ID is corrupt"));
            }

            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            // Adjust the crop parameter
            double beginningCropClamp = double.IsNegative(chatRoot.video.length) ? 172_800 : chatRoot.video.length; // Get length from chatroot or if negavite (N/A) max vod length (48 hours) in seconds. https://help.twitch.tv/s/article/broadcast-guidelines
            chatRoot.video.start = Math.Min(Math.Max(_updateOptions.CropBeginningTime, 0.0), beginningCropClamp);
        }

        private async Task ChatEndingCropTask(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            if (!_updateOptions.CropEnding)
            {
                return;
            }

            string tempFile = Path.Combine(_updateOptions.TempFolder, Path.GetRandomFileName());

            try
            {
                // Only download missing comments if new end crop is greater than old end crop
                if (_updateOptions.CropEndingTime > chatRoot.video.end)
                {
                    ChatDownloadOptions downloadOptions = GetCropDownloadOptions(chatRoot.video.id, tempFile, chatRoot.video.end, _updateOptions.CropEndingTime);
                    await AppendCommentSection(downloadOptions, tempFile, cancellationToken);
                }
            }
            catch (NullReferenceException)
            {
                progress.Report(new ProgressReport(ReportType.Log, "Unable to fetch possible missing comments: source VOD is expired or embedded ID is corrupt"));
            }

            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            // Adjust the crop parameter
            double endingCropClamp = double.IsNegative(chatRoot.video.length) ? 172_800 : chatRoot.video.length; // Get length from chatroot or if negavite (N/A) max vod length (48 hours) in seconds. https://help.twitch.tv/s/article/broadcast-guidelines
            chatRoot.video.end = Math.Min(Math.Max(_updateOptions.CropEndingTime, 0.0), endingCropClamp);
        }

        private async Task AppendCommentSection(ChatDownloadOptions downloadOptions, string inputFile, CancellationToken cancellationToken = new())
        {
            ChatDownloader chatDownloader = new ChatDownloader(downloadOptions);
            await chatDownloader.DownloadAsync(new Progress<ProgressReport>(), cancellationToken);

            ChatRoot newChatRoot = await ChatJson.DeserializeAsync(inputFile, getComments: true, getEmbeds: false, cancellationToken);

            // Append the new comment section
            SortedSet<Comment> commentsSet = new SortedSet<Comment>(new SortedCommentComparer());
            foreach (var comment in newChatRoot.comments)
            {
                if (comment.content_offset_seconds < downloadOptions.CropEndingTime && comment.content_offset_seconds >= downloadOptions.CropBeginningTime)
                {
                    commentsSet.Add(comment);
                }
            }

            lock (SharedObjects.CropChatRootLock)
            {
                foreach (var comment in chatRoot.comments)
                {
                    commentsSet.Add(comment);
                }

                List<Comment> comments = commentsSet.DistinctBy(x => x._id).ToList();
                commentsSet.Clear();

                chatRoot.comments = comments;
            }
        }

        private ChatDownloadOptions GetCropDownloadOptions(string videoId, string tempFile, double sectionStart, double sectionEnd)
        {
            return new ChatDownloadOptions()
            {
                Id = videoId,
                DownloadFormat = ChatFormat.Json, // json is required to parse as a new chatroot object
                Filename = tempFile,
                CropBeginning = true,
                CropBeginningTime = sectionStart,
                CropEnding = true,
                CropEndingTime = sectionEnd,
                ConnectionCount = 4,
                EmbedData = false,
                BttvEmotes = false,
                FfzEmotes = false,
                StvEmotes = false,
                TempFolder = _updateOptions.TempFolder
            };
        }

        public async Task<ChatRoot> ParseJsonAsync(CancellationToken cancellationToken = new())
        {
            chatRoot = await ChatJson.DeserializeAsync(_updateOptions.InputFile, true, true, cancellationToken);

            chatRoot.streamer ??= new Streamer
            {
                id = int.Parse(chatRoot.comments.First().channel_id),
                name = await TwitchHelper.GetStreamerName(int.Parse(chatRoot.comments.First().channel_id))
            };

            return chatRoot;
        }
    }
}
