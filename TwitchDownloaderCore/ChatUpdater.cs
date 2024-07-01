using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore;

public sealed class ChatUpdater {
    private readonly ITaskProgress _progress;
    private readonly object _trimChatRootLock = new();

    private readonly ChatUpdateOptions _updateOptions;

    private bool _trimTaskReportedExpiredVod;

    public ChatUpdater(ChatUpdateOptions updateOptions, ITaskProgress progress) {
        this._updateOptions = updateOptions;
        this._progress = progress;
        this._updateOptions.TempFolder = Path.Combine(
            string.IsNullOrWhiteSpace(this._updateOptions.TempFolder)
                ? Path.GetTempPath()
                : this._updateOptions.TempFolder,
            "TwitchDownloader"
        );
    }

    public ChatRoot chatRoot { get; internal set; } = new();

    public async Task UpdateAsync(CancellationToken cancellationToken) {
        var outputFileInfo = TwitchHelper.ClaimFile(
            this._updateOptions.OutputFile,
            this._updateOptions.FileCollisionCallback,
            this._progress
        );
        this._updateOptions.OutputFile = outputFileInfo.FullName;

        // Open the destination file so that it exists in the filesystem.
        await using var outputFs = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

        try {
            await this.UpdateAsyncImpl(outputFileInfo, outputFs, cancellationToken);
        } catch {
            await Task.Delay(100, CancellationToken.None);

            TwitchHelper.CleanUpClaimedFile(outputFileInfo, outputFs, this._progress);

            throw;
        }
    }

    private async Task UpdateAsyncImpl(
        FileInfo outputFileInfo,
        FileStream outputFs,
        CancellationToken cancellationToken
    ) {
        this.chatRoot.FileInfo = new() {
            Version = ChatRootVersion.CurrentVersion, CreatedAt = this.chatRoot.FileInfo.CreatedAt,
            UpdatedAt = DateTime.Now
        };
        if (!Path.GetExtension(this._updateOptions.InputFile.Replace(".gz", ""))!.Equals(
                ".json",
                StringComparison.OrdinalIgnoreCase
            ))
            throw new NotSupportedException(
                "Only JSON chat files can be used as update input. HTML support may come in the future."
            );

        // Dynamic step count setup
        var currentStep = 0;
        var totalSteps = 2;
        if (this._updateOptions.TrimBeginning || this._updateOptions.TrimEnding) totalSteps++;
        if (this._updateOptions.OutputFormat is ChatFormat.Json or ChatFormat.Html
            && (this._updateOptions.EmbedMissing || this._updateOptions.ReplaceEmbeds)) totalSteps++;

        currentStep++;
        await this.UpdateVideoInfo(totalSteps, currentStep, cancellationToken);

        // If we are editing the chat trim
        if (this._updateOptions.TrimBeginning || this._updateOptions.TrimEnding) {
            currentStep++;
            await this.UpdateChatTrim(totalSteps, currentStep, cancellationToken);
        }

        // If we are updating/replacing embeds
        if (this._updateOptions.OutputFormat is ChatFormat.Json or ChatFormat.Html
            && (this._updateOptions.EmbedMissing || this._updateOptions.ReplaceEmbeds)) {
            currentStep++;
            await this.UpdateEmbeds(currentStep, totalSteps, cancellationToken);
        }

        // Finally save the output to file!
        this._progress.SetStatus($"Writing Output File [{++currentStep}/{totalSteps}]");
        this._progress.ReportProgress(currentStep * 100 / totalSteps);

        switch (this._updateOptions.OutputFormat) {
            case ChatFormat.Json:
                await ChatJson.SerializeAsync(
                    outputFs,
                    this.chatRoot,
                    this._updateOptions.Compression,
                    cancellationToken
                );
                break;

            case ChatFormat.Html:
                await ChatHtml.SerializeAsync(
                    outputFs,
                    outputFileInfo.FullName,
                    this.chatRoot,
                    this._progress,
                    this.chatRoot.embeddedData != null
                    && (this.chatRoot.embeddedData.firstParty?.Count > 0
                        || this.chatRoot.embeddedData.twitchBadges?.Count > 0),
                    cancellationToken
                );
                break; // If there is embedded data, it's almost guaranteed to be first party emotes or badges.

            case ChatFormat.Text:
                await ChatText.SerializeAsync(outputFs, this.chatRoot, this._updateOptions.TextTimestampFormat);
                break;

            default:
                throw new NotSupportedException(
                    $"{this._updateOptions.OutputFormat} is not a supported output format."
                );
        }
    }

