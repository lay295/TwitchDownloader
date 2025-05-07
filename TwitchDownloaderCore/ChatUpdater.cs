using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed class ChatUpdater
    {
        public ChatRoot chatRoot { get; internal set; } = new();
        private readonly object _trimChatRootLock = new();

        private readonly ChatUpdateOptions _updateOptions;
        private readonly ITaskProgress _progress;
        private readonly string _cacheDir;

        public ChatUpdater(ChatUpdateOptions updateOptions, ITaskProgress progress)
        {
            _updateOptions = updateOptions;
            _progress = progress;
            _cacheDir = CacheDirectoryService.GetCacheDirectory(_updateOptions.TempFolder);
        }

        public async Task UpdateAsync(CancellationToken cancellationToken)
        {
            var outputFileInfo = TwitchHelper.ClaimFile(_updateOptions.OutputFile, _updateOptions.FileCollisionCallback, _progress);
            _updateOptions.OutputFile = outputFileInfo.FullName;

            // Open the destination file so that it exists in the filesystem.
            await using var outputFs = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

            try
            {
                await UpdateAsyncImpl(outputFileInfo, outputFs, cancellationToken);
            }
            catch
            {
                await Task.Delay(100, CancellationToken.None);

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, outputFs, _progress);

                throw;
            }
        }

        private async Task UpdateAsyncImpl(FileInfo outputFileInfo, FileStream outputFs, CancellationToken cancellationToken)
        {
            chatRoot.FileInfo = new() { Version = ChatRootVersion.CurrentVersion, CreatedAt = chatRoot.FileInfo.CreatedAt, UpdatedAt = DateTime.Now };
            if (!Path.GetExtension(_updateOptions.InputFile.Replace(".gz", ""))!.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException("Only JSON chat files can be used as update input. HTML support may come in the future.");
            }

            // Dynamic step count setup
            int currentStep = 0;
            int totalSteps = 2;
            if (_updateOptions.TrimBeginning || _updateOptions.TrimEnding) totalSteps++;
            if (_updateOptions.OutputFormat is ChatFormat.Json or ChatFormat.Html
                && (_updateOptions.EmbedMissing || _updateOptions.ReplaceEmbeds)) totalSteps++;

            currentStep++;
            await UpdateVideoInfo(totalSteps, currentStep, cancellationToken);

            // If we are editing the chat trim
            if (_updateOptions.TrimBeginning || _updateOptions.TrimEnding)
            {
                currentStep++;
                await UpdateChatTrim(totalSteps, currentStep, cancellationToken);
            }

            // If we are updating/replacing embeds
            if (_updateOptions.OutputFormat is ChatFormat.Json or ChatFormat.Html
                && (_updateOptions.EmbedMissing || _updateOptions.ReplaceEmbeds))
            {
                currentStep++;
                await UpdateEmbeds(currentStep, totalSteps, cancellationToken);
            }

            // Finally save the output to file!
            _progress.SetStatus($"Writing Output File [{++currentStep}/{totalSteps}]");
            _progress.ReportProgress(currentStep * 100 / totalSteps);

            Stream outputStream = _updateOptions.Compression switch
            {
                ChatCompression.None => outputFs,
                ChatCompression.Gzip => new GZipStream(outputFs, CompressionLevel.SmallestSize),
                _ => throw new NotSupportedException($"{_updateOptions.Compression} is not a supported chat compression.")
            };

            try
            {
                switch (_updateOptions.OutputFormat)
                {
                    case ChatFormat.Json:
                        await ChatJson.SerializeAsync(outputStream, chatRoot, cancellationToken);
                        break;
                    case ChatFormat.Html:
                        await ChatHtml.SerializeAsync(outputStream, outputFileInfo.FullName, chatRoot, _progress, chatRoot.embeddedData != null && (chatRoot.embeddedData.firstParty?.Count > 0 || chatRoot.embeddedData.twitchBadges?.Count > 0), cancellationToken);
                        break; // If there is embedded data, it's almost guaranteed to be first party emotes or badges.
                    case ChatFormat.Text:
                        await ChatText.SerializeAsync(outputStream, chatRoot, _updateOptions.TextTimestampFormat);
                        break;
                    default:
                        throw new NotSupportedException($"{_updateOptions.OutputFormat} is not a supported output format.");
                }
            }
            finally
            {
                if (outputStream is GZipStream gzipStream)
                {
                    // GZipStream finishes writing on disposal, not flush.
                    await gzipStream.DisposeAsync();
                }
            }
        }

        private async Task UpdateVideoInfo(int totalSteps, int currentStep, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _progress.SetStatus($"Updating Video Info [{currentStep}/{totalSteps}]");
            _progress.ReportProgress(currentStep * 100 / totalSteps);

            if (string.IsNullOrWhiteSpace(chatRoot.video.id))
            {
                return;
            }

            if (chatRoot.video.id.All(char.IsDigit))
            {
                var videoId = long.Parse(chatRoot.video.id);
                VideoInfo videoInfo = null;
                try
                {
                    videoInfo = (await TwitchHelper.GetVideoInfo(videoId)).data.video;
                }
                catch { /* Eat the exception */ }

                if (videoInfo is null)
                {
                    _progress.LogInfo("Unable to fetch video info, deleted/expired VOD possibly?");
                    return;
                }

                chatRoot.video.title = videoInfo.title;
                chatRoot.video.description = videoInfo.description;
                chatRoot.video.created_at = videoInfo.createdAt;
                chatRoot.video.length = videoInfo.lengthSeconds;
                chatRoot.video.viewCount = videoInfo.viewCount;
                chatRoot.video.game = videoInfo.game?.displayName;

                var chaptersInfo = (await TwitchHelper.GetOrGenerateVideoChapters(videoId, videoInfo)).data.video.moments.edges;
                foreach (var responseChapter in chaptersInfo)
                {
                    chatRoot.video.chapters.Add(new VideoChapter
                    {
                        id = responseChapter.node.id,
                        startMilliseconds = responseChapter.node.positionMilliseconds,
                        lengthMilliseconds = responseChapter.node.durationMilliseconds,
                        _type = responseChapter.node._type,
                        description = responseChapter.node.description,
                        subDescription = responseChapter.node.subDescription,
                        thumbnailUrl = responseChapter.node.thumbnailURL,
                        gameId = responseChapter.node.details.game?.id,
                        gameDisplayName = responseChapter.node.details.game?.displayName,
                        gameBoxArtUrl = responseChapter.node.details.game?.boxArtURL
                    });
                }
            }
            else
            {
                var clipId = chatRoot.video.id;
                ShareClipRenderStatusClip clipRenderStatus;
                try
                {
                    clipRenderStatus = (await TwitchHelper.GetShareClipRenderStatus(clipId)).data.clip;
                }
                catch
                {
                    _progress.LogInfo("Unable to fetch clip info, deleted possibly?");
                    return;
                }

                chatRoot.clipper ??= new Clipper
                {
                    name = clipRenderStatus.curator?.displayName,
                    login = clipRenderStatus.curator?.login,
                    id = int.Parse(clipRenderStatus.curator?.id ?? "0"),
                };

                chatRoot.video.title = clipRenderStatus.title;
                chatRoot.video.created_at = clipRenderStatus.createdAt;
                chatRoot.video.length = clipRenderStatus.durationSeconds;
                chatRoot.video.viewCount = clipRenderStatus.viewCount;
                chatRoot.video.game = clipRenderStatus.game.displayName;

                if (chatRoot.video.chapters is not { Count: > 0 })
                {
                    var clipChapter = TwitchHelper.GenerateClipChapter(clipRenderStatus);
                    chatRoot.video.chapters.Add(new VideoChapter
                    {
                        id = clipChapter.node.id,
                        startMilliseconds = clipChapter.node.positionMilliseconds,
                        lengthMilliseconds = clipChapter.node.durationMilliseconds,
                        _type = clipChapter.node._type,
                        description = clipChapter.node.description,
                        subDescription = clipChapter.node.subDescription,
                        thumbnailUrl = clipChapter.node.thumbnailURL,
                        gameId = clipChapter.node.details.game?.id,
                        gameDisplayName = clipChapter.node.details.game?.displayName,
                        gameBoxArtUrl = clipChapter.node.details.game?.boxArtURL
                    });
                }
            }
        }

        private async Task UpdateChatTrim(int totalSteps, int currentStep, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _progress.SetStatus($"Updating Chat Trim [{currentStep}/{totalSteps}]");
            _progress.ReportProgress(currentStep * 100 / totalSteps);

            int inputCommentCount = chatRoot.comments.Count;

            var chatTrimTasks = new[]
            {
                ChatTrimBeginningTask(cancellationToken),
                ChatTrimEndingTask(cancellationToken)
            };

            await Task.WhenAll(chatTrimTasks);
            cancellationToken.ThrowIfCancellationRequested();

            // If the output format is not JSON, the user probably wants to remove comments outside of the trim zone
            if (_updateOptions.OutputFormat != ChatFormat.Json)
            {
                if (_updateOptions.TrimBeginning)
                {
                    var startIndex = chatRoot.comments.FindLastIndex(c => c.content_offset_seconds < _updateOptions.TrimBeginningTime);
                    if (startIndex != -1)
                    {
                        chatRoot.comments.RemoveRange(0, startIndex + 1);
                    }
                }

                if (_updateOptions.TrimEnding)
                {
                    var endIndex = chatRoot.comments.FindLastIndex(c => c.content_offset_seconds <= _updateOptions.TrimEndingTime + 1);
                    if (endIndex != -1)
                    {
                        chatRoot.comments.RemoveRange(endIndex, chatRoot.comments.Count - endIndex);
                    }
                }
            }

            // If the comment count didn't change, it probably failed so don't report the counts
            if (inputCommentCount != chatRoot.comments.Count)
            {
                _progress.LogInfo($"Input comment count: {inputCommentCount}. Output count: {chatRoot.comments.Count}");
            }
        }

        private async Task UpdateEmbeds(int currentStep, int totalSteps, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _progress.SetStatus($"Updating Embeds [{currentStep}/{totalSteps}]");
            _progress.ReportProgress(currentStep * 100 / totalSteps);

            chatRoot.embeddedData ??= new EmbeddedData();

            var embedTasks = new[]
            {
                Task.Run(() => FirstPartyEmoteTask(cancellationToken), cancellationToken),
                Task.Run(() => ThirdPartyEmoteTask(cancellationToken), cancellationToken),
                Task.Run(() => ChatBadgeTask(cancellationToken), cancellationToken),
                Task.Run(() => BitTask(cancellationToken), cancellationToken),
            };

            await Task.WhenAll(embedTasks);
        }

        private async Task FirstPartyEmoteTask(CancellationToken cancellationToken = default)
        {
            List<TwitchEmote> firstPartyEmoteList = await TwitchHelper.GetEmotes(chatRoot.comments, _cacheDir, _progress, _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData, cancellationToken: cancellationToken);

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
            _progress.LogInfo($"Input 1st party emote count: {inputCount}. Output count: {chatRoot.embeddedData.firstParty.Count}");
        }

        private async Task ThirdPartyEmoteTask(CancellationToken cancellationToken = default)
        {
            List<TwitchEmote> thirdPartyEmoteList = await TwitchHelper.GetThirdPartyEmotes(chatRoot.comments, chatRoot.streamer.id, _cacheDir, _progress, _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData, _updateOptions.BttvEmotes, _updateOptions.FfzEmotes, _updateOptions.StvEmotes, cancellationToken: cancellationToken);

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
                newEmote.isZeroWidth = emote.IsZeroWidth;
                chatRoot.embeddedData.thirdParty.Add(newEmote);
            }
            _progress.LogInfo($"Input 3rd party emote count: {inputCount}. Output count: {chatRoot.embeddedData.thirdParty.Count}");
        }

        private async Task ChatBadgeTask(CancellationToken cancellationToken = default)
        {
            List<ChatBadge> badgeList = await TwitchHelper.GetChatBadges(chatRoot.comments, chatRoot.streamer.id, _cacheDir, _progress, _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData, cancellationToken: cancellationToken);

            int inputCount = chatRoot.embeddedData.twitchBadges.Count;
            chatRoot.embeddedData.twitchBadges = new List<EmbedChatBadge>();
            foreach (ChatBadge badge in badgeList)
            {
                EmbedChatBadge newBadge = new EmbedChatBadge();
                newBadge.name = badge.Name;
                newBadge.versions = badge.VersionsData;
                chatRoot.embeddedData.twitchBadges.Add(newBadge);
            }
            _progress.LogInfo($"Input badge count: {inputCount}. Output count: {chatRoot.embeddedData.twitchBadges.Count}");
        }

        private async Task BitTask(CancellationToken cancellationToken = default)
        {
            List<CheerEmote> bitList = await TwitchHelper.GetBits(chatRoot.comments, _cacheDir, chatRoot.streamer.id.ToString(), _progress, _updateOptions.ReplaceEmbeds ? null : chatRoot.embeddedData, cancellationToken: cancellationToken);

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
            _progress.LogInfo($"Input bit emote count: {inputCount}. Output count: {chatRoot.embeddedData.twitchBits.Count}");
        }

        private bool _trimTaskReportedExpiredVod;

        private async Task ChatTrimBeginningTask(CancellationToken cancellationToken)
        {
            if (!_updateOptions.TrimBeginning)
            {
                return;
            }

            string tempFile = Path.Combine(_cacheDir, Path.GetRandomFileName());

            try
            {
                // Only download missing comments if new start is less than old start
                if (_updateOptions.TrimBeginningTime < chatRoot.video.start)
                {
                    ChatDownloadOptions downloadOptions = GetTrimDownloadOptions(chatRoot.video.id, tempFile, _updateOptions.TrimBeginningTime, chatRoot.video.start);
                    await AppendCommentSection(downloadOptions, tempFile, cancellationToken);
                }
            }
            catch (NullReferenceException)
            {
                if (!_trimTaskReportedExpiredVod)
                {
                    _trimTaskReportedExpiredVod = true;
                    _progress.LogInfo("Unable to fetch possible missing comments: source VOD is expired or embedded ID is corrupt");
                }
            }

            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            // Adjust the start parameter
            double beginningTrimClamp = double.IsNegative(chatRoot.video.length) ? 172_800 : chatRoot.video.length; // Get length from chatroot or if negative (N/A) max vod length (48 hours) in seconds. https://help.twitch.tv/s/article/broadcast-guidelines
            chatRoot.video.start = Math.Min(Math.Max(_updateOptions.TrimBeginningTime, 0.0), beginningTrimClamp);
        }

        private async Task ChatTrimEndingTask(CancellationToken cancellationToken)
        {
            if (!_updateOptions.TrimEnding)
            {
                return;
            }

            string tempFile = Path.Combine(_cacheDir, Path.GetRandomFileName());

            try
            {
                // Only download missing comments if new end is greater than old end
                if (_updateOptions.TrimEndingTime > chatRoot.video.end)
                {
                    ChatDownloadOptions downloadOptions = GetTrimDownloadOptions(chatRoot.video.id, tempFile, chatRoot.video.end, _updateOptions.TrimEndingTime);
                    await AppendCommentSection(downloadOptions, tempFile, cancellationToken);
                }
            }
            catch (NullReferenceException)
            {
                if (!_trimTaskReportedExpiredVod)
                {
                    _trimTaskReportedExpiredVod = true;
                    _progress.LogInfo("Unable to fetch possible missing comments: source VOD is expired or embedded ID is corrupt");
                }
            }

            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }

            // Adjust the end parameter
            double endingTrimClamp = double.IsNegative(chatRoot.video.length) ? 172_800 : chatRoot.video.length; // Get length from chatroot or if negative (N/A) max vod length (48 hours) in seconds. https://help.twitch.tv/s/article/broadcast-guidelines
            chatRoot.video.end = Math.Min(Math.Max(_updateOptions.TrimEndingTime, 0.0), endingTrimClamp);
        }

        private async Task AppendCommentSection(ChatDownloadOptions downloadOptions, string inputFile, CancellationToken cancellationToken = new())
        {
            var chatDownloader = new ChatDownloader(downloadOptions, StubTaskProgress.Instance);
            await chatDownloader.DownloadAsync(cancellationToken);

            ChatRoot newChatRoot = await ChatJson.DeserializeAsync(inputFile, getComments: true, onlyFirstAndLastComments: false, getEmbeds: false, cancellationToken);

            // Append the new comment section
            var commentsSet = newChatRoot.comments.ToHashSet(new CommentIdEqualityComparer());

            lock (_trimChatRootLock)
            {
                commentsSet.EnsureCapacity(commentsSet.Count + chatRoot.comments.Count);
                foreach (var comment in chatRoot.comments)
                {
                    commentsSet.Add(comment);
                }

                chatRoot.comments = commentsSet.ToList();
                chatRoot.comments.Sort(new CommentOffsetComparer());
            }
        }

        private ChatDownloadOptions GetTrimDownloadOptions(string videoId, string tempFile, double sectionStart, double sectionEnd)
        {
            return new ChatDownloadOptions()
            {
                Id = videoId,
                DownloadFormat = ChatFormat.Json, // json is required to parse as a new chatroot object
                Compression = ChatCompression.Gzip,
                Filename = tempFile,
                TrimBeginning = true,
                TrimBeginningTime = sectionStart,
                TrimEnding = true,
                TrimEndingTime = sectionEnd,
                DownloadThreads = 4,
                EmbedData = false,
                BttvEmotes = false,
                FfzEmotes = false,
                StvEmotes = false,
                TempFolder = _updateOptions.TempFolder
            };
        }

        public async Task<ChatRoot> ParseJsonAsync(CancellationToken cancellationToken = new())
        {
            chatRoot = await ChatJson.DeserializeAsync(_updateOptions.InputFile, true, false, true, cancellationToken);
            return chatRoot;
        }
    }
}