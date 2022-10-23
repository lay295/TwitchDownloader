using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;
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
                            if (latestMessage < videoEnd && comment.content_offset_seconds > videoStart)
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
        public async Task DownloadAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            DownloadType downloadType = downloadOptions.Id.All(x => Char.IsDigit(x)) ? DownloadType.Video : DownloadType.Clip;
            string videoId = "";

            List<Comment> comments = new List<Comment>();
            ChatRoot chatRoot = new ChatRoot() { streamer = new Streamer(), video = new VideoTime(), comments = comments };

            double videoStart = 0.0;
            double videoEnd = 0.0;
            double videoDuration = 0.0;
            int connectionCount = downloadOptions.ConnectionCount;

            if (downloadType == DownloadType.Video)
            {
                videoId = downloadOptions.Id;
                GqlVideoResponse taskInfo = await TwitchHelper.GetVideoInfo(Int32.Parse(videoId));
                chatRoot.streamer.name = taskInfo.data.video.owner.displayName;
                chatRoot.streamer.id = int.Parse(taskInfo.data.video.owner.id);
                videoStart = downloadOptions.CropBeginning ? downloadOptions.CropBeginningTime : 0.0;
                videoEnd = downloadOptions.CropEnding ? downloadOptions.CropEndingTime : taskInfo.data.video.lengthSeconds;
            }
            else
            {
                GqlClipResponse taskInfo = await TwitchHelper.GetClipInfo(downloadOptions.Id);

                if (taskInfo.data.clip.video == null || taskInfo.data.clip.videoOffsetSeconds == null)
                    throw new Exception("Invalid VOD for clip, deleted/expired VOD possibly?");

                videoId = taskInfo.data.clip.video.id;
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

            double chunk = videoDuration / connectionCount;
            for (int i=0;i<connectionCount;i++)
            {
                int tc = i;
                percentages.Add(0);
                var taskProgress = new Progress<ProgressReport>(progressReport => {
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
                tasks.Add(DownloadSection(taskProgress, cancellationToken, start, start + chunk, videoId, commentsSet, commentLock));
            }

            await Task.WhenAll(tasks);

            comments = commentsSet.ToList();
            chatRoot.comments = comments;

            if (downloadOptions.EmbedEmotes && (downloadOptions.DownloadFormat == DownloadFormat.Json || downloadOptions.DownloadFormat == DownloadFormat.Html))
            {
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Downloading + Embedding Emotes" });
                chatRoot.emotes = new Emotes();
                List<EmbedEmoteData> firstParty = new List<EmbedEmoteData>();
                List<EmbedEmoteData> thirdParty = new List<EmbedEmoteData>();

                string cacheFolder = Path.Combine(Path.GetTempPath(), "TwitchDownloader", "cache");
                List<TwitchEmote> thirdPartyEmotes = new List<TwitchEmote>();
                List<TwitchEmote> firstPartyEmotes = new List<TwitchEmote>();

                thirdPartyEmotes = await TwitchHelper.GetThirdPartyEmotes(chatRoot.streamer.id, cacheFolder, bttv: downloadOptions.BttvEmotes, ffz: downloadOptions.FfzEmotes, stv: downloadOptions.StvEmotes);
                firstPartyEmotes = await TwitchHelper.GetEmotes(comments, cacheFolder);

                foreach (TwitchEmote emote in thirdPartyEmotes)
                {
                    EmbedEmoteData newEmote = new EmbedEmoteData();
                    newEmote.id = emote.Id;
                    newEmote.imageScale = emote.ImageScale;
                    newEmote.data = emote.ImageData;
                    newEmote.name = emote.Name;
                    newEmote.width = emote.Width / emote.ImageScale;
                    newEmote.height = emote.Height / emote.ImageScale;
                    thirdParty.Add(newEmote);
                }
                foreach (TwitchEmote emote in firstPartyEmotes)
                {
                    EmbedEmoteData newEmote = new EmbedEmoteData();
                    newEmote.id = emote.Id;
                    newEmote.imageScale = emote.ImageScale;
                    newEmote.data = emote.ImageData;
                    newEmote.width = emote.Width / emote.ImageScale;
                    newEmote.height = emote.Height / emote.ImageScale;
                    firstParty.Add(newEmote);
                }

                chatRoot.emotes.thirdParty = thirdParty;
                chatRoot.emotes.firstParty = firstParty;
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
                        if (downloadOptions.EmbedEmotes)
                        {
                            EmbedEmoteData embedEmoteData = chatRoot.emotes.thirdParty.FirstOrDefault(x => x.id == item.Id);
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
                            if (downloadOptions.EmbedEmotes)
                            {
                                foreach (var emote in chatRoot.emotes.firstParty)
                                {
                                    finalString.AppendLine(".first-" + emote.id + " { content:url(\"data:image/png;base64, " + Convert.ToBase64String(emote.data) + "\"); }");
                                }
                                foreach (var emote in chatRoot.emotes.thirdParty)
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
                                finalString.Append($"<pre class=\"comment-root\">[{timestamp}] <a href=\"https://www.twitch.tv/{comment.commenter.name}\" target=\"_blank\"><span class=\"comment-author\" {(comment.message.user_color == null ? "" : $"style=\"color: {comment.message.user_color}\"")}>{(comment.commenter.display_name.Any(x => x > 127) ? ($"{comment.commenter.display_name} ({comment.commenter.name})") : comment.commenter.display_name)}</span></a><span class=\"comment-message\">: {GetMessageHtml(downloadOptions.EmbedEmotes, thirdEmoteData, chatRoot, comment)}</span></pre>\n");
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
                    if (embedEmotes && chatRoot.emotes.firstParty.Any(x => x.id == fragment.emoticon.emoticon_id))
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
        public int Compare(Comment x, Comment y)
        {
            return x.content_offset_seconds.CompareTo(y.content_offset_seconds);
        }
    }
}