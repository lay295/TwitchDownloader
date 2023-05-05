using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public sealed class ChatDownloader
    {
        private readonly ChatDownloadOptions downloadOptions;
        private static HttpClient httpClient = new HttpClient();
        private static readonly Regex _bitsRegex = new(@"(?<=(?:\s|^)(?:4Head|Anon|Bi(?:bleThumb|tBoss)|bday|C(?:h(?:eer|arity)|orgo)|cheerwal|D(?:ansGame|oodleCheer)|EleGiggle|F(?:rankerZ|ailFish)|Goal|H(?:eyGuys|olidayCheer)|K(?:appa|reygasm)|M(?:rDestructoid|uxy)|NotLikeThis|P(?:arty|ride|JSalt)|RIPCheer|S(?:coops|h(?:owLove|amrock)|eemsGood|wiftRage|treamlabs)|TriHard|uni|VoHiYo))[1-9]\d?\d?\d?\d?\d?\d?(?=\s|$)", RegexOptions.Compiled);
        private enum DownloadType { Clip, Video }

        public ChatDownloader(ChatDownloadOptions DownloadOptions)
        {
            downloadOptions = DownloadOptions;
            downloadOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder,
                "TwitchDownloader");
        }

        private static async Task DownloadSection(double videoStart, double videoEnd, string videoId, SortedSet<Comment> comments, object commentLock, IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            //GQL only wants ints
            videoStart = Math.Floor(videoStart);
            double videoDuration = videoEnd - videoStart;
            double latestMessage = videoStart;
            string cursor = "";
            int errorCount = 0;
            List<string> alreadyAdded = new List<string>();

            while (latestMessage < videoEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<GqlCommentResponse> commentResponse;
                try
                {
                    var request = new HttpRequestMessage()
                    {
                        RequestUri = new Uri("https://gql.twitch.tv/gql"),
                        Method = HttpMethod.Post
                    };
                    request.Headers.Add("Client-ID", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                    request.Content = new StringContent("[{\"operationName\":\"VideoCommentsByOffsetOrCursor\",\"variables\":{\"videoID\":\"" + videoId + "\",\"contentOffsetSeconds\":" + (int)Math.Floor(latestMessage) + "},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"b70a3591ff0f4e0313d126c6a1502d79a1c02baebb288227c582044aa76adf6a\"}}}]", Encoding.UTF8, "application/json");

                    using (var httpResponse = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                    {
                        httpResponse.EnsureSuccessStatusCode();
                        commentResponse = await httpResponse.Content.ReadFromJsonAsync<List<GqlCommentResponse>>(options: null, cancellationToken);
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

                bool addedComment = false;
                var convertedComments = ConvertComments(commentResponse[0].data.video);
                lock (commentLock)
                {
                    foreach (var comment in convertedComments)
                    {
                        if (latestMessage < videoEnd && comment.content_offset_seconds > videoStart && !alreadyAdded.Contains(comment._id))
                        {
                            alreadyAdded.Add(comment._id);
                            comments.Add(comment);
                            addedComment = true;
                        }

                        latestMessage = comment.content_offset_seconds;
                    }
                }

                if (!addedComment)
                    latestMessage = Math.Floor(latestMessage) + 1;

                int percent = (int)Math.Floor((latestMessage - videoStart) / videoDuration * 100);
                progress.Report(new ProgressReport() { ReportType = ReportType.Percent, Data = percent });
            }
        }

        private static List<Comment> ConvertComments(CommentVideo video)
        {
            List<Comment> returnList = new List<Comment>();

            foreach (var comment in video.comments.edges)
            {
                //Commenter can be null for some reason, skip (deleted account?)
                if (comment.node.commenter == null)
                    continue;

                Comment newComment = new Comment();
                var oldComment = comment.node;
                newComment._id = oldComment.id;
                newComment.created_at = oldComment.createdAt;
                newComment.channel_id = video.creator.id;
                newComment.content_type = "video";
                newComment.content_id = video.id;
                newComment.content_offset_seconds = oldComment.contentOffsetSeconds;
                Commenter commenter = new Commenter();
                commenter.display_name = oldComment.commenter.displayName;
                commenter._id = oldComment.commenter.id;
                commenter.name = oldComment.commenter.login;
                newComment.commenter = commenter;
                Message message = new Message();
                message.body = "";
                List<Fragment> fragments = new List<Fragment>();
                List<Emoticon2> emoticons = new List<Emoticon2>();
                foreach (var fragment in oldComment.message.fragments)
                {
                    Fragment newFragment = new Fragment();
                    if (fragment.text != null)
                        message.body += fragment.text;

                    if (fragment.emote != null)
                    {
                        newFragment.emoticon = new Emoticon();
                        newFragment.emoticon.emoticon_id = fragment.emote.emoteID;

                        Emoticon2 newEmote = new Emoticon2();
                        newEmote._id = fragment.emote.emoteID;
                        newEmote.begin = fragment.emote.from;
                        newEmote.end = newEmote.begin + fragment.text.Length + 1;
                        emoticons.Add(newEmote);
                    }

                    newFragment.text = fragment.text;
                    fragments.Add(newFragment);
                }
                message.fragments = fragments;
                List<UserBadge> badges = new List<UserBadge>();
                foreach (var badge in oldComment.message.userBadges)
                {
                    UserBadge newBadge = new UserBadge();
                    newBadge._id = badge.setID;
                    newBadge.version = badge.version;
                    badges.Add(newBadge);
                }
                message.user_badges = badges;
                message.user_color = oldComment.message.userColor;
                message.emoticons = emoticons;
                var bitMatch = _bitsRegex.Match(message.body);
                if (bitMatch.Success && int.TryParse(bitMatch.ValueSpan, out var result))
                {
                    message.bits_spent = result;
                }
                newComment.message = message;

                returnList.Add(newComment);
            }

            return returnList;
        }

        public async Task DownloadAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(downloadOptions.Id))
            {
                throw new NullReferenceException("Null or empty video/clip ID");
            }
            DownloadType downloadType = downloadOptions.Id.All(char.IsDigit) ? DownloadType.Video : DownloadType.Clip;

            List<Comment> comments = new List<Comment>();
            ChatRoot chatRoot = new() { FileInfo = new() { Version = ChatRootVersion.CurrentVersion, CreatedAt = DateTime.Now }, streamer = new(), video = new(), comments = comments };

            string videoId = downloadOptions.Id;
            string videoTitle;
            DateTime videoCreatedAt;
            double videoStart = 0.0;
            double videoEnd = 0.0;
            double videoDuration = 0.0;
            double videoTotalLength;
            int connectionCount = downloadOptions.ConnectionCount;

            if (downloadType == DownloadType.Video)
            {
                GqlVideoResponse videoInfoResponse = await TwitchHelper.GetVideoInfo(int.Parse(videoId));
                if (videoInfoResponse.data.video == null)
                {
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
                }

                chatRoot.streamer.name = videoInfoResponse.data.video.owner.displayName;
                chatRoot.streamer.id = int.Parse(videoInfoResponse.data.video.owner.id);
                videoTitle = videoInfoResponse.data.video.title;
                videoCreatedAt = videoInfoResponse.data.video.createdAt;
                videoStart = downloadOptions.CropBeginning ? downloadOptions.CropBeginningTime : 0.0;
                videoEnd = downloadOptions.CropEnding ? downloadOptions.CropEndingTime : videoInfoResponse.data.video.lengthSeconds;
                videoTotalLength = videoInfoResponse.data.video.lengthSeconds;

                GqlVideoChapterResponse videoChapterResponse = await TwitchHelper.GetVideoChapters(int.Parse(videoId));
                foreach (var responseChapter in videoChapterResponse.data.video.moments.edges)
                {
                    VideoChapter chapter = new()
                    {
                        id = responseChapter.node.id,
                        startMilliseconds = responseChapter.node.positionMilliseconds,
                        lengthMilliseconds = responseChapter.node.durationMilliseconds,
                        _type = responseChapter.node._type,
                        description = responseChapter.node.description,
                        subDescription = responseChapter.node.subDescription,
                        thumbnailUrl = responseChapter.node.thumbnailURL,
                        gameId = responseChapter.node.details.game?.id ?? null,
                        gameDisplayName = responseChapter.node.details.game?.displayName ?? null,
                        gameBoxArtUrl = responseChapter.node.details.game?.boxArtURL ?? null
                    };
                    chatRoot.video.chapters.Add(chapter);
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
                downloadOptions.CropBeginning = true;
                downloadOptions.CropBeginningTime = (int)clipInfoResponse.data.clip.videoOffsetSeconds;
                downloadOptions.CropEnding = true;
                downloadOptions.CropEndingTime = downloadOptions.CropBeginningTime + clipInfoResponse.data.clip.durationSeconds;
                chatRoot.streamer.name = clipInfoResponse.data.clip.broadcaster.displayName;
                chatRoot.streamer.id = int.Parse(clipInfoResponse.data.clip.broadcaster.id);
                videoTitle = clipInfoResponse.data.clip.title;
                videoCreatedAt = clipInfoResponse.data.clip.createdAt;
                videoStart = (int)clipInfoResponse.data.clip.videoOffsetSeconds;
                videoEnd = (int)clipInfoResponse.data.clip.videoOffsetSeconds + clipInfoResponse.data.clip.durationSeconds;
                videoTotalLength = clipInfoResponse.data.clip.durationSeconds;
                connectionCount = 1;
            }

            chatRoot.video.id = videoId;
            chatRoot.video.title = videoTitle;
            chatRoot.video.created_at = videoCreatedAt;
            chatRoot.video.start = videoStart;
            chatRoot.video.end = videoEnd;
            chatRoot.video.length = videoTotalLength;
            videoDuration = videoEnd - videoStart;

            SortedSet<Comment> commentsSet = new SortedSet<Comment>(new SortedCommentComparer());
            object commentLock = new object();
            List<Task> tasks = new List<Task>();
            List<int> percentages = new List<int>(connectionCount);

            double chunk = videoDuration / connectionCount;
            for (int i = 0; i < connectionCount; i++)
            {
                int tc = i;
                percentages.Add(0);
                var taskProgress = new Progress<ProgressReport>(progressReport =>
                {
                    if (progressReport.ReportType != ReportType.Percent)
                    {
                        progress.Report(progressReport);
                    }
                    else
                    {
                        int percent = (int)progressReport.Data;
                        if (percent > 100)
                        {
                            percent = 100;
                        }

                        percentages[tc] = percent;

                        percent = 0;
                        for (int j = 0; j < connectionCount; j++)
                        {
                            percent += percentages[j];
                        }
                        percent /= connectionCount;

                        progress.Report(new ProgressReport() { ReportType = ReportType.SameLineStatus, Data = $"Downloading {percent}%" });
                        progress.Report(new ProgressReport() { ReportType = ReportType.Percent, Data = percent });
                    }
                });
                double start = videoStart + chunk * i;
                tasks.Add(DownloadSection(start, start + chunk, videoId, commentsSet, commentLock, taskProgress, cancellationToken));
            }

            await Task.WhenAll(tasks);

            comments = commentsSet.DistinctBy(x => x._id).ToList();
            chatRoot.comments = comments;

            if (downloadOptions.EmbedData && (downloadOptions.DownloadFormat is ChatFormat.Json or ChatFormat.Html))
            {
                progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Downloading + Embedding Images" });
                chatRoot.embeddedData = new EmbeddedData();

                // This is the exact same process as in ChatUpdater.cs but not in a task oriented manner
                // TODO: Combine this with ChatUpdater in a different file
                List<TwitchEmote> thirdPartyEmotes = await TwitchHelper.GetThirdPartyEmotes(comments, chatRoot.streamer.id, downloadOptions.TempFolder, bttv: downloadOptions.BttvEmotes, ffz: downloadOptions.FfzEmotes, stv: downloadOptions.StvEmotes, cancellationToken: cancellationToken);
                List<TwitchEmote> firstPartyEmotes = await TwitchHelper.GetEmotes(comments, downloadOptions.TempFolder, cancellationToken: cancellationToken);
                List<ChatBadge> twitchBadges = await TwitchHelper.GetChatBadges(comments, chatRoot.streamer.id, downloadOptions.TempFolder, cancellationToken: cancellationToken);
                List<CheerEmote> twitchBits = await TwitchHelper.GetBits(comments, downloadOptions.TempFolder, chatRoot.streamer.id.ToString(), cancellationToken: cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

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
                foreach (ChatBadge badge in twitchBadges)
                {
                    EmbedChatBadge newBadge = new EmbedChatBadge();
                    newBadge.name = badge.Name;
                    newBadge.versions = badge.VersionsData;
                    chatRoot.embeddedData.twitchBadges.Add(newBadge);
                }
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
            }

            if (downloadOptions.DownloadFormat is ChatFormat.Json)
            {
                //Best effort, but if we fail oh well
                progress.Report(new ProgressReport() { ReportType = ReportType.NewLineStatus, Data = "Backfilling commenter info" });
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
                    progress.Report(new ProgressReport() { ReportType = ReportType.Log, Data = "Failed to backfill some commenter info" });
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

            progress.Report(new ProgressReport(ReportType.NewLineStatus, "Writing output file"));
            switch (downloadOptions.DownloadFormat)
            {
                case ChatFormat.Json:
                    await ChatJson.SerializeAsync(downloadOptions.Filename, chatRoot, downloadOptions.Compression, cancellationToken);
                    break;
                case ChatFormat.Html:
                    await ChatHtml.SerializeAsync(downloadOptions.Filename, chatRoot, downloadOptions.EmbedData, cancellationToken);
                    break;
                case ChatFormat.Text:
                    await ChatText.SerializeAsync(downloadOptions.Filename, chatRoot, downloadOptions.TimeFormat);
                    break;
                default:
                    throw new NotImplementedException("Requested output chat format is not implemented");
            }
        }
    }
}