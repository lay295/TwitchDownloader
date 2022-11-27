using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;
using TwitchDownloaderCore.TwitchObjects.Gql;

namespace TwitchDownloaderCore
{
    public class ChatDownloader
    {
        ChatDownloadOptions downloadOptions;
        enum DownloadType { Clip, Video }

        public ChatDownloader(ChatDownloadOptions DownloadOptions)
        {
            downloadOptions = DownloadOptions;
        }

        public async Task DownloadSection(IProgress<ProgressReport> progress, CancellationToken cancellationToken, double videoStart, double videoEnd, string videoId, SortedSet<Comment> comments, object commentLock)
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

        private async Task DownloadSectionGql(IProgress<ProgressReport> progress, CancellationToken cancellationToken, double videoStart, double videoEnd, string videoId, SortedSet<Comment> comments, object commentLock)
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

        private List<Comment> ConvertComments(CommentVideo video)
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
            DownloadType downloadType = downloadOptions.Id.All(x => char.IsDigit(x)) ? DownloadType.Video : DownloadType.Clip;

            List<Comment> comments = new List<Comment>();
            ChatRoot chatRoot = new ChatRoot() { FileInfo = new ChatRootInfo() { Version = new ChatRootVersion(1, 1, 0) }, streamer = new Streamer(), video = new Video(), comments = comments };

            string videoId = "";
            string videoTitle = "";
            DateTime videoCreatedAt;
            double videoStart = 0.0;
            double videoEnd = 0.0;
            double videoDuration = 0.0;
            double videoTotalLength;
            int connectionCount = downloadOptions.ConnectionCount;

            if (downloadType == DownloadType.Video)
            {
                videoId = downloadOptions.Id;
                GqlVideoResponse taskVideoInfo = await TwitchHelper.GetVideoInfo(int.Parse(videoId));
                chatRoot.streamer.name = taskVideoInfo.data.video.owner.displayName;
                chatRoot.streamer.id = int.Parse(taskVideoInfo.data.video.owner.id);
                videoTitle = taskVideoInfo.data.video.title;
                videoCreatedAt = taskVideoInfo.data.video.createdAt;
                videoStart = downloadOptions.CropBeginning ? downloadOptions.CropBeginningTime : 0.0;
                videoEnd = downloadOptions.CropEnding ? downloadOptions.CropEndingTime : taskVideoInfo.data.video.lengthSeconds;
                videoTotalLength = taskVideoInfo.data.video.lengthSeconds;
            }
            else
            {
                GqlClipResponse taskClipInfo = await TwitchHelper.GetClipInfo(downloadOptions.Id);

                if (taskClipInfo.data.clip.video == null || taskClipInfo.data.clip.videoOffsetSeconds == null)
                    throw new Exception("Invalid VOD for clip, deleted/expired VOD possibly?");

                videoId = taskClipInfo.data.clip.video.id;
                downloadOptions.CropBeginning = true;
                downloadOptions.CropBeginningTime = (int)taskClipInfo.data.clip.videoOffsetSeconds;
                downloadOptions.CropEnding = true;
                downloadOptions.CropEndingTime = downloadOptions.CropBeginningTime + taskClipInfo.data.clip.durationSeconds;
                chatRoot.streamer.name = taskClipInfo.data.clip.broadcaster.displayName;
                chatRoot.streamer.id = int.Parse(taskClipInfo.data.clip.broadcaster.id);
                videoTitle = taskClipInfo.data.clip.title;
                videoCreatedAt = taskClipInfo.data.clip.createdAt;
                videoStart = (int)taskClipInfo.data.clip.videoOffsetSeconds;
                videoEnd = (int)taskClipInfo.data.clip.videoOffsetSeconds + taskClipInfo.data.clip.durationSeconds;
                videoTotalLength = taskClipInfo.data.clip.durationSeconds;
                connectionCount = 1;
            }

            chatRoot.video.title = videoTitle;
            chatRoot.video.id = videoId;
            chatRoot.video.created_at = videoCreatedAt;
            chatRoot.video.start = videoStart;
            chatRoot.video.end = videoEnd;
            chatRoot.video.length = videoTotalLength;
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
                        int percent = (int)(progressReport.data);
                        if (percent > 100)
                        {
                            percent = 100;
                        }