    private async Task UpdateVideoInfo(int totalSteps, int currentStep, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        this._progress.SetStatus($"Updating Video Info [{currentStep}/{totalSteps}]");
        this._progress.ReportProgress(currentStep * 100 / totalSteps);

        if (string.IsNullOrWhiteSpace(this.chatRoot.video.id))
            return;

        if (this.chatRoot.video.id.All(char.IsDigit)) {
            var videoId = long.Parse(this.chatRoot.video.id);
            VideoInfo videoInfo = null;
            try {
                videoInfo = (await TwitchHelper.GetVideoInfo(videoId)).data.video;
            } catch {
                /* Eat the exception */
            }

            if (videoInfo is null) {
                this._progress.LogInfo("Unable to fetch video info, deleted/expired VOD possibly?");
                return;
            }

            this.chatRoot.video.title = videoInfo.title;
            this.chatRoot.video.description = videoInfo.description;
            this.chatRoot.video.created_at = videoInfo.createdAt;
            this.chatRoot.video.length = videoInfo.lengthSeconds;
            this.chatRoot.video.viewCount = videoInfo.viewCount;
            this.chatRoot.video.game = videoInfo.game?.displayName;

            var chaptersInfo = (await TwitchHelper.GetOrGenerateVideoChapters(videoId, videoInfo)).data.video.moments
                .edges;
            foreach (var responseChapter in chaptersInfo)
                this.chatRoot.video.chapters.Add(
                    new() {
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
                    }
                );
        } else {
            var clipId = this.chatRoot.video.id;
            Clip clipInfo = null;
            try {
                clipInfo = (await TwitchHelper.GetClipInfo(clipId)).data.clip;
            } catch {
                /* Eat the exception */
            }

            if (clipInfo is null) {
                this._progress.LogInfo("Unable to fetch clip info, deleted possibly?");
                return;
            }

            this.chatRoot.video.title = clipInfo.title;
            this.chatRoot.video.created_at = clipInfo.createdAt;
            this.chatRoot.video.length = clipInfo.durationSeconds;
            this.chatRoot.video.viewCount = clipInfo.viewCount;
            this.chatRoot.video.game = clipInfo.game.displayName;

            var clipChapter = TwitchHelper.GenerateClipChapter(clipInfo);
            this.chatRoot.video.chapters.Add(
                new() {
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
                }
            );
        }
    }

    private async Task UpdateChatTrim(int totalSteps, int currentStep, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        this._progress.SetStatus($"Updating Chat Trim [{currentStep}/{totalSteps}]");
        this._progress.ReportProgress(currentStep * 100 / totalSteps);

        var inputCommentCount = this.chatRoot.comments.Count;

        var chatTrimTasks = new[] {
            this.ChatTrimBeginningTask(cancellationToken), this.ChatTrimEndingTask(cancellationToken)
        };

        await Task.WhenAll(chatTrimTasks);
        cancellationToken.ThrowIfCancellationRequested();

        // If the output format is not JSON, the user probably wants to remove comments outside of the trim zone
        if (this._updateOptions.OutputFormat != ChatFormat.Json) {
            if (this._updateOptions.TrimBeginning) {
                var startIndex = this.chatRoot.comments.FindLastIndex(
                    c => c.content_offset_seconds < this._updateOptions.TrimBeginningTime
                );
                if (startIndex != -1)
                    this.chatRoot.comments.RemoveRange(0, startIndex + 1);
            }

            if (this._updateOptions.TrimEnding) {
                var endIndex = this.chatRoot.comments.FindLastIndex(
                    c => c.content_offset_seconds <= this._updateOptions.TrimEndingTime + 1
                );
                if (endIndex != -1)
                    this.chatRoot.comments.RemoveRange(endIndex, this.chatRoot.comments.Count - endIndex);
            }
        }

        // If the comment count didn't change, it probably failed so don't report the counts
        if (inputCommentCount != this.chatRoot.comments.Count)
            this._progress.LogInfo(
                $"Input comment count: {inputCommentCount}. Output count: {this.chatRoot.comments.Count}"
            );
    }

    private async Task UpdateEmbeds(int currentStep, int totalSteps, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        this._progress.SetStatus($"Updating Embeds [{currentStep}/{totalSteps}]");
        this._progress.ReportProgress(currentStep * 100 / totalSteps);

        this.chatRoot.embeddedData ??= new();

        var embedTasks = new[] {
            Task.Run(() => this.FirstPartyEmoteTask(cancellationToken), cancellationToken),
            Task.Run(() => this.ThirdPartyEmoteTask(cancellationToken), cancellationToken),
            Task.Run(() => this.ChatBadgeTask(cancellationToken), cancellationToken),
            Task.Run(() => this.BitTask(cancellationToken), cancellationToken)
        };

        await Task.WhenAll(embedTasks);
    }

