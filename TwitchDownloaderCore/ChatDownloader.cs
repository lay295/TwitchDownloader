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

                JObject result = new JObject();
                JObject video = new JObject();
                JObject streamer = new JObject();
                JArray comments = new JArray();

                double videoStart = 0.0;
                double videoEnd = 0.0;
                double videoDuration = 0.0;

                if (downloadType == DownloadType.Video)
                {
                    videoId = downloadOptions.Id;
                    JObject taskInfo = await TwitchHelper.GetVideoInfo(Int32.Parse(videoId));
                    streamer["name"] = taskInfo["channel"]["display_name"];
                    streamer["id"] = taskInfo["channel"]["_id"];
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
                    streamer["name"] = taskInfo["broadcaster"]["display_name"];
                    streamer["id"] = taskInfo["broadcaster"]["id"];
                    videoStart = taskInfo["vod"]["offset"].ToObject<double>();
                    videoEnd = taskInfo["vod"]["offset"].ToObject<double>() + taskInfo["duration"].ToObject<double>();
                }

                video["start"] = videoStart;
                video["end"] = videoEnd;
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

                    JObject res = JObject.Parse(response);

                    foreach (var comment in res["comments"])
                    {
                        if (latestMessage < videoEnd && comment["content_offset_seconds"].ToObject<double>() > videoStart)
                            comments.Add(comment);

                        latestMessage = comment["content_offset_seconds"].ToObject<double>();
                    }
                    if (res["_next"] == null)
                        break;
                    else
                        cursor = res["_next"].ToString();

                    int percent = (int)Math.Floor((latestMessage - videoStart) / videoDuration * 100);
                    progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = percent });
                    progress.Report(new ProgressReport() { reportType = ReportType.MessageInfo, data = $"Downloading {percent}%" });

                    cancellationToken.ThrowIfCancellationRequested();

                    if (isFirst)
                        isFirst = false;

                }

                result["streamer"] = streamer;
                result["comments"] = comments;
                result["video"] = video;

                if (downloadOptions.EmbedEmotes && downloadOptions.IsJson)
                {
                    progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Downloading + Embedding Emotes" });
                    result["emotes"] = new JObject();
                    JArray firstParty = new JArray();
                    JArray thirdParty = new JArray();

                    string cacheFolder = Path.Combine(Path.GetTempPath(), "TwitchDownloader", "cache");
                    List<ThirdPartyEmote> thirdPartyEmotes = new List<ThirdPartyEmote>();
                    List<KeyValuePair<string, SKBitmap>> firstPartyEmotes = new List<KeyValuePair<string, SKBitmap>>();

                    await Task.Run(() => {
                        thirdPartyEmotes = TwitchHelper.GetThirdPartyEmotes(streamer["id"].ToObject<int>(), cacheFolder);
                        firstPartyEmotes = TwitchHelper.GetEmotes(result["comments"].ToObject<List<Comment>>(), cacheFolder).ToList();
                    });

                    foreach (ThirdPartyEmote emote in thirdPartyEmotes)
                    {
                        JObject newEmote = new JObject();
                        newEmote["id"] = emote.id;
                        newEmote["imageScale"] = emote.imageScale;
                        newEmote["data"] = emote.imageData;
                        newEmote["name"] = emote.name;
                        thirdParty.Add(newEmote);
                    }
                    foreach (KeyValuePair<string, SKBitmap> emote in firstPartyEmotes)
                    {
                        JObject newEmote = new JObject();
                        newEmote["id"] = emote.Key;
                        newEmote["imageScale"] = 1;
                        newEmote["data"] = SKImage.FromBitmap(emote.Value).Encode(SKEncodedImageFormat.Png, 100).ToArray();
                        firstParty.Add(newEmote);
                    }

                    result["emotes"]["thirdParty"] = thirdParty;
                    result["emotes"]["firstParty"] = firstParty;
                }

                using (StreamWriter sw = new StreamWriter(downloadOptions.Filename))
                {
                    if (downloadOptions.IsJson)
                    {
                        sw.Write(result.ToString(Formatting.None));
                    }
                    else
                    {
                        foreach (var comment in result["comments"])
                        {
                            string username = comment["commenter"]["display_name"].ToString();
                            string message = comment["message"]["body"].ToString();
                            if (downloadOptions.Timestamp)
                            {
                                string timestamp = comment["created_at"].ToObject<DateTime>().ToString("u").Replace("Z", " UTC");
                                sw.WriteLine(String.Format("[{0}] {1}: {2}", timestamp, username, message));
                            }
                            else
                            {
                                sw.WriteLine(String.Format("{0}: {1}", username, message));
                            }
                        }
                    }

                    sw.Flush();
                    sw.Close();
                    result = null;
                }
            }
        }
    }
}