                        percentages[tc] = percent;

                        percent = 0;
                        for (int j = 0; j < connectionCount; j++)
                            percent += percentages[j];
                        percent = percent / connectionCount;

                        progress.Report(new ProgressReport() { reportType = ReportType.MessageInfo, data = $"Downloading {percent}%" });
                        progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = percent });
                    }
                });
                double start = videoStart + chunk * i;
                if (LegacyApiWorks)
                    tasks.Add(DownloadSection(taskProgress, cancellationToken, start, start + chunk, videoId, commentsSet, commentLock));
                else
                    tasks.Add(DownloadSectionGql(taskProgress, cancellationToken, start, start + chunk, videoId, commentsSet, commentLock));
            }

            await Task.WhenAll(tasks);

            comments = commentsSet.DistinctBy(x => x._id).ToList();
            chatRoot.comments = comments;

            if (downloadOptions.EmbedData && (downloadOptions.DownloadFormat == DownloadFormat.Json || downloadOptions.DownloadFormat == DownloadFormat.Html))
            {
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Downloading + Embedding Images" });
                chatRoot.embeddedData = new EmbeddedData();
                List<EmbedEmoteData> firstPartyReturnList = new List<EmbedEmoteData>();
                List<EmbedEmoteData> thirdPartyReturnList = new List<EmbedEmoteData>();
                List<EmbedChatBadge> badgesReturnList = new List<EmbedChatBadge>();
                List<EmbedCheerEmote> bitsReturnList = new List<EmbedCheerEmote>();

                string cacheFolder = Path.Combine(Path.GetTempPath(), "TwitchDownloader", "cache");
                List<TwitchEmote> thirdPartyEmotes = new List<TwitchEmote>();
                List<TwitchEmote> firstPartyEmotes = new List<TwitchEmote>();
                List<ChatBadge> twitchBadges = new List<ChatBadge>();
                List<CheerEmote> twitchBits = new List<CheerEmote>();

                thirdPartyEmotes = await TwitchHelper.GetThirdPartyEmotes(chatRoot.streamer.id, cacheFolder, bttv: downloadOptions.BttvEmotes, ffz: downloadOptions.FfzEmotes, stv: downloadOptions.StvEmotes);
                firstPartyEmotes = await TwitchHelper.GetEmotes(comments, cacheFolder);
                twitchBadges = await TwitchHelper.GetChatBadges(chatRoot.streamer.id, cacheFolder);
                twitchBits = await TwitchHelper.GetBits(cacheFolder, chatRoot.streamer.id.ToString());

                foreach (TwitchEmote emote in thirdPartyEmotes)
                {
                    EmbedEmoteData newEmote = new EmbedEmoteData();
                    newEmote.id = emote.Id;
                    newEmote.imageScale = emote.ImageScale;
                    newEmote.data = emote.ImageData;
                    newEmote.name = emote.Name;
                    newEmote.width = emote.Width / emote.ImageScale;
                    newEmote.height = emote.Height / emote.ImageScale;
                    thirdPartyReturnList.Add(newEmote);
                }
                foreach (TwitchEmote emote in firstPartyEmotes)
                {
                    EmbedEmoteData newEmote = new EmbedEmoteData();
                    newEmote.id = emote.Id;
                    newEmote.imageScale = emote.ImageScale;
                    newEmote.data = emote.ImageData;
                    newEmote.width = emote.Width / emote.ImageScale;
                    newEmote.height = emote.Height / emote.ImageScale;
                    firstPartyReturnList.Add(newEmote);
                }
                foreach (ChatBadge badge in twitchBadges)
                {
                    EmbedChatBadge newBadge = new EmbedChatBadge();
                    newBadge.name = badge.Name;
                    newBadge.versions = badge.VersionsData;
                    badgesReturnList.Add(newBadge);
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
                    bitsReturnList.Add(newBit);
                }

                chatRoot.embeddedData.thirdParty = thirdPartyReturnList;
                chatRoot.embeddedData.firstParty = firstPartyReturnList;
                chatRoot.embeddedData.twitchBadges = badgesReturnList;
                chatRoot.embeddedData.twitchBits = bitsReturnList;
            }

            if (downloadOptions.DownloadFormat == DownloadFormat.Json)
            {
                using (TextWriter writer = File.CreateText(downloadOptions.Filename))
                {
                    var serializer = new JsonSerializer();
                    serializer.Serialize(writer, chatRoot);
                }
            }
            else if (downloadOptions.DownloadFormat == DownloadFormat.Text)
            {
                using (StreamWriter sw = new StreamWriter(downloadOptions.Filename))
                {
                    foreach (var comment in chatRoot.comments)
                    {
                        string username = comment.commenter.display_name;
                        string message = comment.message.body;
                        if (downloadOptions.TimeFormat == TimestampFormat.Utc)
                        {
                            string timestamp = comment.created_at.ToString("u").Replace("Z", " UTC");
                            sw.WriteLine(String.Format("[{0}] {1}: {2}", timestamp, username, message));
                        }
                        else if (downloadOptions.TimeFormat == TimestampFormat.Relative)
                        {
                            TimeSpan time = new TimeSpan(0, 0, (int)comment.content_offset_seconds);
                            string timestamp = time.ToString(@"h\:mm\:ss");
                            sw.WriteLine(String.Format("[{0}] {1}: {2}", timestamp, username, message));
                        }
                        else if (downloadOptions.TimeFormat == TimestampFormat.None)
                        {
                            sw.WriteLine(String.Format("{0}: {1}", username, message));
                        }
                    }

                    sw.Flush();
                    sw.Close();
                }
            }
            else if (downloadOptions.DownloadFormat == DownloadFormat.Html)
            {
                Dictionary<string, EmbedEmoteData> thirdEmoteData = null;
                EmoteResponse emotes = await TwitchHelper.GetThirdPartyEmoteData(chatRoot.streamer.id.ToString(), true, true, true);
                thirdEmoteData = new Dictionary<string, EmbedEmoteData>();
                List<EmoteResponseItem> itemList = new List<EmoteResponseItem>();
                itemList.AddRange(emotes.BTTV);
                itemList.AddRange(emotes.FFZ);
                itemList.AddRange(emotes.STV);

                foreach (var item in itemList)
                {
                    if (!thirdEmoteData.ContainsKey(item.Code))
                    {
                        if (downloadOptions.EmbedData)
                        {
                            EmbedEmoteData embedEmoteData = chatRoot.embeddedData.thirdParty.FirstOrDefault(x => x.id == item.Id);
                            if (embedEmoteData != null)
                            {
                                embedEmoteData.url = item.ImageUrl.Replace("[scale]", "1");
                                thirdEmoteData[item.Code] = embedEmoteData;
                            }
                        }
                        else
                        {
                            EmbedEmoteData embedEmoteData = new EmbedEmoteData();
                            embedEmoteData.url = item.ImageUrl.Replace("[scale]", "1");
                            thirdEmoteData[item.Code] = embedEmoteData;
                        }
                    }
                }

                List<string> templateStrings = new List<string>(Properties.Resources.template.Split('\n'));
                StringBuilder finalString = new StringBuilder();

                for (int i = 0; i < templateStrings.Count; i++)
                {
                    switch (templateStrings[i].TrimEnd('\r', '\n'))
                    {
                        case "<!-- TITLE -->":
                            finalString.AppendLine(HttpUtility.HtmlEncode(Path.GetFileNameWithoutExtension(downloadOptions.Filename)));
                            break;
                        case "/* [CUSTOM CSS] */":
                            if (downloadOptions.EmbedData)
                            {
                                foreach (var emote in chatRoot.embeddedData.firstParty)
                                {
                                    finalString.AppendLine(".first-" + emote.id + " { content:url(\"data:image/png;base64, " + Convert.ToBase64String(emote.data) + "\"); }");
                                }
                                foreach (var emote in chatRoot.embeddedData.thirdParty)
                                {
                                    finalString.AppendLine(".third-" + emote.id + " { content:url(\"data:image/png;base64, " + Convert.ToBase64String(emote.data) + "\"); }");
                                }
                            }
                            break;
                        case "<!-- CUSTOM HTML -->":
                            foreach (Comment comment in chatRoot.comments)
                            {
                                TimeSpan time = new TimeSpan(0, 0, (int)comment.content_offset_seconds);
                                string timestamp = time.ToString(@"h\:mm\:ss");
                                finalString.Append($"<pre class=\"comment-root\">[{timestamp}] <a href=\"https://www.twitch.tv/{comment.commenter.name}\" target=\"_blank\"><span class=\"comment-author\" {(comment.message.user_color == null ? "" : $"style=\"color: {comment.message.user_color}\"")}>{(comment.commenter.display_name.Any(x => x > 127) ? ($"{comment.commenter.display_name} ({comment.commenter.name})") : comment.commenter.display_name)}</span></a><span class=\"comment-message\">: {GetMessageHtml(downloadOptions.EmbedData, thirdEmoteData, chatRoot, comment)}</span></pre>\n");
                            }
                            break;
                        default:
                            finalString.AppendLine(templateStrings[i].TrimEnd('\r', '\n'));
                            break;
                    }
                }

                File.WriteAllText(downloadOptions.Filename, finalString.ToString(), Encoding.Unicode);
            }

            chatRoot = null;
            GC.Collect();
        }

        private async Task<bool> CheckLegacyApiAsync(string videoId)
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

        private string GetMessageHtml(bool embedEmotes, Dictionary<string, EmbedEmoteData> thirdEmoteData, ChatRoot chatRoot, Comment comment)
        {
            StringBuilder message = new StringBuilder();

            if (comment.message.fragments == null)
            {
                comment.message.fragments = new List<Fragment>();
                comment.message.fragments.Add(new Fragment() { text = comment.message.body });
            }

            foreach (var fragment in comment.message.fragments)
            {
                if (fragment.emoticon == null)
                {
                    List<string> wordList = new List<string>(fragment.text.Split(' '));

                    foreach (var word in wordList)
                    {
                        if (thirdEmoteData.ContainsKey(word))
                        {
                            if (embedEmotes)
                            {
                                message.Append($"<img width=\"{thirdEmoteData[word].width}\" height=\"{thirdEmoteData[word].height}\" class=\"emote-image third-{thirdEmoteData[word].id}\" title=\"{word}\"\"><div class=\"invis-text\">{word}</div> ");
                            }
                            else
                            {
                                message.Append($"<img class=\"emote-image\" title=\"{word}\" src=\"{thirdEmoteData[word].url}\"><div class=\"invis-text\">{word}</div> ");
                            }
                        }
                        else if (word != "")
                            message.Append(HttpUtility.HtmlEncode(word) + " ");
                    }
                }
                else
                {
                    if (embedEmotes && chatRoot.embeddedData.firstParty.Any(x => x.id == fragment.emoticon.emoticon_id))
                    {
                        message.Append($"<img class=\"emote-image first-{fragment.emoticon.emoticon_id}\" title=\"{fragment.text}\"><div class=\"invis-text\">{fragment.text}</div> ");
                    }
                    else
                    {
                        message.Append($"<img class=\"emote-image\" src=\"https://static-cdn.jtvnw.net/emoticons/v2/{fragment.emoticon.emoticon_id}/default/dark/1.0\" title=\"{fragment.text}\"><div class=\"invis-text\">{fragment.text}</div> ");
                    }
                }
            }
            return message.ToString();
        }
    }

    internal class SortedCommentComparer : IComparer<Comment>
    {
        // Modified from double.CompareTo
        public int Compare(Comment x, Comment y)
        {
            double m_value = x.content_offset_seconds;
            double value = y.content_offset_seconds;
            if (m_value < value) return -1;
            if (m_value > value) return 1;
            if (m_value == value) return 1;

            // At least one of the values is NaN.
            if (double.IsNaN(m_value))
                return double.IsNaN(value) ? 0 : -1;
            else
                return 1;
        }
    }
}