    private async Task FirstPartyEmoteTask(CancellationToken cancellationToken = default) {
        var firstPartyEmoteList = await TwitchHelper.GetEmotes(
            this.chatRoot.comments,
            this._updateOptions.TempFolder,
            this._progress,
            this._updateOptions.ReplaceEmbeds ? null : this.chatRoot.embeddedData,
            cancellationToken: cancellationToken
        );

        var inputCount = this.chatRoot.embeddedData.firstParty.Count;
        this.chatRoot.embeddedData.firstParty = new();
        foreach (var emote in firstPartyEmoteList) {
            var newEmote = new EmbedEmoteData();
            newEmote.id = emote.Id;
            newEmote.imageScale = emote.ImageScale;
            newEmote.data = emote.ImageData;
            newEmote.width = emote.Width / emote.ImageScale;
            newEmote.height = emote.Height / emote.ImageScale;
            this.chatRoot.embeddedData.firstParty.Add(newEmote);
        }

        this._progress.LogInfo(
            $"Input 1st party emote count: {inputCount}. Output count: {this.chatRoot.embeddedData.firstParty.Count}"
        );
    }

    private async Task ThirdPartyEmoteTask(CancellationToken cancellationToken = default) {
        var thirdPartyEmoteList = await TwitchHelper.GetThirdPartyEmotes(
            this.chatRoot.comments,
            this.chatRoot.streamer.id,
            this._updateOptions.TempFolder,
            this._progress,
            this._updateOptions.ReplaceEmbeds ? null : this.chatRoot.embeddedData,
            this._updateOptions.BttvEmotes,
            this._updateOptions.FfzEmotes,
            this._updateOptions.StvEmotes,
            cancellationToken: cancellationToken
        );

        var inputCount = this.chatRoot.embeddedData.thirdParty.Count;
        this.chatRoot.embeddedData.thirdParty = new();
        foreach (var emote in thirdPartyEmoteList) {
            var newEmote = new EmbedEmoteData();
            newEmote.id = emote.Id;
            newEmote.imageScale = emote.ImageScale;
            newEmote.data = emote.ImageData;
            newEmote.name = emote.Name;
            newEmote.width = emote.Width / emote.ImageScale;
            newEmote.height = emote.Height / emote.ImageScale;
            newEmote.isZeroWidth = emote.IsZeroWidth;
            this.chatRoot.embeddedData.thirdParty.Add(newEmote);
        }

        this._progress.LogInfo(
            $"Input 3rd party emote count: {inputCount}. Output count: {this.chatRoot.embeddedData.thirdParty.Count}"
        );
    }

    private async Task ChatBadgeTask(CancellationToken cancellationToken = default) {
        var badgeList = await TwitchHelper.GetChatBadges(
            this.chatRoot.comments,
            this.chatRoot.streamer.id,
            this._updateOptions.TempFolder,
            this._progress,
            this._updateOptions.ReplaceEmbeds ? null : this.chatRoot.embeddedData,
            cancellationToken: cancellationToken
        );

        var inputCount = this.chatRoot.embeddedData.twitchBadges.Count;
        this.chatRoot.embeddedData.twitchBadges = new();
        foreach (var badge in badgeList) {
            var newBadge = new EmbedChatBadge();
            newBadge.name = badge.Name;
            newBadge.versions = badge.VersionsData;
            this.chatRoot.embeddedData.twitchBadges.Add(newBadge);
        }

        this._progress.LogInfo(
            $"Input badge count: {inputCount}. Output count: {this.chatRoot.embeddedData.twitchBadges.Count}"
        );
    }

    private async Task BitTask(CancellationToken cancellationToken = default) {
        var bitList = await TwitchHelper.GetBits(
            this.chatRoot.comments,
            this._updateOptions.TempFolder,
            this.chatRoot.streamer.id.ToString(),
            this._progress,
            this._updateOptions.ReplaceEmbeds ? null : this.chatRoot.embeddedData,
            cancellationToken: cancellationToken
        );

        var inputCount = this.chatRoot.embeddedData.twitchBits.Count;
        this.chatRoot.embeddedData.twitchBits = new();
        foreach (var bit in bitList) {
            var newBit = new EmbedCheerEmote();
            newBit.prefix = bit.prefix;
            newBit.tierList = new();
            foreach (var emotePair in bit.tierList) {
                var newEmote = new EmbedEmoteData();
                newEmote.id = emotePair.Value.Id;
                newEmote.imageScale = emotePair.Value.ImageScale;
                newEmote.data = emotePair.Value.ImageData;
                newEmote.name = emotePair.Value.Name;
                newEmote.width = emotePair.Value.Width / emotePair.Value.ImageScale;
                newEmote.height = emotePair.Value.Height / emotePair.Value.ImageScale;
                newBit.tierList.Add(emotePair.Key, newEmote);
            }

            this.chatRoot.embeddedData.twitchBits.Add(newBit);
        }

        this._progress.LogInfo(
            $"Input bit emote count: {inputCount}. Output count: {this.chatRoot.embeddedData.twitchBits.Count}"
        );
    }

