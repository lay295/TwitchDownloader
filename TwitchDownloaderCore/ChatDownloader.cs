using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public class ChatDownloader
    {
        internal ChatDownloadOptions downloadOptions { get; private set; }
        private enum DownloadType { Clip, Video }

        public ChatDownloader(ChatDownloadOptions DownloadOptions)
        {
            downloadOptions = DownloadOptions;
            downloadOptions.TempFolder = Path.Combine(string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder, "TwitchDownloader");
        }

        internal static async Task DownloadSection(IProgress<ProgressReport> progress, CancellationToken cancellationToken, double videoStart, double videoEnd, string videoId, SortedSet<Comment> comments, object commentLock)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Accept", "application/vnd.twitchtv.v5+json; charset=UTF-8");
                client.Headers.Add("Client-Id", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                double videoDuration = videoEnd - videoStart;
                double latestMessage = videoStart - 1;
                bool isFirst = true;
                string cursor = "";
                int errorCount = 0;

                while (latestMessage < videoEnd)
                {
                    string response;

                    try
                    {
                        if (isFirst)
                            response = await client.DownloadStringTaskAsync(String.Format("https://api.twitch.tv/v5/videos/{0}/comments?content_offset_seconds={1}", videoId, videoStart));
                        else
                            response = await client.DownloadStringTaskAsync(String.Format("https://api.twitch.tv/v5/videos/{0}/comments?cursor={1}", videoId, cursor));
                        errorCount = 0;
                    }
                    catch (WebException ex)
                    {
                        await Task.Delay(1000 * errorCount);
                        errorCount++;

                        if (errorCount >= 10)
                            throw ex;

                        continue;
                    }

                    CommentResponse commentResponse = JsonConvert.DeserializeObject<CommentResponse>(response);

                    lock (commentLock)
                    {
                        foreach (var comment in commentResponse.comments)
                        {
                            if (latestMessage < videoEnd && comment.content_offset_seconds >= videoStart)
                                comments.Add(comment);

                            latestMessage = comment.content_offset_seconds;
                        }
                    }
                    if (commentResponse._next == null)
                        break;
                    else
                        cursor = commentResponse._next;

                    int percent = (int)Math.Floor((latestMessage - videoStart) / videoDuration * 100);
                    progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = percent });

                    cancellationToken.ThrowIfCancellationRequested();

                    if (isFirst)
                        isFirst = false;

                }
            }
        }

        internal static async Task DownloadSectionGql(IProgress<ProgressReport> progress, CancellationToken cancellationToken, double videoStart, double videoEnd, string videoId, SortedSet<Comment> comments, object commentLock)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Client-Id", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                //GQL only wants ints
                videoStart = Math.Floor(videoStart);
                double videoDuration = videoEnd - videoStart;
                double latestMessage = videoStart - 1;
                bool isFirst = true;
                string cursor = "";
                int errorCount = 0;

                while (latestMessage < videoEnd)
                {
                    string response;

                    try
                    {
                        if (isFirst)
                            response = await client.UploadStringTaskAsync("https://gql.twitch.tv/gql", "[{\"operationName\":\"VideoCommentsByOffsetOrCursor\",\"variables\":{\"videoID\":\"" + videoId + "\",\"contentOffsetSeconds\":" + videoStart + "},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"b70a3591ff0f4e0313d126c6a1502d79a1c02baebb288227c582044aa76adf6a\"}}}]");
                        else
                            response = await client.UploadStringTaskAsync("https://gql.twitch.tv/gql", "[{\"operationName\":\"VideoCommentsByOffsetOrCursor\",\"variables\":{\"videoID\":\"" + videoId + "\",\"cursor\":\"" + cursor + "\"},\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"b70a3591ff0f4e0313d126c6a1502d79a1c02baebb288227c582044aa76adf6a\"}}}]");
                        errorCount = 0;
                    }
                    catch (WebException ex)
                    {
                        await Task.Delay(1000 * errorCount);
                        errorCount++;

                        if (errorCount >= 10)
                            throw ex;

                        continue;
                    }

                    GqlCommentResponse commentResponse = JsonConvert.DeserializeObject<List<GqlCommentResponse>>(response)[0];
                    List<Comment> convertedComments = ConvertComments(commentResponse.data.video);

                    lock (commentLock)
                    {
                        foreach (var comment in convertedComments)
                        {
                            if (latestMessage < videoEnd && comment.content_offset_seconds > videoStart)
                                comments.Add(comment);

                            latestMessage = comment.content_offset_seconds;
                        }
                    }
                    if (!commentResponse.data.video.comments.pageInfo.hasNextPage)
                        break;
                    else
                        cursor = commentResponse.data.video.comments.edges.Last().cursor;

                    int percent = (int)Math.Floor((latestMessage - videoStart) / videoDuration * 100);
                    progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = percent });

                    cancellationToken.ThrowIfCancellationRequested();

                    if (isFirst)
                        isFirst = false;

                }
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
                newComment.updated_at = oldComment.createdAt;
                newComment.channel_id = video.creator.id;
                newComment.content_type = "video";
                newComment.content_id = video.id;
                newComment.content_offset_seconds = oldComment.contentOffsetSeconds;
                Commenter commenter = new Commenter();
                commenter.display_name = oldComment.commenter.displayName;
                commenter._id = oldComment.commenter.id;
                commenter.name = oldComment.commenter.login;
                commenter.type = "user";
                newComment.commenter = commenter;
                newComment.source = "chat";
                newComment.state = "published";
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
                message.is_action = false;
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
            string videoId = downloadOptions.Id;

            List<Comment> comments = new List<Comment>();
            ChatRoot chatRoot = new ChatRoot() { streamer = new Streamer(), video = new VideoTime(), comments = comments };

            double videoStart = 0.0;
            double videoEnd = 0.0;
            double videoDuration = 0.0;
            int connectionCount = downloadOptions.ConnectionCount;

            if (downloadType == DownloadType.Video)
            {
                GqlVideoResponse taskInfo = await TwitchHelper.GetVideoInfo(int.Parse(videoId));
                if (taskInfo.data.video == null)
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");

                chatRoot.streamer.name = taskInfo.data.video.owner.displayName;
                chatRoot.streamer.id = int.Parse(taskInfo.data.video.owner.id);
                videoStart = downloadOptions.CropBeginning ? downloadOptions.CropBeginningTime : 0.0;
                videoEnd = downloadOptions.CropEnding ? downloadOptions.CropEndingTime : taskInfo.data.video.lengthSeconds;
            }
            else
            {
                GqlClipResponse taskInfo = await TwitchHelper.GetClipInfo(downloadOptions.Id);
                if (taskInfo.data.clip.video == null || taskInfo.data.clip.videoOffsetSeconds == null)
                    throw new NullReferenceException("Invalid VOD for clip, deleted/expired VOD possibly?");

                downloadOptions.CropBeginning = true;
                downloadOptions.CropBeginningTime = (int)taskInfo.data.clip.videoOffsetSeconds;
                downloadOptions.CropEnding = true;
                downloadOptions.CropEndingTime = downloadOptions.CropBeginningTime + taskInfo.data.clip.durationSeconds;
                chatRoot.streamer.name = taskInfo.data.clip.broadcaster.displayName;
                chatRoot.streamer.id = int.Parse(taskInfo.data.clip.broadcaster.id);
                videoStart = (int)taskInfo.data.clip.videoOffsetSeconds;
                videoEnd = (int)taskInfo.data.clip.videoOffsetSeconds + taskInfo.data.clip.durationSeconds;
                connectionCount = 1;
            }

            chatRoot.video.start = videoStart;
            chatRoot.video.end = videoEnd;
            videoDuration = videoEnd - videoStart;

            SortedSet<Comment> commentsSet = new SortedSet<Comment>(new SortedCommentComparer());
            object commentLock = new object();
            List<Task> tasks = new List<Task>();
            List<int> percentages = new List<int>(connectionCount);

            bool LegacyApiWorks = await CheckLegacyApiAsync(videoId);

            double chunk = videoDuration / connectionCount;
            for (int i = 0; i < connectionCount; i++)
            {
                int tc = i;
                percentages.Add(0);
                var taskProgress = new Progress<ProgressReport>(progressReport =>
                {
                    if (progressReport.reportType != ReportType.Percent)
                    {
                        progress.Report(progressReport);
                    }
                    else
                    {
                        int percent = (int)progressReport.data;
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

                        progress.Report(new ProgressReport() { reportType = ReportType.StatusInfo, data = $"Downloading {percent}%" });
                        progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = percent });
                    }
                });
                double start = videoStart + chunk * i;
                if (LegacyApiWorks)
                {
                    tasks.Add(DownloadSection(taskProgress, cancellationToken, start, start + chunk, videoId, commentsSet, commentLock));
                }
                else
                {
                    tasks.Add(DownloadSectionGql(taskProgress, cancellationToken, start, start + chunk, videoId, commentsSet, commentLock));
                }
            }

            await Task.WhenAll(tasks);

            comments = commentsSet.DistinctBy(x => x._id).ToList();
            chatRoot.comments = comments;

            if (downloadOptions.EmbedData && (downloadOptions.DownloadFormat is ChatFormat.Json or ChatFormat.Html))
            {
                progress.Report(new ProgressReport() { reportType = ReportType.Status, data = "Downloading + Embedding Images" });
                chatRoot.embeddedData = new EmbeddedData();

                List<TwitchEmote> thirdPartyEmotes = new List<TwitchEmote>();
                List<TwitchEmote> firstPartyEmotes = new List<TwitchEmote>();
                List<ChatBadge> twitchBadges = new List<ChatBadge>();
                List<CheerEmote> twitchBits = new List<CheerEmote>();

                thirdPartyEmotes = await TwitchHelper.GetThirdPartyEmotes(chatRoot.streamer.id, downloadOptions.TempFolder, bttv: downloadOptions.BttvEmotes, ffz: downloadOptions.FfzEmotes, stv: downloadOptions.StvEmotes);
                firstPartyEmotes = await TwitchHelper.GetEmotes(comments, downloadOptions.TempFolder);
                twitchBadges = await TwitchHelper.GetChatBadges(chatRoot.streamer.id, downloadOptions.TempFolder);
                twitchBits = await TwitchHelper.GetBits(downloadOptions.TempFolder, chatRoot.streamer.id.ToString());

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

            switch (downloadOptions.DownloadFormat)
            {
                case ChatFormat.Json:
                    await ChatFileTools.WriteJsonChatAsync(downloadOptions.Filename, chatRoot);
                    break;
                case ChatFormat.Html:
                    await ChatFileTools.WriteHtmlChatAsync(downloadOptions.Filename, downloadOptions.EmbedData, chatRoot);
                    break;
                case ChatFormat.Text:
                    await ChatFileTools.WriteTextChatAsync(downloadOptions.Filename, downloadOptions.TimeFormat, chatRoot);
                    break;
                default:
                    throw new NotImplementedException("Requested output chat format is not implemented");
            }
        }

        internal static async Task<bool> CheckLegacyApiAsync(string videoId)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Accept", "application/vnd.twitchtv.v5+json; charset=UTF-8");
                client.Headers.Add("Client-Id", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                try
                {
                    await client.DownloadStringTaskAsync(String.Format("https://api.twitch.tv/v5/videos/{0}/comments?content_offset_seconds={1}", videoId, 0));
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError)
                        return false;
                }
            }
            return true;
        }
    }
}