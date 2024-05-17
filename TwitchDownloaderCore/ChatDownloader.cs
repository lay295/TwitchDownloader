using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed class ChatDownloader
    {
        private readonly ChatDownloadOptions downloadOptions;
        private readonly ITaskProgress _progress;

        private static readonly HttpClient HttpClient = new()
        {
            BaseAddress = new Uri("https://gql.twitch.tv/gql"),
            DefaultRequestHeaders = { { "Client-ID", "kd1unb4b3q4t58fwlpcbzcbnm76a8fp" } }
        };

        private enum DownloadType
        {
            Clip,
            Video
        }

        public ChatDownloader(ChatDownloadOptions chatDownloadOptions, ITaskProgress progress)
        {
            downloadOptions = chatDownloadOptions;
            _progress = progress;
            downloadOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder,
                "TwitchDownloader");
        }

        private static async Task<List<Comment>> DownloadSection(double videoStart, double videoEnd, string videoId, IProgress<int> progress, ChatFormat format, CancellationToken cancellationToken)
        {
            var comments = new List<Comment>();
            //GQL only wants ints
            videoStart = Math.Floor(videoStart);
            double videoDuration = videoEnd - videoStart;
            double latestMessage = videoStart - 1;
            bool isFirst = true;
            string cursor = "";
            int errorCount = 0;

            while (latestMessage < videoEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();

                GqlCommentResponse[] commentResponse;
                try
                {
                    var request = new HttpRequestMessage()
                    {
                        Method = HttpMethod.Post
                    };

                    if (isFirst)
                    {
                        request.Content = new StringContent(
                            "[{\"operationName\":\"VideoCommentsByOffsetOrCursor\",\"variables\":{\"videoID\":\"" + videoId + "\",\"contentOffsetSeconds\":" + videoStart + "},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"b70a3591ff0f4e0313d126c6a1502d79a1c02baebb288227c582044aa76adf6a\"}}}]",
                            Encoding.UTF8, "application/json");
                    }
                    else
                    {
                        request.Content = new StringContent(
                            "[{\"operationName\":\"VideoCommentsByOffsetOrCursor\",\"variables\":{\"videoID\":\"" + videoId + "\",\"cursor\":\"" + cursor + "\"},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"b70a3591ff0f4e0313d126c6a1502d79a1c02baebb288227c582044aa76adf6a\"}}}]",
                            Encoding.UTF8, "application/json");
                    }

                    using (var httpResponse = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                    {
                        httpResponse.EnsureSuccessStatusCode();
                        commentResponse = await httpResponse.Content.ReadFromJsonAsync<GqlCommentResponse[]>(options: null, cancellationToken);
                    }

                    errorCount = 0;
                }
                catch (HttpRequestException)
                {
                    if (++errorCount > 10)
                    {
                        throw;
                    }

                    await Task.Delay(1_000 * errorCount, cancellationToken);
                    continue;
                }

                if (commentResponse[0].data.video.comments?.edges is null)
                {
                    // video.comments can be null for some dumb reason, skip
                    continue;
                }

                var convertedComments = ConvertComments(commentResponse[0].data.video, format);
                comments.EnsureCapacity(Math.Min(0, comments.Capacity + convertedComments.Count));
                foreach (var comment in convertedComments)
                {
                    if (latestMessage < videoEnd && comment.content_offset_seconds > videoStart)
                        comments.Add(comment);

                    latestMessage = comment.content_offset_seconds;
                }

                if (!commentResponse[0].data.video.comments.pageInfo.hasNextPage)
                    break;

                cursor = commentResponse[0].data.video.comments.edges.Last().cursor;

                if (progress != null)
                {
                    int percent = (int)Math.Floor((latestMessage - videoStart) / videoDuration * 100);
                    progress.Report(percent);
                }

                if (isFirst)
                {
                    isFirst = false;
                }
            }

            return comments;
        }

        private static List<Comment> ConvertComments(CommentVideo video, ChatFormat format)
        {
            List<Comment> returnList = new List<Comment>(video.comments.edges.Count);

            foreach (var comment in video.comments.edges)
            {
                //Commenter can be null for some reason, skip (deleted account?)
                if (comment.node.commenter == null)
                    continue;

                var oldComment = comment.node;
                var newComment = new Comment
                {
                    _id = oldComment.id,
                    created_at = oldComment.createdAt,
                    channel_id = video.creator?.id ?? "", // Deliberate empty string for ChatJson.UpgradeChatJson
                    content_type = "video",
                    content_id = video.id,
                    content_offset_seconds = oldComment.contentOffsetSeconds,
                    commenter = new Commenter
                    {
                        display_name = oldComment.commenter.displayName.Trim(),
                        _id = oldComment.commenter.id,
                        name = oldComment.commenter.login
                    }
                };
                var message = new Message();

                const int AVERAGE_WORD_LENGTH = 5; // The average english word is ~4.7 chars. Round up to partially account for spaces
                var bodyStringBuilder = new StringBuilder(oldComment.message.fragments.Count * AVERAGE_WORD_LENGTH);
                if (format == ChatFormat.Text)
                {
                    // Optimize allocations for writing text chats
                    foreach (var fragment in oldComment.message.fragments)
                    {
                        if (fragment.text == null)
                            continue;

                        bodyStringBuilder.Append(fragment.text);
                    }
                }
                else
                {
                    var fragments = new List<Fragment>(oldComment.message.fragments.Count);
                    var emoticons = new List<Emoticon2>();
                    foreach (var fragment in oldComment.message.fragments)
                    {
                        if (fragment.text == null)
                            continue;

                        bodyStringBuilder.Append(fragment.text);

                        var newFragment = new Fragment
                        {
                            text = fragment.text
                        };
                        if (fragment.emote != null)
                        {
                            newFragment.emoticon = new Emoticon
                            {
                                emoticon_id = fragment.emote.emoteID
                            };

                            var newEmote = new Emoticon2
                            {
                                _id = fragment.emote.emoteID,
                                begin = fragment.emote.from
                            };
                            newEmote.end = newEmote.begin + fragment.text.Length + 1;
                            emoticons.Add(newEmote);
                        }

                        fragments.Add(newFragment);
                    }

                    message.fragments = fragments;
                    message.emoticons = emoticons;
                    var badges = new List<UserBadge>(oldComment.message.userBadges.Count);
                    foreach (var badge in oldComment.message.userBadges)
                    {
                        if (string.IsNullOrEmpty(badge.setID) && string.IsNullOrEmpty(badge.version))
                            continue;

                        var newBadge = new UserBadge
                        {
                            _id = badge.setID,
                            version = badge.version
                        };
                        badges.Add(newBadge);
                    }

                    message.user_badges = badges;
                    message.user_color = oldComment.message.userColor;
                }

                message.body = bodyStringBuilder.ToString();

                var bitMatch = TwitchRegex.BitsRegex.Match(message.body);
                if (bitMatch.Success && int.TryParse(bitMatch.ValueSpan, out var result))
                {
                    message.bits_spent = result;
                }

                newComment.message = message;

                returnList.Add(newComment);
            }

            return returnList;
        }

        public async Task DownloadAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(downloadOptions.Id))
            {
                throw new NullReferenceException("Null or empty video/clip ID");
            }

            DownloadType downloadType = downloadOptions.Id.All(char.IsDigit) ? DownloadType.Video : DownloadType.Clip;

            ChatRoot chatRoot = new()
            {
                FileInfo = new ChatRootInfo { Version = ChatRootVersion.CurrentVersion, CreatedAt = DateTime.Now },
                streamer = new(),
                video = new(),
                comments = new List<Comment>()
            };

            string videoId = downloadOptions.Id;
            string videoTitle;
            DateTime videoCreatedAt;
            double videoStart = 0.0;
            double videoEnd = 0.0;
            double videoDuration = 0.0;
            double videoTotalLength;
            int viewCount;
            string game;
            int connectionCount = downloadOptions.DownloadThreads;

            if (downloadType == DownloadType.Video)
            {
                GqlVideoResponse videoInfoResponse = await TwitchHelper.GetVideoInfo(long.Parse(videoId));
                if (videoInfoResponse.data.video == null)
                {
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
                }

                chatRoot.streamer.name = videoInfoResponse.data.video.owner.displayName;
                chatRoot.streamer.id = int.Parse(videoInfoResponse.data.video.owner.id);
                chatRoot.video.description = videoInfoResponse.data.video.description?.Replace("  \n", "\n").Replace("\n\n", "\n").TrimEnd();
                videoTitle = videoInfoResponse.data.video.title;
                videoCreatedAt = videoInfoResponse.data.video.createdAt;
                videoStart = downloadOptions.TrimBeginning ? downloadOptions.TrimBeginningTime : 0.0;
                videoEnd = downloadOptions.TrimEnding ? downloadOptions.TrimEndingTime : videoInfoResponse.data.video.lengthSeconds;
                videoTotalLength = videoInfoResponse.data.video.lengthSeconds;
                viewCount = videoInfoResponse.data.video.viewCount;
                game = videoInfoResponse.data.video.game?.displayName ?? "Unknown";

                GqlVideoChapterResponse videoChapterResponse = await TwitchHelper.GetOrGenerateVideoChapters(long.Parse(videoId), videoInfoResponse.data.video);
                chatRoot.video.chapters.EnsureCapacity(videoChapterResponse.data.video.moments.edges.Count);
                foreach (var responseChapter in videoChapterResponse.data.video.moments.edges)
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
                GqlClipResponse clipInfoResponse = await TwitchHelper.GetClipInfo(videoId);
                if (clipInfoResponse.data.clip.video == null || clipInfoResponse.data.clip.videoOffsetSeconds == null)
                {
                    throw new NullReferenceException("Invalid VOD for clip, deleted/expired VOD possibly?");
                }

                videoId = clipInfoResponse.data.clip.video.id;
                downloadOptions.TrimBeginning = true;
                downloadOptions.TrimBeginningTime = (int)clipInfoResponse.data.clip.videoOffsetSeconds;
                downloadOptions.TrimEnding = true;
                downloadOptions.TrimEndingTime = downloadOptions.TrimBeginningTime + clipInfoResponse.data.clip.durationSeconds;
                chatRoot.streamer.name = clipInfoResponse.data.clip.broadcaster.displayName;
                chatRoot.streamer.id = int.Parse(clipInfoResponse.data.clip.broadcaster.id);
                videoTitle = clipInfoResponse.data.clip.title;
                videoCreatedAt = clipInfoResponse.data.clip.createdAt;
                videoStart = (int)clipInfoResponse.data.clip.videoOffsetSeconds;
                videoEnd = (int)clipInfoResponse.data.clip.videoOffsetSeconds + clipInfoResponse.data.clip.durationSeconds;
                videoTotalLength = clipInfoResponse.data.clip.durationSeconds;
                viewCount = clipInfoResponse.data.clip.viewCount;
                game = clipInfoResponse.data.clip.game?.displayName ?? "Unknown";
                connectionCount = 1;

                var clipChapter = TwitchHelper.GenerateClipChapter(clipInfoResponse.data.clip);
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

            chatRoot.video.id = videoId;
            chatRoot.video.title = videoTitle;
            chatRoot.video.created_at = videoCreatedAt;
            chatRoot.video.start = videoStart;
            chatRoot.video.end = videoEnd;
            chatRoot.video.length = videoTotalLength;
            chatRoot.video.viewCount = viewCount;
            chatRoot.video.game = game;
            videoDuration = videoEnd - videoStart;

            var downloadTasks = new List<Task<List<Comment>>>(connectionCount);
            var percentages = new int[connectionCount];

            double chunk = videoDuration / connectionCount;
            for (int i = 0; i < connectionCount; i++)
            {
                int tc = i;

                var taskProgress = new Progress<int>(percent =>
                {
                    percentages[tc] = Math.Clamp(percent, 0, 100);

                    var reportPercent = percentages.Sum() / connectionCount;
                    _progress.ReportProgress(reportPercent);
                });

                double start = videoStart + chunk * i;
                downloadTasks.Add(DownloadSection(start, start + chunk, videoId, taskProgress, downloadOptions.DownloadFormat, cancellationToken));
            }

            _progress.SetTemplateStatus("Downloading {0}%", 0);
            await Task.WhenAll(downloadTasks);

            var sortedComments = new List<Comment>(downloadTasks.Count);
            foreach (var commentTask in downloadTasks)
            {
                sortedComments.AddRange(commentTask.Result);
            }

            sortedComments.Sort(new CommentOffsetComparer());

            chatRoot.comments = sortedComments.DistinctBy(x => x._id).ToList();

            if (downloadOptions.EmbedData && (downloadOptions.DownloadFormat is ChatFormat.Json or ChatFormat.Html))
            {
                _progress.SetTemplateStatus("Downloading + Embedding Images {0}%", 0);
                chatRoot.embeddedData = new EmbeddedData();

                // This is the exact same process as in ChatUpdater.cs but not in a task oriented manner
                // TODO: Combine this with ChatUpdater in a different file
                List<TwitchEmote> thirdPartyEmotes = await TwitchHelper.GetThirdPartyEmotes(chatRoot.comments, chatRoot.streamer.id, downloadOptions.TempFolder, _progress, bttv: downloadOptions.BttvEmotes, ffz: downloadOptions.FfzEmotes, stv: downloadOptions.StvEmotes, cancellationToken: cancellationToken);
                _progress.ReportProgress(50 / 4);
                List<TwitchEmote> firstPartyEmotes = await TwitchHelper.GetEmotes(chatRoot.comments, downloadOptions.TempFolder, _progress, cancellationToken: cancellationToken);
                _progress.ReportProgress(50 / 4 * 2);
                List<ChatBadge> twitchBadges = await TwitchHelper.GetChatBadges(chatRoot.comments, chatRoot.streamer.id, downloadOptions.TempFolder, _progress, cancellationToken: cancellationToken);
                _progress.ReportProgress(50 / 4 * 3);
                List<CheerEmote> twitchBits = await TwitchHelper.GetBits(chatRoot.comments, downloadOptions.TempFolder, chatRoot.streamer.id.ToString(), cancellationToken: cancellationToken);
                _progress.ReportProgress(50);

                var totalImageCount = thirdPartyEmotes.Count + firstPartyEmotes.Count + twitchBadges.Count + twitchBits.Count;
                var imagesProcessed = 0;

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
                    _progress.ReportProgress(++imagesProcessed * 100 / totalImageCount + 50);
                }

                cancellationToken.ThrowIfCancellationRequested();

                foreach (TwitchEmote emote in firstPartyEmotes)
                {
                    EmbedEmoteData newEmote = new EmbedEmoteData();
                    newEmote.id = emote.Id;
                    newEmote.imageScale = emote.ImageScale;
                    newEmote.data = emote.ImageData;
                    newEmote.width = emote.Width / emote.ImageScale;
                    newEmote.height = emote.Height / emote.ImageScale;
                    newEmote.isZeroWidth = emote.IsZeroWidth;
                    chatRoot.embeddedData.firstParty.Add(newEmote);
                    _progress.ReportProgress(++imagesProcessed * 100 / totalImageCount + 50);
                }

                cancellationToken.ThrowIfCancellationRequested();

                foreach (ChatBadge badge in twitchBadges)
                {
                    EmbedChatBadge newBadge = new EmbedChatBadge();
                    newBadge.name = badge.Name;
                    newBadge.versions = badge.VersionsData;
                    chatRoot.embeddedData.twitchBadges.Add(newBadge);
                    _progress.ReportProgress(++imagesProcessed * 100 / totalImageCount + 50);
                }

                cancellationToken.ThrowIfCancellationRequested();

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
                    _progress.ReportProgress(++imagesProcessed * 100 / totalImageCount + 50);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (downloadOptions.DownloadFormat is ChatFormat.Json)
            {
                //Best effort, but if we fail oh well
                _progress.SetStatus("Backfilling commenter info");
                List<string> userList = chatRoot.comments.DistinctBy(x => x.commenter._id).Select(x => x.commenter._id).ToList();
                Dictionary<string, User> userInfo = new Dictionary<string, User>();
                int batchSize = 100;
                bool failedInfo = false;
                for (int i = 0; i <= userList.Count / batchSize; i++)
                {
                    try
                    {
                        List<string> userSubset = userList.Skip(i * batchSize).Take(batchSize).ToList();
                        GqlUserInfoResponse userInfoResponse = await TwitchHelper.GetUserInfo(userSubset);
                        foreach (var user in userInfoResponse.data.users)
                        {
                            userInfo[user.id] = user;
                        }
                    }
                    catch { failedInfo = true; }
                }

                if (failedInfo)
                {
                    _progress.LogInfo("Failed to backfill some commenter info");
                }

                foreach (var comment in chatRoot.comments)
                {
                    if (userInfo.TryGetValue(comment.commenter._id, out var user))
                    {
                        comment.commenter.updated_at = user.updatedAt;
                        comment.commenter.created_at = user.createdAt;
                        comment.commenter.bio = user.description;
                        comment.commenter.logo = user.profileImageURL;
                    }
                }
            }

            _progress.SetStatus("Writing output file");
            switch (downloadOptions.DownloadFormat)
            {
                case ChatFormat.Json:
                    await ChatJson.SerializeAsync(downloadOptions.Filename, chatRoot, downloadOptions.Compression, cancellationToken);
                    break;
                case ChatFormat.Html:
                    await ChatHtml.SerializeAsync(downloadOptions.Filename, chatRoot, _progress, downloadOptions.EmbedData, cancellationToken);
                    break;
                case ChatFormat.Text:
                    await ChatText.SerializeAsync(downloadOptions.Filename, chatRoot, downloadOptions.TimeFormat);
                    break;
                default:
                    throw new NotSupportedException($"{downloadOptions.DownloadFormat} is not a supported output format.");
            }
        }
    }
}