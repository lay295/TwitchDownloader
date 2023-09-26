using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.VideoPlatforms.Interfaces;
using TwitchDownloaderCore.VideoPlatforms.Twitch.Gql;
using TwitchDownloaderCore.VideoPlatforms.Twitch;
using System.Text.Json;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using System.Globalization;

namespace TwitchDownloaderCore.VideoPlatforms.Kick.Downloaders
{
    public sealed class KickChatDownloader : IChatDownloader
    {
        private readonly ChatDownloadOptions downloadOptions;
        private readonly IProgress<ProgressReport> _progress;

        private static readonly HttpClient HttpClient = new();
        private static readonly string emotePattern = @"\[emote:(\d+):?([^\]]+)?\]";

        public int StreamerId { get; set; }
        public int ChannelId { get; set; }
        public string VideoId { get; set; }

        public KickChatDownloader(ChatDownloadOptions chatDownloadOptions, IProgress<ProgressReport> progress)
        {
            downloadOptions = chatDownloadOptions;
            downloadOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(downloadOptions.TempFolder) ? Path.GetTempPath() : downloadOptions.TempFolder,
                "TwitchDownloader");
            _progress = progress;

            VideoId = downloadOptions.Id;
        }

        private async Task<List<Comment>> DownloadSection(int streamerId, DateTime videoCreatedAt, double videoStart, double videoEnd, IProgress<ProgressReport> progress, ChatFormat format, CancellationToken cancellationToken)
        {
            var comments = new List<Comment>();
            double videoDuration = videoEnd - videoStart;
            double latestMessage = videoStart - 1;
            string cursor = "";
            int errorCount = 0;

            while (latestMessage < videoEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();

                KickChatResponse chatResponse;
                try
                {
                    DateTime dateTime;
                    if (String.IsNullOrWhiteSpace(cursor))
                    {
                        dateTime = videoCreatedAt.AddSeconds(videoStart);
                    }
                    else
                    {
                        DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(cursor) / 1000);
                        dateTime = dateTimeOffset.UtcDateTime;
                    }

                    string formattedTime = dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                    string response = await Task.Run(() => CurlImpersonate.GetCurlReponse($"https://kick.com/api/v2/channels/{streamerId}/messages?start_time={formattedTime}"));
                    chatResponse = JsonSerializer.Deserialize<KickChatResponse>(response);
                    Console.WriteLine(formattedTime);

                    if (chatResponse.status.error)
                    {
                        throw new Exception("Kick API Error: " + chatResponse.status.message);
                    }

                    errorCount = 0;
                }
                catch (Exception)
                {
                    if (++errorCount > 10)
                    {
                        throw;
                    }

                    await Task.Delay(1_000 * errorCount, cancellationToken);
                    continue;
                }

                cursor = chatResponse.data.cursor;

                var convertedComments = ConvertComments(chatResponse.data.messages, videoCreatedAt, format);
                comments.EnsureCapacity(Math.Min(0, comments.Capacity + convertedComments.Count));
                foreach (var comment in convertedComments)
                {
                    if (latestMessage < videoEnd && comment.content_offset_seconds > videoStart)
                        comments.Add(comment);

                    latestMessage = comment.content_offset_seconds;
                }

                Console.WriteLine(latestMessage);

                if (progress != null)
                {
                    int percent = (int)Math.Floor((latestMessage - videoStart) / videoDuration * 100);
                    progress.Report(new ProgressReport() { ReportType = ReportType.Percent, Data = percent });
                }
            }

