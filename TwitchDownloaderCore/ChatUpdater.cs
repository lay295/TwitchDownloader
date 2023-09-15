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

        private readonly ChatUpdateOptions _updateOptions;

        public ChatUpdater(ChatUpdateOptions updateOptions)
        {
            _updateOptions = updateOptions;
            _updateOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(_updateOptions.TempFolder) ? Path.GetTempPath() : _updateOptions.TempFolder,
                "TwitchDownloader");
        }

        private static class SharedObjects
        {
            internal static object CropChatRootLock = new();
        }

        public async Task UpdateAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            chatRoot.FileInfo = new() { Version = ChatRootVersion.CurrentVersion, CreatedAt = chatRoot.FileInfo.CreatedAt, UpdatedAt = DateTime.Now };
            if (!Path.GetExtension(_updateOptions.InputFile.Replace(".gz", ""))!.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Only JSON chat files can be used as update input. HTML support may come in the future.");
            }

            // Dynamic step count setup
            int currentStep = 0;
            int totalSteps = 1;
            if (_updateOptions.CropBeginning || _updateOptions.CropEnding) totalSteps++;
            if (_updateOptions.EmbedMissing || _updateOptions.ReplaceEmbeds) totalSteps++;

            // If we are editing the chat crop
            if (_updateOptions.CropBeginning || _updateOptions.CropEnding)
            {
                currentStep++;
                await UpdateChatCrop(totalSteps, currentStep, progress, cancellationToken);
            }

            // If we are updating/replacing embeds
            if (_updateOptions.EmbedMissing || _updateOptions.ReplaceEmbeds)
            {
                currentStep++;
                await UpdateEmbeds(currentStep, totalSteps, progress, cancellationToken);
            }

            // Finally save the output to file!
            progress.Report(new ProgressReport(ReportType.NewLineStatus, $"Writing Output File [{++currentStep}/{totalSteps}]"));
            progress.Report(new ProgressReport(totalSteps / currentStep));

            switch (_updateOptions.OutputFormat)
            {
                case ChatFormat.Json:
                    await ChatJson.SerializeAsync(_updateOptions.OutputFile, chatRoot, _updateOptions.Compression, cancellationToken);
                    break;
                case ChatFormat.Html:
                    await ChatHtml.SerializeAsync(_updateOptions.OutputFile, chatRoot, chatRoot.embeddedData != null && (chatRoot.embeddedData.firstParty?.Count > 0 || chatRoot.embeddedData.twitchBadges?.Count > 0), cancellationToken);
                    break; // If there is embedded data, it's almost guaranteed to be first party emotes or badges.
                case ChatFormat.Text:
                    await ChatText.SerializeAsync(_updateOptions.OutputFile, chatRoot, _updateOptions.TextTimestampFormat);
                    break;
                default:
                    throw new NotSupportedException($"{_updateOptions.OutputFormat} is not a supported output format.");
            }
        }

        private async Task UpdateChatCrop(int totalSteps, int currentStep, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            progress.Report(new ProgressReport(ReportType.SameLineStatus, $"Updating Chat Crop [{currentStep}/{totalSteps}]"));
            progress.Report(new ProgressReport(totalSteps / currentStep));

            chatRoot.video ??= new Video();

            bool cropTaskVodExpired = false;
            var cropTaskProgress = new Progress<ProgressReport>(report =>
            {
                if (((string)report.Data).ToLower().Contains("vod is expired"))
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

            var chatCropTasks = new[]
            {
                ChatBeginningCropTask(cropTaskProgress, cancellationToken),
                ChatEndingCropTask(cropTaskProgress, cancellationToken)
            };

            await Task.WhenAll(chatCropTasks);
            cancellationToken.ThrowIfCancellationRequested();

            // If the output format is not JSON, the user probably wants to remove comments outside of the crop zone
            if (_updateOptions.OutputFormat != ChatFormat.Json)
            {
                if (_updateOptions.CropBeginning)
                {
                    var startIndex = chatRoot.comments.FindLastIndex(c => c.content_offset_seconds < _updateOptions.CropBeginningTime);
                    if (startIndex != -1)
                    {
                        chatRoot.comments.RemoveRange(0, startIndex + 1);
                    }
                }

                if (_updateOptions.CropEnding)
                {
                    var endIndex = chatRoot.comments.FindLastIndex(c => c.content_offset_seconds <= _updateOptions.CropEndingTime + 1);
                    if (endIndex != -1)
                    {
                        chatRoot.comments.RemoveRange(endIndex, chatRoot.comments.Count - endIndex);
                    }
                }
            }

            // If the comment count didn't change, it probably failed so don't report the counts
            if (inputCommentCount != chatRoot.comments.Count)
            {
                progress.Report(new ProgressReport(ReportType.Log, $"Input comment count: {inputCommentCount}. Output count: {chatRoot.comments.Count}"));
            }
        }

        private async Task UpdateEmbeds(int currentStep, int totalSteps, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            progress.Report(new ProgressReport(ReportType.NewLineStatus, $"Updating Embeds [{currentStep}/{totalSteps}]"));
            progress.Report(new ProgressReport(totalSteps / currentStep));

            chatRoot.embeddedData ??= new EmbeddedData();

            var embedTasks = new[]
            {
                Task.Run(() => FirstPartyEmoteTask(progress, cancellationToken), cancellationToken),
                Task.Run(() => ThirdPartyEmoteTask(progress, cancellationToken), cancellationToken),
                Task.Run(() => ChatBadgeTask(progress, cancellationToken), cancellationToken),
                Task.Run(() => BitTask(progress, cancellationToken), cancellationToken),
            };

            await Task.WhenAll(embedTasks);
        }

        private async Task FirstPartyEmoteTask(IProgress<ProgressReport> progress = null, CancellationToken cancellationToken = default)
        {
            List<TwitchEmote> firstPartyEmoteList = await TwitchHelper.GetEmotes(chatRoot.comments, _updateOptions.TempFolder, _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData, cancellationToken: cancellationToken);

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
            progress?.Report(new ProgressReport(ReportType.Log, $"Input 1st party emote count: {inputCount}. Output count: {chatRoot.embeddedData.firstParty.Count}"));
        }

        private async Task ThirdPartyEmoteTask(IProgress<ProgressReport> progress = null, CancellationToken cancellationToken = default)
        {
            List<TwitchEmote> thirdPartyEmoteList = await TwitchHelper.GetThirdPartyEmotes(chatRoot.comments, chatRoot.streamer.id, _updateOptions.TempFolder, _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData, _updateOptions.BttvEmotes, _updateOptions.FfzEmotes, _updateOptions.StvEmotes, cancellationToken: cancellationToken);

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
            progress?.Report(new ProgressReport(ReportType.Log, $"Input 3rd party emote count: {inputCount}. Output count: {chatRoot.embeddedData.thirdParty.Count}"));
        }

        private async Task ChatBadgeTask(IProgress<ProgressReport> progress = null, CancellationToken cancellationToken = default)
        {
            List<ChatBadge> badgeList = await TwitchHelper.GetChatBadges(chatRoot.comments, chatRoot.streamer.id, _updateOptions.TempFolder, _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData, cancellationToken: cancellationToken);

            int inputCount = chatRoot.embeddedData.twitchBadges.Count;
            chatRoot.embeddedData.twitchBadges = new List<EmbedChatBadge>();
            foreach (ChatBadge badge in badgeList)
            {
                EmbedChatBadge newBadge = new EmbedChatBadge();
                newBadge.name = badge.Name;
                newBadge.versions = badge.VersionsData;
                chatRoot.embeddedData.twitchBadges.Add(newBadge);
            }
            progress?.Report(new ProgressReport(ReportType.Log, $"Input badge count: {inputCount}. Output count: {chatRoot.embeddedData.twitchBadges.Count}"));
        }

        private async Task BitTask(IProgress<ProgressReport> progress = null, CancellationToken cancellationToken = default)
        {
            List<CheerEmote> bitList = await TwitchHelper.GetBits(chatRoot.comments, _updateOptions.TempFolder, chatRoot.streamer.id.ToString(), _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData, cancellationToken: cancellationToken);

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
            progress?.Report(new ProgressReport(ReportType.Log, $"Input bit emote count: {inputCount}. Output count: {chatRoot.embeddedData.twitchBits.Count}"));
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
            double beginningCropClamp = double.IsNegative(chatRoot.video.length) ? 172_800 : chatRoot.video.length; // Get length from chatroot or if negative (N/A) max vod length (48 hours) in seconds. https://help.twitch.tv/s/article/broadcast-guidelines
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
            double endingCropClamp = double.IsNegative(chatRoot.video.length) ? 172_800 : chatRoot.video.length; // Get length from chatroot or if negative (N/A) max vod length (48 hours) in seconds. https://help.twitch.tv/s/article/broadcast-guidelines
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
            return chatRoot;
        }
    }
}