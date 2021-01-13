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

        public async Task DownloadAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            using (WebClient client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers.Add("Accept", "application/vnd.twitchtv.v5+json; charset=UTF-8");
                client.Headers.Add("Client-Id", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                DownloadType downloadType = downloadOptions.Id.All(x => Char.IsDigit(x)) ? DownloadType.Video : DownloadType.Clip;
                string videoId = "";

                List<Comment> comments = new List<Comment>();
                ChatRoot chatRoot = new ChatRoot() { streamer = new Streamer(), video = new VideoTime(), comments = comments };

                double videoStart = 0.0;
                double videoEnd = 0.0;
                double videoDuration = 0.0;

                if (downloadType == DownloadType.Video)
                {
                    videoId = downloadOptions.Id;
                    JObject taskInfo = await TwitchHelper.GetVideoInfo(Int32.Parse(videoId));
                    chatRoot.streamer.name = taskInfo["channel"]["display_name"].ToString();
                    chatRoot.streamer.id = taskInfo["channel"]["_id"].ToObject<int>();
                    videoStart = downloadOptions.CropBeginning ? downloadOptions.CropBeginningTime : 0.0;
                    videoEnd = downloadOptions.CropEnding ? downloadOptions.CropEndingTime : taskInfo["length"].ToObject<double>();
                }
                else
                {
                    JObject taskInfo = await TwitchHelper.GetClipInfo(downloadOptions.Id);
                    videoId = taskInfo["vod"]["id"].ToString();
                    downloadOptions.CropBeginning = true;
                    downloadOptions.CropBeginningTime = taskInfo["vod"]["offset"].ToObject<int>();
                    downloadOptions.CropEnding = true;
                    downloadOptions.CropEndingTime = downloadOptions.CropBeginningTime + taskInfo["duration"].ToObject<double>();
                    chatRoot.streamer.name = taskInfo["broadcaster"]["display_name"].ToString();
                    chatRoot.streamer.id = taskInfo["broadcaster"]["id"].ToObject<int>();
                    videoStart = taskInfo["vod"]["offset"].ToObject<double>();
                    videoEnd = taskInfo["vod"]["offset"].ToObject<double>() + taskInfo["duration"].ToObject<double>();
                }

                chatRoot.video.start = videoStart;
                chatRoot.video.end = videoEnd;
                videoDuration = videoEnd - videoStart;

                double latestMessage = videoStart - 1;
                bool isFirst = true;
                string cursor = "";

                while (latestMessage < videoEnd)
                {
                    string response;
                    if (isFirst)
                        response = await client.DownloadStringTaskAsync(String.Format("https://api.twitch.tv/v5/videos/{0}/comments?content_offset_seconds={1}", videoId, videoStart));
                    else
                        response = await client.DownloadStringTaskAsync(String.Format("https://api.twitch.tv/v5/videos/{0}/comments?cursor={1}", videoId, cursor));

                    CommentResponse commentResponse = JsonConvert.DeserializeObject<CommentResponse>(response);

                    foreach (var comment in commentResponse.comments)
                    {
                        if (latestMessage < videoEnd && comment.content_offset_seconds > videoStart)
                            comments.Add(comment);

                        latestMessage = comment.content_offset_seconds;
                    }
                    if (commentResponse._next == null)
                        break;
                    else
                        cursor = commentResponse._next;

                    int percent = (int)Math.Floor((latestMessage - videoStart) / videoDuration * 100);
                    progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = percent });
                    progress.Report(new ProgressReport() { reportType = ReportType.MessageInfo, data = $"Downloading {percent}%" });

                    cancellationToken.ThrowIfCancellationRequested();

                    if (isFirst)
                        isFirst = false;

                }

                if (downloadOptions.EmbedEmotes && downloadOptions.IsJson)
                {
                    progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Downloading + Embedding Emotes" });
                    chatRoot.emotes = new Emotes();
                    List<FirstPartyEmoteData> firstParty = new List<FirstPartyEmoteData>();
                    List<ThirdPartyEmoteData> thirdParty = new List<ThirdPartyEmoteData>();

                    string cacheFolder = Path.Combine(Path.GetTempPath(), "TwitchDownloader", "cache");
                    List<ThirdPartyEmote> thirdPartyEmotes = new List<ThirdPartyEmote>();
                    List<KeyValuePair<string, SKBitmap>> firstPartyEmotes = new List<KeyValuePair<string, SKBitmap>>();

                    await Task.Run(() => {
                        thirdPartyEmotes = TwitchHelper.GetThirdPartyEmotes(chatRoot.streamer.id, cacheFolder);
                        firstPartyEmotes = TwitchHelper.GetEmotes(comments, cacheFolder).ToList();
                    });

                    foreach (ThirdPartyEmote emote in thirdPartyEmotes)
                    {
                        ThirdPartyEmoteData newEmote = new ThirdPartyEmoteData();
                        newEmote.id = emote.id;
                        newEmote.imageScale = emote.imageScale;
                        newEmote.data = emote.imageData;
                        newEmote.name = emote.name;
                        thirdParty.Add(newEmote);
                    }
                    foreach (KeyValuePair<string, SKBitmap> emote in firstPartyEmotes)
                    {
                        FirstPartyEmoteData newEmote = new FirstPartyEmoteData();
                        newEmote.id = emote.Key;
                        newEmote.imageScale = 1;
                        newEmote.data = SKImage.FromBitmap(emote.Value).Encode(SKEncodedImageFormat.Png, 100).ToArray();
                        firstParty.Add(newEmote);
                    }

                    chatRoot.emotes.thirdParty = thirdParty;
                    chatRoot.emotes.firstParty = firstParty;
                }

                if (downloadOptions.IsJson)
                {
                    using (TextWriter writer = File.CreateText(downloadOptions.Filename))
                    {
                        var serializer = new JsonSerializer();
                        serializer.Serialize(writer, chatRoot);
                    }
                }
                else
                {
                    using (StreamWriter sw = new StreamWriter(downloadOptions.Filename))
                    {
                        foreach (var comment in chatRoot.comments)
                        {
                            string username = comment.commenter.display_name;
                            string message = comment.message.body;
                            if (downloadOptions.Timestamp)
                            {
                                string timestamp = comment.created_at.ToString("u").Replace("Z", " UTC");
                                sw.WriteLine(String.Format("[{0}] {1}: {2}", timestamp, username, message));
                            }
                            else
                            {
                                sw.WriteLine(String.Format("{0}: {1}", username, message));
                            }
                        }

                        sw.Flush();
                        sw.Close();
                    }
                }
                
                chatRoot = null;
                GC.Collect();
            }
        }
    }
}