            return comments;
        }

        private List<Comment> ConvertComments(List<KickMessage> messages, DateTime videoCreatedAt, ChatFormat format)
        {
            List<Comment> returnList = new List<Comment>(messages.Count);
            videoCreatedAt = videoCreatedAt.ToUniversalTime();
            foreach (var comment in messages)
            {
                var newComment = new Comment
                {
                    _id = comment.id,
                    created_at = comment.created_at,
                    channel_id = StreamerId.ToString(),
                    content_type = "video",
                    content_id = VideoId,
                    content_offset_seconds = comment.created_at.ToUniversalTime().Subtract(videoCreatedAt).TotalSeconds,
                    commenter = new Commenter
                    {
                        display_name = comment.sender.username,
                        _id = comment.sender.id.ToString(),
                        name = comment.sender.slug
                    }
                };

                newComment.message = new() { fragments = new(), user_badges = new() };

                List<KickStringInfo> stringsInfoList = new List<KickStringInfo>();
                StringBuilder replacedStringBuilder = new StringBuilder();
                int lastMatchEnd = 0;

                foreach (Match match in Regex.Matches(comment.content, emotePattern))
                {
                    if (lastMatchEnd != match.Index)
                    {
                        string unmatchedString = comment.content.Substring(lastMatchEnd, match.Index - lastMatchEnd);
                        stringsInfoList.Add(new KickStringInfo
                        {
                            Value = unmatchedString,
                            StartIndex = replacedStringBuilder.Length,
                            EndIndex = replacedStringBuilder.Length + unmatchedString.Length
                        });
                        replacedStringBuilder.Append(unmatchedString);
                    }


                    int emoteId = int.Parse(match.Groups[1].Value);
                    string emoteName = match.Groups[2].Value; // This can be null if emote name is not present

                    // If emote name is not present, use a placeholder or leave it empty
                    emoteName = emoteName ?? $"emote_{emoteId}";

                    stringsInfoList.Add(new KickStringInfo
                    {
                        Value = emoteName,
                        StartIndex = replacedStringBuilder.Length,
                        EndIndex = replacedStringBuilder.Length + emoteName.Length,
                        EmoteId = emoteId
                    });
                    replacedStringBuilder.Append(emoteName);

                    lastMatchEnd = match.Index + match.Length;
                }

                if (lastMatchEnd < comment.content.Length)
                {
                    string remainingString = comment.content.Substring(lastMatchEnd);
                    stringsInfoList.Add(new KickStringInfo
                    {
                        Value = remainingString,
                        StartIndex = replacedStringBuilder.Length,
                        EndIndex = replacedStringBuilder.Length + remainingString.Length
                    });
                    replacedStringBuilder.Append(remainingString);
                }

                string replacedString = replacedStringBuilder.ToString();

                newComment.message.body = replacedString;
                newComment.message.user_color = comment.sender.identity.color;

                foreach (var fragment in stringsInfoList)
                {
                    var newFragment = new Fragment() { text = fragment.Value };
                    if (fragment.EmoteId != null)
                    {
                        newFragment.emoticon = new() { emoticon_id = fragment.EmoteId?.ToString() };
                    }
                    newComment.message.fragments.Add(newFragment);

                    if (fragment.EmoteId != null)
                    {
                        if (newComment.message.emoticons == null)
                            newComment.message.emoticons = new();

                        newComment.message.emoticons.Add(new Emoticon2() { _id = fragment.EmoteId?.ToString(), begin = fragment.StartIndex, end = fragment.EndIndex });
                    }
                }

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

            if (downloadOptions.DownloadFormat == ChatFormat.Html)
            {
                throw new NotImplementedException("Kick chat download as HTML is not currently supported");
            }

            bool vodParsed = UrlParse.TryParseVod(downloadOptions.Id, out VideoPlatform videoPlatformVod, out string vodId);

            ChatRoot chatRoot = new()
            {
                FileInfo = new ChatRootInfo { Version = ChatRootVersion.CurrentVersion, CreatedAt = DateTime.Now },
                streamer = new(),
                video = new(),
                comments = new List<Comment>(),
                videoPlatform = VideoPlatform.Kick
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

            if (downloadOptions.DownloadType == ChatDownloadType.Video)
            {
                KickVideoResponse videoInfo = await KickHelper.GetVideoInfo(videoId);
                if (videoInfo.id == 0)
                {
                    throw new NullReferenceException("Invalid VOD, deleted/expired VOD possibly?");
                }

                chatRoot.streamer.name = videoInfo.StreamerName;
                StreamerId = videoInfo.livestream.channel.user_id;
                ChannelId = videoInfo.livestream.channel_id;
                chatRoot.streamer.id = StreamerId;
                videoTitle = videoInfo.Title;
                videoCreatedAt = videoInfo.CreatedAt;
                videoStart = downloadOptions.CropBeginning ? downloadOptions.CropBeginningTime : 0.0;
                videoEnd = downloadOptions.CropEnding ? downloadOptions.CropEndingTime : videoInfo.Duration;
                videoTotalLength = videoInfo.Duration;
                viewCount = videoInfo.ViewCount;
                game = videoInfo.Game ?? "Unknown";
            }
            else
            {
                throw new NotImplementedException();
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
                viewCount = clipInfoResponse.data.clip.viewCount;
                game = clipInfoResponse.data.clip.game?.displayName ?? "Unknown";
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

            int downloadChunks = Math.Max(1, (int)videoDuration);
            var tasks = new List<Func<Task<List<Comment>>>>();
            var percentages = new int[downloadChunks];

            double chunk = videoDuration / downloadChunks;
            for (int i = 0; i < downloadChunks; i++)
            {
                int tc = i;

                Progress<ProgressReport> taskProgress = null;
                if (!downloadOptions.Silent)
                {
                    taskProgress = new Progress<ProgressReport>(progressReport =>
                    {
                        if (progressReport.ReportType != ReportType.Percent)
                        {
                            _progress.Report(progressReport);
                        }
                        else
                        {
                            var percent = (int)progressReport.Data;
                            if (percent > 100)
                            {
                                percent = 100;
                            }

                            percentages[tc] = percent;

                            percent = 0;
                            for (int j = 0; j < downloadChunks; j++)
                            {
                                percent += percentages[j];
                            }

                            percent /= downloadChunks;

                            _progress.Report(new ProgressReport() { ReportType = ReportType.SameLineStatus, Data = $"Downloading {percent}%" });
                            _progress.Report(new ProgressReport() { ReportType = ReportType.Percent, Data = percent });
                        }
                    });
                }

                double start = videoStart + chunk * i;
                tasks.Add(() => DownloadSection(ChannelId, videoCreatedAt, start, start + chunk, taskProgress, downloadOptions.DownloadFormat, cancellationToken));
            }

            /* Back to using a semaphore, at least for Kick chat downloading. 
             * Ran into issues just having number of tasks equaling number of connection count
             * due to long running stragglers (last 10% took longer than first 90%) */
            var results = await RunTasksWithLimitedConcurrency(downloadOptions.ConnectionCount, tasks);

            var sortedComments = new List<Comment>(results.Length);
            foreach (var commentTask in results)
            {
                sortedComments.AddRange(commentTask);
            }

            sortedComments.Sort(new SortedCommentComparer());

            chatRoot.comments = sortedComments.DistinctBy(x => x._id).ToList();

            if (downloadOptions.EmbedData && downloadOptions.DownloadFormat is ChatFormat.Json or ChatFormat.Html)
            {
                //TODO: Implement emote embeds
            }

            _progress.Report(new ProgressReport(ReportType.NewLineStatus, "Writing output file"));
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
                    throw new NotSupportedException($"{downloadOptions.DownloadFormat} is not a supported output format.");
            }
        }

        static async Task<List<Comment>[]> RunTasksWithLimitedConcurrency(int degreeOfParallelism, List<Func<Task<List<Comment>>>> tasks)
        {
            var semaphore = new SemaphoreSlim(degreeOfParallelism);
            var taskList = new List<Task<List<Comment>>>();

            foreach (var task in tasks)
            {
                await semaphore.WaitAsync();

                taskList.Add(Task.Run(async () =>
                {
                    try
                    {
                        return await task();
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            var results = await Task.WhenAll(taskList);
            return results;
        }
    }

    public class KickStringInfo
    {
        public string Value { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public int? EmoteId { get; set; }
    }
}