    private async Task ChatTrimBeginningTask(CancellationToken cancellationToken) {
        if (!this._updateOptions.TrimBeginning)
            return;

        var tempFile = Path.Combine(this._updateOptions.TempFolder, Path.GetRandomFileName());

        try {
            // Only download missing comments if new start is less than old start
            if (this._updateOptions.TrimBeginningTime < this.chatRoot.video.start) {
                var downloadOptions = this.GetTrimDownloadOptions(
                    this.chatRoot.video.id,
                    tempFile,
                    this._updateOptions.TrimBeginningTime,
                    this.chatRoot.video.start
                );
                await this.AppendCommentSection(downloadOptions, tempFile, cancellationToken);
            }
        } catch (NullReferenceException) {
            if (!this._trimTaskReportedExpiredVod) {
                this._trimTaskReportedExpiredVod = true;
                this._progress.LogInfo(
                    "Unable to fetch possible missing comments: source VOD is expired or embedded ID is corrupt"
                );
            }
        }

        if (File.Exists(tempFile))
            File.Delete(tempFile);

        // Adjust the start parameter
        var beginningTrimClamp
            = double.IsNegative(this.chatRoot.video.length)
                ? 172_800
                : this.chatRoot.video
                    .length; // Get length from chatroot or if negative (N/A) max vod length (48 hours) in seconds. https://help.twitch.tv/s/article/broadcast-guidelines
        this.chatRoot.video.start = Math.Min(Math.Max(this._updateOptions.TrimBeginningTime, 0.0), beginningTrimClamp);
    }

    private async Task ChatTrimEndingTask(CancellationToken cancellationToken) {
        if (!this._updateOptions.TrimEnding)
            return;

        var tempFile = Path.Combine(this._updateOptions.TempFolder, Path.GetRandomFileName());

        try {
            // Only download missing comments if new end is greater than old end
            if (this._updateOptions.TrimEndingTime > this.chatRoot.video.end) {
                var downloadOptions = this.GetTrimDownloadOptions(
                    this.chatRoot.video.id,
                    tempFile,
                    this.chatRoot.video.end,
                    this._updateOptions.TrimEndingTime
                );
                await this.AppendCommentSection(downloadOptions, tempFile, cancellationToken);
            }
        } catch (NullReferenceException) {
            if (!this._trimTaskReportedExpiredVod) {
                this._trimTaskReportedExpiredVod = true;
                this._progress.LogInfo(
                    "Unable to fetch possible missing comments: source VOD is expired or embedded ID is corrupt"
                );
            }
        }

        if (File.Exists(tempFile))
            File.Delete(tempFile);

        // Adjust the end parameter
        var endingTrimClamp
            = double.IsNegative(this.chatRoot.video.length)
                ? 172_800
                : this.chatRoot.video
                    .length; // Get length from chatroot or if negative (N/A) max vod length (48 hours) in seconds. https://help.twitch.tv/s/article/broadcast-guidelines
        this.chatRoot.video.end = Math.Min(Math.Max(this._updateOptions.TrimEndingTime, 0.0), endingTrimClamp);
    }

    private async Task AppendCommentSection(
        ChatDownloadOptions downloadOptions,
        string inputFile,
        CancellationToken cancellationToken = new()
    ) {
        var chatDownloader = new ChatDownloader(downloadOptions, StubTaskProgress.Instance);
        await chatDownloader.DownloadAsync(cancellationToken);

        var newChatRoot = await ChatJson.DeserializeAsync(inputFile, true, false, false, cancellationToken);

        // Append the new comment section
        var commentsSet = new SortedSet<Comment>(new CommentOffsetComparer());
        foreach (var comment in newChatRoot.comments.Where(
                comment => comment.content_offset_seconds < downloadOptions.TrimEndingTime
                    && comment.content_offset_seconds >= downloadOptions.TrimBeginningTime
            ))
            commentsSet.Add(comment);

        lock (this._trimChatRootLock) {
            foreach (var comment in this.chatRoot.comments)
                commentsSet.Add(comment);

            var comments = commentsSet.DistinctBy(x => x._id).ToList();
            commentsSet.Clear();

            this.chatRoot.comments = comments;
        }
    }

    private ChatDownloadOptions GetTrimDownloadOptions(
        string videoId,
        string tempFile,
        double sectionStart,
        double sectionEnd
    ) => new() {
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
        TempFolder = this._updateOptions.TempFolder
    };

    public async Task<ChatRoot> ParseJsonAsync(CancellationToken cancellationToken = new()) {
        this.chatRoot = await ChatJson.DeserializeAsync(
            this._updateOptions.InputFile,
            true,
            false,
            true,
            cancellationToken
        );
        return this.chatRoot;
    }
}
