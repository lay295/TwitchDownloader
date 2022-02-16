using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public class ChatRenderer
    {
        ChatRenderOptions renderOptions;
        static SKPaint imagePaint = new SKPaint() { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        static SKPaint emotePaint = new SKPaint() { IsAntialias = true, FilterQuality = SKFilterQuality.High };
        static SKFontManager fontManager = SKFontManager.CreateDefault();
        static ConcurrentDictionary<int, SKPaint> fallbackCache = new ConcurrentDictionary<int, SKPaint>();

        public ChatRenderer(ChatRenderOptions RenderOptions)
        {
            renderOptions = RenderOptions;
        }

        public async Task RenderVideoAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            string tempFolder = renderOptions.TempFolder == null || renderOptions.TempFolder == "" ? Path.Combine(Path.GetTempPath(), "TwitchDownloader") : Path.Combine(renderOptions.TempFolder, "TwitchDownloader");
            string downloadFolder = Path.Combine(tempFolder, "Chat Render", Guid.NewGuid().ToString());
            string cacheFolder = Path.Combine(tempFolder, "cache");
            try
            {
                ChatRoot chatJson = ParseJson();
                List<TwitchComment> finalComments = new List<TwitchComment>();
                List<string> defaultColors = new List<string>() { "#FF0000", "#0000FF", "#00FF00", "#B22222", "#FF7F50", "#9ACD32", "#FF4500", "#2E8B57", "#DAA520", "#D2691E", "#5F9EA0", "#1E90FF", "#FF69B4", "#8A2BE2", "#00FF7F" };

                if (!Directory.Exists(downloadFolder))
                    TwitchHelper.CreateDirectory(downloadFolder);
                if (!Directory.Exists(cacheFolder))
                    TwitchHelper.CreateDirectory(cacheFolder);

                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Fetching Chat Badges" });
                List<ChatBadge> chatBadges = await Task.Run(() => TwitchHelper.GetChatBadges(chatJson.streamer.id));
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Fetching Emotes" });
                List<TwitchEmote> chatEmotes = await Task.Run(() => TwitchHelper.GetEmotes(chatJson.comments, cacheFolder, chatJson.emotes, true));
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Fetching 3rd Party Emotes" });
                List<TwitchEmote> thirdPartyEmotes = await Task.Run(() => TwitchHelper.GetThirdPartyEmotes(chatJson.streamer.id, cacheFolder, chatJson.emotes, renderOptions.BttvEmotes, renderOptions.FfzEmotes, renderOptions.StvEmotes));
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Fetching Cheer Emotes" });
                List<CheerEmote> cheerEmotes = await Task.Run(() => TwitchHelper.GetBits(cacheFolder, chatJson.streamer.id.ToString()));
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Fetching Emojis" });
                Dictionary<string, SKBitmap> emojiCache = await Task.Run(() => TwitchHelper.GetTwitterEmojis(chatJson.comments, cacheFolder));

                CheckCancelation(cancellationToken, downloadFolder);

                Size canvasSize = new Size(renderOptions.ChatWidth, renderOptions.SectionHeight);
                SKPaint nameFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.Font, renderOptions.UsernameFontStyle), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
                SKPaint messageFont = new SKPaint() { Typeface = SKTypeface.FromFamilyName(renderOptions.Font, renderOptions.MessageFontStyle), LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High, Color = renderOptions.MessageColor };

                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Rendering Comments" });
                await Task.Run(() =>
                {
                    foreach (Comment comment in chatJson.comments)
                    {
                        if (comment.source != "chat")
                            continue;
                        if (comment.message.user_notice_params != null && comment.message.user_notice_params.msg_id != null)
                        {
                            if (comment.message.user_notice_params.msg_id != "highlighted-message" && comment.message.user_notice_params.msg_id != "sub" && comment.message.user_notice_params.msg_id != "resub" && comment.message.user_notice_params.msg_id != "subgift" && comment.message.user_notice_params.msg_id != "")
                                continue;
                            if (!renderOptions.SubMessages && (comment.message.user_notice_params.msg_id == "sub" || comment.message.user_notice_params.msg_id == "resub" || comment.message.user_notice_params.msg_id == "subgift"))
                                continue;
                            if (comment.message.user_notice_params.msg_id == "highlighted-message" && comment.message.fragments == null && comment.message.body != null)
                            {
                                comment.message.fragments = new List<Fragment>();
                                comment.message.fragments.Add(new Fragment());
                                comment.message.fragments[0].text = comment.message.body;
                            }
                        }
                        if (comment.message.fragments == null || comment.commenter == null)
                            continue;

                        Point drawPos = new Point(renderOptions.PaddingLeft, 0);
                        SKColor userColor = SKColor.Parse(comment.message.user_color != null ? comment.message.user_color : defaultColors[Math.Abs(comment.commenter.display_name.GetHashCode()) % defaultColors.Count]);
                        userColor = GenerateUserColor(userColor, renderOptions.BackgroundColor, renderOptions);

                        List<SKBitmap> imageList = new List<SKBitmap>();
                        SKBitmap sectionImage = new SKBitmap(canvasSize.Width, canvasSize.Height);
                        int default_x = renderOptions.PaddingLeft;
                        bool accentMessage = false;

                        List<GifEmote> currentGifEmotes = new List<GifEmote>();
                        List<SKBitmap> emoteList = new List<SKBitmap>();
                        List<SKRect> emotePositionList = new List<SKRect>();
                        new SKCanvas(sectionImage).Clear(renderOptions.BackgroundColor);

                        if (comment.message.user_notice_params != null && comment.message.user_notice_params.msg_id != null && (comment.message.user_notice_params.msg_id == "sub" || comment.message.user_notice_params.msg_id == "resub" || comment.message.user_notice_params.msg_id == "subgift"))
                        {
                            accentMessage = true;
                            drawPos.X += (int)(8 * renderOptions.EmoteScale);
                            default_x += (int)(8 * renderOptions.EmoteScale); 
                            sectionImage = DrawMessage(sectionImage, imageList, renderOptions, currentGifEmotes, messageFont, emojiCache, chatEmotes, thirdPartyEmotes, cheerEmotes, comment, canvasSize, ref drawPos, ref default_x, emoteList, emotePositionList);
                        }
                        else
                        {
                            if (renderOptions.Timestamp)
                                sectionImage = DrawTimestamp(sectionImage, imageList, messageFont, renderOptions, comment, canvasSize, ref drawPos, ref default_x);
                            if (renderOptions.ChatBadges)
                                sectionImage = DrawBadges(sectionImage, imageList, renderOptions, chatBadges, comment, canvasSize, ref drawPos);
                            sectionImage = DrawUsername(sectionImage, imageList, renderOptions, nameFont, comment.commenter.display_name, userColor, canvasSize, ref drawPos);
                            sectionImage = DrawMessage(sectionImage, imageList, renderOptions, currentGifEmotes, messageFont, emojiCache, chatEmotes, thirdPartyEmotes, cheerEmotes, comment, canvasSize, ref drawPos, ref default_x, emoteList, emotePositionList);
                        }

                        int finalHeight = 0;
                        foreach (var img in imageList)
                            finalHeight += img.Height;
                        SKBitmap finalImage = new SKBitmap(canvasSize.Width, finalHeight);
                        SKCanvas finalImageCanvas = new SKCanvas(finalImage);
                        finalHeight = 0;
                        finalImageCanvas.Clear(renderOptions.BackgroundColor);
                        foreach (var img in imageList)
                        {
                            finalImageCanvas.DrawBitmap(img, 0, finalHeight);
                            finalHeight += img.Height;
                            img.Dispose();
                        }

                        if (accentMessage)
                            finalImageCanvas.DrawRect(renderOptions.PaddingLeft, 0, (int)(4 * renderOptions.EmoteScale), finalHeight - (int)Math.Floor(.4 * renderOptions.SectionHeight), new SKPaint() { Color = SKColor.Parse("#7b2cf2") });

                        string imagePath = Path.Combine(downloadFolder, Guid.NewGuid() + ".png");
                        finalComments.Add(new TwitchComment() { Section = imagePath, SecondsOffset = Double.Parse(comment.content_offset_seconds.ToString()), GifEmotes = currentGifEmotes, NormalEmotes = emoteList, NormalEmotesPositions = emotePositionList });
                        using (Stream s = File.OpenWrite(imagePath))
                        {
                            SKImage saveImage = SKImage.FromBitmap(finalImage);
                            if (saveImage != null)
                            {
                                saveImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(s);
                                saveImage.Dispose();
                            }
                        }
                        finalImage.Dispose();
                        finalImageCanvas.Dispose();

                        int percent = (int)Math.Floor(((double)finalComments.Count / (double)chatJson.comments.Count) * 100);
                        CheckCancelation(cancellationToken, downloadFolder);
                    }
                });
                
                progress.Report(new ProgressReport() { reportType = ReportType.MessageInfo, data = "Rendering Video 0%" });
                await Task.Run(() => RenderVideo(renderOptions, new Queue<TwitchComment>(finalComments.OrderBy(x => x.SecondsOffset)), chatJson, progress), cancellationToken);
                progress.Report(new ProgressReport() { reportType = ReportType.Message, data = "Cleaning up..." });
                Cleanup(downloadFolder);
            }
            catch
            {
                Cleanup(downloadFolder);
                throw;
            }
        }

        private void CheckCancelation(CancellationToken cancellationToken, string downloadFolder)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Cleanup(downloadFolder);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private void Cleanup(string downloadFolder)
        {
            if (Directory.Exists(downloadFolder))
                Directory.Delete(downloadFolder, true);
        }

        private void RenderVideo(ChatRenderOptions renderOptions, Queue<TwitchComment> finalComments, ChatRoot chatJson, IProgress<ProgressReport> progress = null)
        {
            SKBitmap bufferBitmap = new SKBitmap(renderOptions.ChatWidth, renderOptions.ChatHeight);
            SKCanvas bufferCanvas = new SKCanvas(bufferBitmap);
            SKPaint gifBackgroundPaint = new SKPaint();
            SKPaint gifPaint = new SKPaint();
            int videoStart;
            int duration;
            if (chatJson.video != null)
            {
                int startSeconds = (int)Math.Floor(chatJson.video.start);
                int firstCommentSeconds = (int)Math.Floor(chatJson.comments.First().content_offset_seconds);
                videoStart = startSeconds;
                duration = (int)Math.Ceiling(chatJson.video.end) - videoStart;
            }
            else
            {
                videoStart = (int)Math.Floor(chatJson.comments.First().content_offset_seconds);
                duration = (int)Math.Ceiling(chatJson.comments.Last().content_offset_seconds) - videoStart;
            }
            List<GifEmote> displayedGifs = new List<GifEmote>();

            if (renderOptions.BackgroundColor.Alpha < 255)
            {
                gifPaint.BlendMode = SKBlendMode.SrcOver;
                gifBackgroundPaint.BlendMode = SKBlendMode.Src;
            }

            if (File.Exists(renderOptions.OutputFile))
                File.Delete(renderOptions.OutputFile);

            string inputArgs = renderOptions.InputArgs.Replace("{fps}", renderOptions.Framerate.ToString())
                .Replace("{height}", renderOptions.ChatHeight.ToString()).Replace("{width}", renderOptions.ChatWidth.ToString())
                .Replace("{save_path}", renderOptions.OutputFile).Replace("{max_int}", int.MaxValue.ToString())
                .Replace("{pix_fmt}", bufferBitmap.ColorType == SKColorType.Rgba8888 ? "rgba" : "bgra");
            string outputArgs = renderOptions.OutputArgs.Replace("{fps}", renderOptions.Framerate.ToString())
                .Replace("{height}", renderOptions.ChatHeight.ToString()).Replace("{width}", renderOptions.ChatWidth.ToString())
                .Replace("{save_path}", renderOptions.OutputFile).Replace("{max_int}", int.MaxValue.ToString());

            var process = new Process
            {
                StartInfo =
                {
                    FileName = renderOptions.FfmpegPath,
                    Arguments = $"{inputArgs} {outputArgs}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            //process.ErrorDataReceived += ErrorDataHandler;

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Process maskProcess = null;
            BinaryWriter maskStream = null;
            if (renderOptions.GenerateMask)
            {
                string outputArgsMask = renderOptions.OutputArgs.Replace("{fps}", renderOptions.Framerate.ToString())
                .Replace("{height}", renderOptions.ChatHeight.ToString()).Replace("{width}", renderOptions.ChatWidth.ToString())
                .Replace("{save_path}", renderOptions.OutputFileMask).Replace("{max_int}", int.MaxValue.ToString());
                maskProcess = new Process
                {
                    StartInfo =
                    {
                        FileName = renderOptions.FfmpegPath,
                        Arguments = $"{inputArgs} {outputArgsMask}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                if (File.Exists(renderOptions.OutputFileMask))
                    File.Delete(renderOptions.OutputFileMask);

                maskProcess.Start();
                maskProcess.BeginErrorReadLine();
                maskProcess.BeginOutputReadLine();
                maskStream = new BinaryWriter(maskProcess.StandardInput.BaseStream);
            }

            using (var ffmpegStream = new BinaryWriter(process.StandardInput.BaseStream))
            {
                bufferCanvas.Clear(renderOptions.BackgroundColor);
                int startTick = (int)Math.Floor(videoStart / (1.0 / renderOptions.Framerate));
                int endTick = (int)Math.Floor((videoStart + duration) / (1.0 / renderOptions.Framerate));
                int lastUpdateTick = startTick;
                int globalTick = startTick;
                for (int i = startTick; i < endTick; i++)
                {
                    int height = 0;
                    if (globalTick % renderOptions.UpdateFrame == 0)
                    {
                        int y = 0;
                        List<GifEmote> old = new List<GifEmote>(displayedGifs);
                        List<GifEmote> newly_added = new List<GifEmote>();
                        List<TwitchComment> new_comments = new List<TwitchComment>();

                        bool isDone = false;
                        while (finalComments.Count > 0 && !isDone)
                        {
                            int commentTick = (int)Math.Floor(finalComments.Peek().SecondsOffset / (1.0 / renderOptions.Framerate));

                            if (commentTick < startTick)
                            {
                                finalComments.Dequeue();
                                continue;
                            }

                            if (commentTick >= lastUpdateTick && commentTick < globalTick)
                            {
                                TwitchComment currentComment = finalComments.Dequeue();
                                foreach (var emote in currentComment.GifEmotes)
                                {
                                    GifEmote newGif = new GifEmote(new Point(emote.Offset.X, emote.Offset.Y + height), emote.Name, emote.Codec, emote.ImageScale, emote.ImageFrames);
                                    displayedGifs.Add(newGif);
                                    newly_added.Add(newGif);
                                }
                                height += SKBitmap.Decode(currentComment.Section).Height;
                                new_comments.Add(currentComment);
                            }
                            else
                            {
                                isDone = true;
                            }
                        }
                        foreach (var emote in old)
                            emote.Offset = new Point(emote.Offset.X, emote.Offset.Y - height);
                        foreach (var emote in newly_added)
                        {
                            emote.Offset = new Point(emote.Offset.X, (renderOptions.ChatHeight - height) + emote.Offset.Y);
                        }


                        if (height > 0)
                        {
                            List<SKBitmap> emoteList = new List<SKBitmap>();
                            List<SKRect> emotePos = new List<SKRect>();
                            SKBitmap sectionBitmap = new SKBitmap(renderOptions.ChatWidth, height);
                            SKCanvas sectionCanvas = new SKCanvas(sectionBitmap);
                            sectionCanvas.Clear(renderOptions.BackgroundColor);

                            for (int j = 0; j < new_comments.Count; j++)
                            {
                                int commentTick = (int)Math.Floor(new_comments[j].SecondsOffset / (1.0 / renderOptions.Framerate));
                                if (commentTick >= lastUpdateTick && commentTick < globalTick)
                                {
                                    SKBitmap sectionImage = SKBitmap.Decode(new_comments[j].Section);
                                    sectionCanvas.DrawBitmap(sectionImage, 0, y);
                                    for (int k = 0; k < new_comments[j].NormalEmotes.Count; k++)
                                    {
                                        emoteList.Add(new_comments[j].NormalEmotes[k]);
                                        SKRect refrenceRect = new_comments[j].NormalEmotesPositions[k];
                                        float top = bufferBitmap.Height - sectionBitmap.Height + y + refrenceRect.Top;
                                        emotePos.Add(new SKRect(refrenceRect.Left, top, refrenceRect.Right, top + (refrenceRect.Bottom - refrenceRect.Top)));
                                    }
                                    y += sectionImage.Height;
                                }
                            }
                            if (renderOptions.BackgroundColor.Alpha < 255)
                            {
                                SKBitmap bufferCopy = bufferBitmap.Copy();
                                bufferCanvas.Clear();
                                bufferCanvas.DrawBitmap(bufferCopy, 0, -height);
                                bufferCopy.Dispose();
                            }
                            else
                            {
                                bufferCanvas.DrawBitmap(bufferBitmap, 0, -height);
                            }
                            bufferCanvas.DrawBitmap(sectionBitmap, 0, renderOptions.ChatHeight - height);

                            for (int k = 0; k < emoteList.Count; k++)
                                bufferCanvas.DrawBitmap(emoteList[k], emotePos[k], emotePaint);

                            foreach (var emote in newly_added)
                            {
                                float temp_x = (float)emote.Offset.X;
                                float temp_y = (float)emote.Offset.Y + (int)Math.Floor((renderOptions.SectionHeight - ((emote.ImageFrames.First().Height / emote.ImageScale) * renderOptions.EmoteScale)) / 2.0);
                                SKRect copyRect = new SKRect(temp_x, temp_y, temp_x + (float)((emote.ImageFrames.First().Width / emote.ImageScale) * renderOptions.EmoteScale), temp_y + (float)((emote.ImageFrames.First().Height / emote.ImageScale) * renderOptions.EmoteScale));
                                emote.BackgroundImage = new SKBitmap((int)copyRect.Width, (int)copyRect.Height);
                                using (SKCanvas tempCanvas = new SKCanvas(emote.BackgroundImage))
                                {
                                    tempCanvas.Clear(renderOptions.BackgroundColor);
                                    tempCanvas.DrawBitmap(bufferBitmap, copyRect,
                                        new SKRect(0, 0, copyRect.Width, copyRect.Height));
                                }
                            }
                        }
                        lastUpdateTick = globalTick;
                    }

                    List<GifEmote> to_remove = new List<GifEmote>();
                    foreach (var emote in displayedGifs)
                    {
                        if (emote.Offset.Y < -emote.Width - renderOptions.ChatHeight)
                        {
                            to_remove.Add(emote);
                        }
                        else
                        {
                            int gifTime = (int)Math.Floor((1.5 * globalTick / (renderOptions.Framerate / 60.0)) % emote.TotalDuration);
                            int frame = emote.FrameCount - 1;
                            int timeCount = 0;
                            for (int k = 0; k < emote.DurationList.Count; k++)
                            {
                                if (timeCount + emote.DurationList[k] > gifTime)
                                {
                                    frame = k;
                                    break;
                                }
                                timeCount += emote.DurationList[k];
                            }

                            SKBitmap gifBitmap = emote.ImageFrames[frame];
                            float x = (float)emote.Offset.X;
                            float y = (float)emote.Offset.Y + (int)Math.Floor((renderOptions.SectionHeight - ((gifBitmap.Height / emote.ImageScale) * renderOptions.EmoteScale)) / 2.0);
                            bufferCanvas.DrawBitmap(gifBitmap, new SKRect(x, y, x + (float)((gifBitmap.Width / emote.ImageScale) * renderOptions.EmoteScale), y + (float)((gifBitmap.Height / emote.ImageScale) * renderOptions.EmoteScale)), gifPaint);
                        }
                    }

                    foreach (var emote in to_remove)
                    {
                        displayedGifs.Remove(emote);
                    }

                    var pix = bufferBitmap.PeekPixels();
                    var data = SKData.Create(pix.GetPixels(), pix.Info.BytesSize);
                    var bytes = data.ToArray();
                    ffmpegStream.Write(bytes);
                    if (renderOptions.GenerateMask)
                    {
                        byte[] bytesMask = GetMaskBytes(bufferBitmap, renderOptions);
                        maskStream.Write(bytesMask);
                    }

                    foreach (var emote in displayedGifs)
                    {
                        SKRect drawRect = new SKRect((float)emote.Offset.X, (float)emote.Offset.Y + (int)Math.Floor((renderOptions.SectionHeight - ((emote.Height / emote.ImageScale) * renderOptions.EmoteScale)) / 2.0), (float)emote.Offset.X + (float)((emote.Width / emote.ImageScale) * renderOptions.EmoteScale), (float)emote.Offset.Y + (int)Math.Floor((renderOptions.SectionHeight - ((emote.Height / emote.ImageScale) * renderOptions.EmoteScale)) / 2.0) + (float)((emote.Height / emote.ImageScale) * renderOptions.EmoteScale));
                        bufferCanvas.DrawBitmap(emote.BackgroundImage, drawRect, gifBackgroundPaint);
                    }
                    globalTick += 1;
                    double percentDouble = (double)(globalTick - startTick) / (double)(endTick - startTick) * 100.0;
                    int percentInt = (int)Math.Floor(percentDouble);
                    if (progress != null)
                    {
                        progress.Report(new ProgressReport() { reportType = ReportType.Percent, data = percentInt });
                        int timeLeftInt = (int)Math.Floor(100.0 / percentDouble * stopwatch.Elapsed.TotalSeconds) - (int)stopwatch.Elapsed.TotalSeconds;
                        TimeSpan timeLeft = new TimeSpan(0, 0, timeLeftInt);
                        progress.Report(new ProgressReport() { reportType = ReportType.MessageInfo, data = $"Rendering Video {percentInt}% ({timeLeft.ToString(@"h\hm\ms\s")} left)" });
                    } 
                }
            }
            if (renderOptions.GenerateMask)
            {
                maskStream.Dispose();
                maskProcess.WaitForExit();
            }
            stopwatch.Stop();
            progress.Report(new ProgressReport() { reportType = ReportType.Log, data = $"FINISHED. RENDER TIME: {(int)stopwatch.Elapsed.TotalSeconds}s SPEED: {(duration / stopwatch.Elapsed.TotalSeconds).ToString("0.##")}x" });
            process.WaitForExit();
        }

        private void ErrorDataHandler(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private byte[] GetMaskBytes(SKBitmap bufferBitmap, ChatRenderOptions renderOptions)
        {
            SKBitmap maskBitmap = new SKBitmap(renderOptions.ChatWidth, renderOptions.ChatHeight);
            using (SKCanvas maskCanvas = new SKCanvas(maskBitmap))
            {
                maskCanvas.Clear(SKColors.White);
                maskCanvas.DrawBitmap(bufferBitmap, 0, 0, new SKPaint() { BlendMode = SKBlendMode.DstIn });
            }
            var pixMask = maskBitmap.PeekPixels();
            var dataMask = SKData.Create(pixMask.GetPixels(), pixMask.Info.BytesSize);
            var bytesMask = dataMask.ToArray();
            return bytesMask;
        }

        public static SKBitmap DrawTimestamp(SKBitmap sectionImage, List<SKBitmap> imageList, SKPaint messageFont, ChatRenderOptions renderOptions, Comment comment, Size canvasSize, ref Point drawPos, ref int default_x)
        {
            SKCanvas sectionImageCanvas = new SKCanvas(sectionImage);
            float xPos = (float)drawPos.X;
            float yPos = (float)(((canvasSize.Height - renderOptions.FontSize) / 2) + renderOptions.FontSize) - (float)(renderOptions.EmoteScale * 2);
            TimeSpan timestamp = new TimeSpan(0, 0, (int)comment.content_offset_seconds);
            string timeString = timestamp.ToString(@"h\:mm\:ss");
            float textWidth = messageFont.MeasureText(timeString);
            if (renderOptions.Outline)
            {
                SKPath outlinePath = messageFont.GetTextPath(timeString, xPos, yPos);
                SKPaint outlinePaint = new SKPaint() { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(renderOptions.OutlineSize * renderOptions.EmoteScale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
                sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
            }
            sectionImageCanvas.DrawText(timeString, xPos, yPos, messageFont);
            drawPos.X += (int)Math.Floor(textWidth) + (int)Math.Floor(6 * renderOptions.EmoteScale);
            default_x = (int)drawPos.X;
            return sectionImage;
        }
        public SKBitmap DrawBadges(SKBitmap sectionImage, List<SKBitmap> imageList, ChatRenderOptions renderOptions, List<ChatBadge> chatBadges, Comment comment, Size canvasSize, ref Point drawPos)
        {
            //A little easter egg for my Twitch username won't hurt :)
            try
            {
                if (comment.commenter.name == "ilovekeepo69" && chatBadges.Any(x => x.Name == "ilovekeepo69"))
                {
                    SKBitmap badgeImage = chatBadges.Where(x => x.Name == "ilovekeepo69").First().Versions["1"];
                    using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                    {
                        float imageRatio = (float)(renderOptions.EmoteScale * 0.5);
                        float imageSize = badgeImage.Width * imageRatio;
                        float left = (float)drawPos.X;
                        float right = imageSize + left;
                        float top = (float)((sectionImage.Height - imageSize) / 2);
                        float bottom = imageSize + top;
                        SKRect drawBox = new SKRect(left, top, right, bottom);
                        sectionImageCanvas.DrawBitmap(badgeImage, drawBox, imagePaint);
                        drawPos.X += (int)Math.Floor(20 * renderOptions.EmoteScale);
                    }
                }
            }
            catch { }

            if (comment.message.user_badges != null)
            {
                foreach (var badge in comment.message.user_badges)
                {
                    string id = badge._id.ToString();
                    string version = badge.version.ToString();

                    SKBitmap badgeImage = null;
                    foreach (var cachedBadge in chatBadges)
                    {
                        if (cachedBadge.Name == id)
                        {
                            foreach (var cachedVersion in cachedBadge.Versions)
                            {
                                if (cachedVersion.Key == version)
                                {
                                    badgeImage = cachedVersion.Value;
                                }
                            }
                        }
                    }

                    if (badgeImage != null)
                    {
                        using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                        {
                            float imageRatio = (float)(renderOptions.EmoteScale * 0.5);
                            float imageSize = badgeImage.Width * imageRatio;
                            float left = (float)drawPos.X;
                            float right = imageSize + left;
                            float top = (float)((sectionImage.Height - imageSize) / 2);
                            float bottom = imageSize + top;
                            SKRect drawBox = new SKRect(left, top, right, bottom);
                            sectionImageCanvas.DrawBitmap(badgeImage, drawBox, imagePaint);
                            drawPos.X += (int)Math.Floor(20 * renderOptions.EmoteScale);
                        }
                    }
                }
            }
            return sectionImage;
        }
        public static SKBitmap DrawUsername(SKBitmap sectionImage, List<SKBitmap> imageList, ChatRenderOptions renderOptions, SKPaint nameFont, string userName, SKColor userColor, Size canvasSize, ref Point drawPos)
        {
            using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
            {
                SKPaint userPaint = nameFont;
                if (userName.Any(isNotAscii))
                {
                    userPaint = GetFallbackFont((int)userName.Where(x => isNotAscii(x)).First(), renderOptions);
                    userPaint.Color = userColor;
                }
                float textWidth = userPaint.MeasureText(userName + ":");
                float xPos = (float)drawPos.X;
                float yPos = (float)(((canvasSize.Height - renderOptions.FontSize) / 2) + renderOptions.FontSize) - (float)(renderOptions.EmoteScale * 2);
                if (renderOptions.Outline)
                {
                    SKPath outlinePath = userPaint.GetTextPath(userName + ":", xPos, yPos);
                    SKPaint outlinePaint = new SKPaint() { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(renderOptions.OutlineSize * renderOptions.EmoteScale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
                    sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
                }
                userPaint.Color = userColor;
                sectionImageCanvas.DrawText(userName + ":", xPos, yPos, userPaint);
                drawPos.X += (int)Math.Floor(textWidth) + (int)Math.Floor(6 * renderOptions.EmoteScale);
            }
            return sectionImage;
        }
        public static SKBitmap DrawMessage(SKBitmap sectionImage, List<SKBitmap> imageList, ChatRenderOptions renderOptions, List<GifEmote> currentGifEmotes, SKPaint messageFont, Dictionary<string, SKBitmap> emojiCache, List<TwitchEmote> chatEmotes, List<TwitchEmote> thirdPartyEmotes, List<CheerEmote> cheerEmotes, Comment comment, Size canvasSize, ref Point drawPos, ref int default_x, List<SKBitmap> emoteList, List<SKRect> emotePositionList)
        {
            string emojiRegex = @"[#*0-9]\uFE0F?\u20E3|©\uFE0F?|[®\u203C\u2049\u2122\u2139\u2194-\u2199\u21A9\u21AA]\uFE0F?|[\u231A\u231B]|[\u2328\u23CF]\uFE0F?|[\u23E9-\u23EC]|[\u23ED-\u23EF]\uFE0F?|\u23F0|[\u23F1\u23F2]\uFE0F?|\u23F3|[\u23F8-\u23FA\u24C2\u25AA\u25AB\u25B6\u25C0\u25FB\u25FC]\uFE0F?|[\u25FD\u25FE]|[\u2600-\u2604\u260E\u2611]\uFE0F?|[\u2614\u2615]|\u2618\uFE0F?|\u261D(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|[\u2620\u2622\u2623\u2626\u262A\u262E\u262F\u2638-\u263A\u2640\u2642]\uFE0F?|[\u2648-\u2653]|[\u265F\u2660\u2663\u2665\u2666\u2668\u267B\u267E]\uFE0F?|\u267F|\u2692\uFE0F?|\u2693|[\u2694-\u2697\u2699\u269B\u269C\u26A0]\uFE0F?|\u26A1|\u26A7\uFE0F?|[\u26AA\u26AB]|[\u26B0\u26B1]\uFE0F?|[\u26BD\u26BE\u26C4\u26C5]|\u26C8\uFE0F?|\u26CE|[\u26CF\u26D1\u26D3]\uFE0F?|\u26D4|\u26E9\uFE0F?|\u26EA|[\u26F0\u26F1]\uFE0F?|[\u26F2\u26F3]|\u26F4\uFE0F?|\u26F5|[\u26F7\u26F8]\uFE0F?|\u26F9(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?|\uFE0F(?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\u26FA\u26FD]|\u2702\uFE0F?|\u2705|[\u2708\u2709]\uFE0F?|[\u270A\u270B](?:\uD83C[\uDFFB-\uDFFF])?|[\u270C\u270D](?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|\u270F\uFE0F?|[\u2712\u2714\u2716\u271D\u2721]\uFE0F?|\u2728|[\u2733\u2734\u2744\u2747]\uFE0F?|[\u274C\u274E\u2753-\u2755\u2757]|\u2763\uFE0F?|\u2764(?:\u200D(?:\uD83D\uDD25|\uD83E\uDE79)|\uFE0F(?:\u200D(?:\uD83D\uDD25|\uD83E\uDE79))?)?|[\u2795-\u2797]|\u27A1\uFE0F?|[\u27B0\u27BF]|[\u2934\u2935\u2B05-\u2B07]\uFE0F?|[\u2B1B\u2B1C\u2B50\u2B55]|[\u3030\u303D\u3297\u3299]\uFE0F?|\uD83C(?:[\uDC04\uDCCF]|[\uDD70\uDD71\uDD7E\uDD7F]\uFE0F?|[\uDD8E\uDD91-\uDD9A]|\uDDE6\uD83C[\uDDE8-\uDDEC\uDDEE\uDDF1\uDDF2\uDDF4\uDDF6-\uDDFA\uDDFC\uDDFD\uDDFF]|\uDDE7\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEF\uDDF1-\uDDF4\uDDF6-\uDDF9\uDDFB\uDDFC\uDDFE\uDDFF]|\uDDE8\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDEE\uDDF0-\uDDF5\uDDF7\uDDFA-\uDDFF]|\uDDE9\uD83C[\uDDEA\uDDEC\uDDEF\uDDF0\uDDF2\uDDF4\uDDFF]|\uDDEA\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDED\uDDF7-\uDDFA]|\uDDEB\uD83C[\uDDEE-\uDDF0\uDDF2\uDDF4\uDDF7]|\uDDEC\uD83C[\uDDE6\uDDE7\uDDE9-\uDDEE\uDDF1-\uDDF3\uDDF5-\uDDFA\uDDFC\uDDFE]|\uDDED\uD83C[\uDDF0\uDDF2\uDDF3\uDDF7\uDDF9\uDDFA]|\uDDEE\uD83C[\uDDE8-\uDDEA\uDDF1-\uDDF4\uDDF6-\uDDF9]|\uDDEF\uD83C[\uDDEA\uDDF2\uDDF4\uDDF5]|\uDDF0\uD83C[\uDDEA\uDDEC-\uDDEE\uDDF2\uDDF3\uDDF5\uDDF7\uDDFC\uDDFE\uDDFF]|\uDDF1\uD83C[\uDDE6-\uDDE8\uDDEE\uDDF0\uDDF7-\uDDFB\uDDFE]|\uDDF2\uD83C[\uDDE6\uDDE8-\uDDED\uDDF0-\uDDFF]|\uDDF3\uD83C[\uDDE6\uDDE8\uDDEA-\uDDEC\uDDEE\uDDF1\uDDF4\uDDF5\uDDF7\uDDFA\uDDFF]|\uDDF4\uD83C\uDDF2|\uDDF5\uD83C[\uDDE6\uDDEA-\uDDED\uDDF0-\uDDF3\uDDF7-\uDDF9\uDDFC\uDDFE]|\uDDF6\uD83C\uDDE6|\uDDF7\uD83C[\uDDEA\uDDF4\uDDF8\uDDFA\uDDFC]|\uDDF8\uD83C[\uDDE6-\uDDEA\uDDEC-\uDDF4\uDDF7-\uDDF9\uDDFB\uDDFD-\uDDFF]|\uDDF9\uD83C[\uDDE6\uDDE8\uDDE9\uDDEB-\uDDED\uDDEF-\uDDF4\uDDF7\uDDF9\uDDFB\uDDFC\uDDFF]|\uDDFA\uD83C[\uDDE6\uDDEC\uDDF2\uDDF3\uDDF8\uDDFE\uDDFF]|\uDDFB\uD83C[\uDDE6\uDDE8\uDDEA\uDDEC\uDDEE\uDDF3\uDDFA]|\uDDFC\uD83C[\uDDEB\uDDF8]|\uDDFD\uD83C\uDDF0|\uDDFE\uD83C[\uDDEA\uDDF9]|\uDDFF\uD83C[\uDDE6\uDDF2\uDDFC]|\uDE01|\uDE02\uFE0F?|[\uDE1A\uDE2F\uDE32-\uDE36]|\uDE37\uFE0F?|[\uDE38-\uDE3A\uDE50\uDE51\uDF00-\uDF20]|[\uDF21\uDF24-\uDF2C]\uFE0F?|[\uDF2D-\uDF35]|\uDF36\uFE0F?|[\uDF37-\uDF7C]|\uDF7D\uFE0F?|[\uDF7E-\uDF84]|\uDF85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDF86-\uDF93]|[\uDF96\uDF97\uDF99-\uDF9B\uDF9E\uDF9F]\uFE0F?|[\uDFA0-\uDFC1]|\uDFC2(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC3\uDFC4](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDFC5\uDFC6]|\uDFC7(?:\uD83C[\uDFFB-\uDFFF])?|[\uDFC8\uDFC9]|\uDFCA(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDFCB\uDFCC](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?|\uFE0F(?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDFCD\uDFCE]\uFE0F?|[\uDFCF-\uDFD3]|[\uDFD4-\uDFDF]\uFE0F?|[\uDFE0-\uDFF0]|\uDFF3(?:\u200D(?:\u26A7\uFE0F?|\uD83C\uDF08)|\uFE0F(?:\u200D(?:\u26A7\uFE0F?|\uD83C\uDF08))?)?|\uDFF4(?:\u200D\u2620\uFE0F?|\uDB40\uDC67\uDB40\uDC62\uDB40(?:\uDC65\uDB40\uDC6E\uDB40\uDC67|\uDC73\uDB40\uDC63\uDB40\uDC74|\uDC77\uDB40\uDC6C\uDB40\uDC73)\uDB40\uDC7F)?|[\uDFF5\uDFF7]\uFE0F?|[\uDFF8-\uDFFF])|\uD83D(?:[\uDC00-\uDC07]|\uDC08(?:\u200D\u2B1B)?|[\uDC09-\uDC14]|\uDC15(?:\u200D\uD83E\uDDBA)?|[\uDC16-\uDC3A]|\uDC3B(?:\u200D\u2744\uFE0F?)?|[\uDC3C-\uDC3E]|\uDC3F\uFE0F?|\uDC40|\uDC41(?:\u200D\uD83D\uDDE8\uFE0F?|\uFE0F(?:\u200D\uD83D\uDDE8\uFE0F?)?)?|[\uDC42\uDC43](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC44\uDC45]|[\uDC46-\uDC50](?:\uD83C[\uDFFB-\uDFFF])?|[\uDC51-\uDC65]|[\uDC66\uDC67](?:\uD83C[\uDFFB-\uDFFF])?|\uDC68(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|[\uDC68\uDC69]\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFC-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFD-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFD\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?\uDC68\uD83C[\uDFFB-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D\uDC68\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC69(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:\uDC8B\u200D\uD83D)?[\uDC68\uDC69]|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?|\uDC69\u200D\uD83D(?:\uDC66(?:\u200D\uD83D\uDC66)?|\uDC67(?:\u200D\uD83D[\uDC66\uDC67])?)|[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92])|\uD83E[\uDDAF-\uDDB3\uDDBC\uDDBD])|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFC-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB\uDFFD-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFD\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D\uD83D(?:[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF]|\uDC8B\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFF])|\uD83C[\uDF3E\uDF73\uDF7C\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83D[\uDC68\uDC69]\uD83C[\uDFFB-\uDFFE]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|\uDC6A|[\uDC6B-\uDC6D](?:\uD83C[\uDFFB-\uDFFF])?|\uDC6E(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC6F(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDC70\uDC71](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC72(?:\uD83C[\uDFFB-\uDFFF])?|\uDC73(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDC74-\uDC76](?:\uD83C[\uDFFB-\uDFFF])?|\uDC77(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC78(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC79-\uDC7B]|\uDC7C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC7D-\uDC80]|[\uDC81\uDC82](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDC83(?:\uD83C[\uDFFB-\uDFFF])?|\uDC84|\uDC85(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC86\uDC87](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDC88-\uDC8E]|\uDC8F(?:\uD83C[\uDFFB-\uDFFF])?|\uDC90|\uDC91(?:\uD83C[\uDFFB-\uDFFF])?|[\uDC92-\uDCA9]|\uDCAA(?:\uD83C[\uDFFB-\uDFFF])?|[\uDCAB-\uDCFC]|\uDCFD\uFE0F?|[\uDCFF-\uDD3D]|[\uDD49\uDD4A]\uFE0F?|[\uDD4B-\uDD4E\uDD50-\uDD67]|[\uDD6F\uDD70\uDD73]\uFE0F?|\uDD74(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|\uDD75(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?|\uFE0F(?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDD76-\uDD79]\uFE0F?|\uDD7A(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD87\uDD8A-\uDD8D]\uFE0F?|\uDD90(?:\uD83C[\uDFFB-\uDFFF]|\uFE0F)?|[\uDD95\uDD96](?:\uD83C[\uDFFB-\uDFFF])?|\uDDA4|[\uDDA5\uDDA8\uDDB1\uDDB2\uDDBC\uDDC2-\uDDC4\uDDD1-\uDDD3\uDDDC-\uDDDE\uDDE1\uDDE3\uDDE8\uDDEF\uDDF3\uDDFA]\uFE0F?|[\uDDFB-\uDE2D]|\uDE2E(?:\u200D\uD83D\uDCA8)?|[\uDE2F-\uDE34]|\uDE35(?:\u200D\uD83D\uDCAB)?|\uDE36(?:\u200D\uD83C\uDF2B\uFE0F?)?|[\uDE37-\uDE44]|[\uDE45-\uDE47](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDE48-\uDE4A]|\uDE4B(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDE4C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE4D\uDE4E](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDE4F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDE80-\uDEA2]|\uDEA3(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDEA4-\uDEB3]|[\uDEB4-\uDEB6](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDEB7-\uDEBF]|\uDEC0(?:\uD83C[\uDFFB-\uDFFF])?|[\uDEC1-\uDEC5]|\uDECB\uFE0F?|\uDECC(?:\uD83C[\uDFFB-\uDFFF])?|[\uDECD-\uDECF]\uFE0F?|[\uDED0-\uDED2\uDED5-\uDED7]|[\uDEE0-\uDEE5\uDEE9]\uFE0F?|[\uDEEB\uDEEC]|[\uDEF0\uDEF3]\uFE0F?|[\uDEF4-\uDEFC\uDFE0-\uDFEB])|\uD83E(?:\uDD0C(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD0D\uDD0E]|\uDD0F(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD10-\uDD17]|[\uDD18-\uDD1C](?:\uD83C[\uDFFB-\uDFFF])?|\uDD1D|[\uDD1E\uDD1F](?:\uD83C[\uDFFB-\uDFFF])?|[\uDD20-\uDD25]|\uDD26(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDD27-\uDD2F]|[\uDD30-\uDD34](?:\uD83C[\uDFFB-\uDFFF])?|\uDD35(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDD36(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD37-\uDD39](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDD3A|\uDD3C(?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDD3D\uDD3E](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDD3F-\uDD45\uDD47-\uDD76]|\uDD77(?:\uD83C[\uDFFB-\uDFFF])?|[\uDD78\uDD7A-\uDDB4]|[\uDDB5\uDDB6](?:\uD83C[\uDFFB-\uDFFF])?|\uDDB7|[\uDDB8\uDDB9](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDDBA|\uDDBB(?:\uD83C[\uDFFB-\uDFFF])?|[\uDDBC-\uDDCB]|[\uDDCD-\uDDCF](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDDD0|\uDDD1(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1|[\uDDAF-\uDDB3\uDDBC\uDDBD]))|\uD83C(?:\uDFFB(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFC-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFC(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB\uDFFD-\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFD(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB\uDFFC\uDFFE\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFE(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB-\uDFFD\uDFFF]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?|\uDFFF(?:\u200D(?:[\u2695\u2696\u2708]\uFE0F?|\u2764\uFE0F?\u200D(?:\uD83D\uDC8B\u200D)?\uD83E\uDDD1\uD83C[\uDFFB-\uDFFE]|\uD83C[\uDF3E\uDF73\uDF7C\uDF84\uDF93\uDFA4\uDFA8\uDFEB\uDFED]|\uD83D[\uDCBB\uDCBC\uDD27\uDD2C\uDE80\uDE92]|\uD83E(?:\uDD1D\u200D\uD83E\uDDD1\uD83C[\uDFFB-\uDFFF]|[\uDDAF-\uDDB3\uDDBC\uDDBD])))?))?|[\uDDD2\uDDD3](?:\uD83C[\uDFFB-\uDFFF])?|\uDDD4(?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|\uDDD5(?:\uD83C[\uDFFB-\uDFFF])?|[\uDDD6-\uDDDD](?:\u200D[\u2640\u2642]\uFE0F?|\uD83C[\uDFFB-\uDFFF](?:\u200D[\u2640\u2642]\uFE0F?)?)?|[\uDDDE\uDDDF](?:\u200D[\u2640\u2642]\uFE0F?)?|[\uDDE0-\uDDFF\uDE70-\uDE74\uDE78-\uDE7A\uDE80-\uDE86\uDE90-\uDEA8\uDEB0-\uDEB6\uDEC0-\uDEC2\uDED0-\uDED6])";
            int bitsCount = comment.message.bits_spent;
            foreach (var fragment in comment.message.fragments)
            {
                if (fragment.emoticon == null)
                {
                    string[] fragmentParts = SwapRTL(fragment.text.Split(' '));
                    for (int i = 0; i < fragmentParts.Length; i++)
                    {
                        string output = fragmentParts[i].Trim();
                        bool isThirdPartyEmote = false;
                        TwitchEmote currentEmote = null;

                        if (output == "" || output == "󠀀")
                            continue;

                        foreach (var thirdPartyEmote in thirdPartyEmotes)
                        {
                            if (fragmentParts[i] == thirdPartyEmote.name)
                            {
                                isThirdPartyEmote = true;
                                currentEmote = thirdPartyEmote;
                            }
                        }

                        if (isThirdPartyEmote)
                        {
                            if (drawPos.X + (currentEmote.width / currentEmote.imageScale) * renderOptions.EmoteScale > canvasSize.Width)
                                sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

                            if (currentEmote.imageType == "gif" || currentEmote.imageType == "webp")
                            {
                                GifEmote emote = new GifEmote(new Point(drawPos.X, drawPos.Y), currentEmote.name, currentEmote.codec, currentEmote.imageScale, currentEmote.emote_frames);
                                currentGifEmotes.Add(emote);
                                drawPos.X += (int)Math.Floor((currentEmote.width / currentEmote.imageScale) * renderOptions.EmoteScale + (3 * renderOptions.EmoteScale));
                            }
                            else
                            {
                                using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                                {
                                    float imageRatio = (float)renderOptions.EmoteScale / currentEmote.imageScale;
                                    float imageWidth = currentEmote.width * imageRatio;
                                    float imageHeight = currentEmote.height * imageRatio;
                                    float left = (float)drawPos.X;
                                    float right = imageWidth + left;
                                    float top = (float)((sectionImage.Height - imageHeight) / 2);
                                    float bottom = imageHeight + top;
                                    emoteList.Add(currentEmote.emote_frames.First());
                                    emotePositionList.Add(new SKRect(left, top + (renderOptions.SectionHeight * imageList.Count), right, bottom + (renderOptions.SectionHeight * imageList.Count)));
                                    drawPos.X += (int)Math.Ceiling(imageWidth + (3 * renderOptions.EmoteScale));
                                }
                            }
                        }
                        else
                        {
                            if (Regex.Match(output, emojiRegex).Success)
                            {
                                MatchCollection matches = Regex.Matches(output, emojiRegex);
                                List<char> charList = new List<char>();
                                List<string> codepointList = new List<string>();
                                for (int j = 0; j < output.Length; j += char.IsHighSurrogate(output[j]) ? 2 : 1)
                                {
                                    Match match = null;
                                    foreach (Match currentMatch in matches)
                                    {
                                        if (j >= currentMatch.Index && j <= currentMatch.Index + currentMatch.Length - 1)
                                        {
                                            match = currentMatch;
                                            break;
                                        }
                                    }
                                    bool isEmoji = match != null;
                                    if (isEmoji)
                                    {
                                        if (charList.Count > 0)
                                        {
                                            string text = new string(charList.ToArray());
                                            if (text != "\u200D")
                                                sectionImage = DrawText(sectionImage, text, messageFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, false, default_x);
                                            charList.Clear();
                                        }

                                        string codepoint = "";
                                        if (char.IsHighSurrogate(output[j]) && j < output.Length - 1)
                                        {
                                            codepoint = String.Format("{0:X4}", char.ConvertToUtf32(output[j], output[j + 1])).ToLower();
                                        }
                                        else
                                        {
                                            codepoint = String.Format("{0:X4}", (int)output[j]).ToLower();
                                        }
                                        codepoint = codepoint.Replace("fe0f", "");
                                        string codepointSequence = String.Join("-", codepointList.ToArray());
                                        string newCodepoint = codepointList.Count > 0 ? (codepointSequence + "-" + codepoint).Trim() : codepoint;
                                        if (emojiCache.ContainsKey(newCodepoint))
                                        {
                                            codepointList.Add(codepoint);
                                        }
                                        else
                                        {
                                            if (emojiCache.ContainsKey(codepointSequence))
                                            {
                                                SKBitmap emojiBitmap = emojiCache[codepointSequence];
                                                float emojiSize = (emojiBitmap.Width / 4) * (float)renderOptions.EmoteScale;
                                                if (drawPos.X + (20 * renderOptions.EmoteScale) + 3 > canvasSize.Width)
                                                    sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

                                                using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                                                {
                                                    float emojiLeft = (float)drawPos.X;
                                                    float emojiTop = (float)Math.Floor((renderOptions.SectionHeight - emojiSize) / 2.0);
                                                    SKRect emojiRect = new SKRect(emojiLeft, emojiTop, emojiLeft + emojiSize, emojiTop + emojiSize);
                                                    sectionImageCanvas.DrawBitmap(emojiBitmap, emojiRect, imagePaint);
                                                    drawPos.X += (int)Math.Floor(emojiSize + (int)Math.Floor(3 * renderOptions.EmoteScale));
                                                }
                                            }
                                            codepointList.Clear();
                                            codepointList.Add(codepoint);
                                        }
                                    }
                                    else
                                    {
                                        if (codepointList.Count > 0)
                                        {
                                            string codepointSequence = String.Join("-", codepointList.ToArray());
                                            if (emojiCache.ContainsKey(codepointSequence))
                                            {
                                                SKBitmap emojiBitmap = emojiCache[codepointSequence];
                                                float emojiSize = (emojiBitmap.Width / 4) * (float)renderOptions.EmoteScale;
                                                if (drawPos.X + (20 * renderOptions.EmoteScale) + 3 > canvasSize.Width)
                                                    sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

                                                using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                                                {
                                                    float emojiLeft = (float)drawPos.X;
                                                    float emojiTop = (float)Math.Floor((renderOptions.SectionHeight - emojiSize) / 2.0);
                                                    SKRect emojiRect = new SKRect(emojiLeft, emojiTop, emojiLeft + emojiSize, emojiTop + emojiSize);
                                                    sectionImageCanvas.DrawBitmap(emojiBitmap, emojiRect, imagePaint);
                                                    drawPos.X += (int)Math.Floor(emojiSize + (int)Math.Floor(3 * renderOptions.EmoteScale));
                                                }
                                            }
                                            codepointList.Clear();
                                        }
                                        charList.Add(output[j]);
                                    }
                                }
                                if (charList.Count > 0)
                                {
                                    string text = new string(charList.ToArray());
                                    if (text != "\u200D")
                                        sectionImage = DrawText(sectionImage, text, messageFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, false, default_x);
                                    charList.Clear();
                                }
                                if (codepointList.Count > 0)
                                {
                                    string codepointSequence = String.Join("-", codepointList.ToArray());
                                    if (emojiCache.ContainsKey(codepointSequence))
                                    {
                                        SKBitmap emojiBitmap = emojiCache[codepointSequence];
                                        float emojiSize = (emojiBitmap.Width / 4) * (float)renderOptions.EmoteScale;
                                        if (drawPos.X + (20 * renderOptions.EmoteScale) + 3 > canvasSize.Width)
                                            sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

                                        using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                                        {
                                            float emojiLeft = (float)drawPos.X;
                                            float emojiTop = (float)Math.Floor((renderOptions.SectionHeight - emojiSize) / 2.0);
                                            SKRect emojiRect = new SKRect(emojiLeft, emojiTop, emojiLeft + emojiSize, emojiTop + emojiSize);
                                            sectionImageCanvas.DrawBitmap(emojiBitmap, emojiRect, imagePaint);
                                            drawPos.X += (int)Math.Floor(emojiSize + (int)Math.Floor(3 * renderOptions.EmoteScale));
                                        }
                                    }
                                    codepointList.Clear();
                                }
                            }
                            else if (new StringInfo(output).LengthInTextElements < output.Length || !messageFont.ContainsGlyphs(output))
                            {
                                SKPaint renderFont = messageFont;
                                List<char> charList = new List<char>(output.ToArray());
                                string messageBuffer = "";
                                //Very rough estimation of width of text, because we don't know the font yet. This is to show ASCII spam properly
                                int textWidth = (int)Math.Floor(charList.Count * (9.0 * renderOptions.EmoteScale));
                                if (drawPos.X + textWidth > canvasSize.Width)
                                    sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

                                for (int j = 0; j < charList.Count; j++)
                                {
                                    if (char.IsHighSurrogate(charList[j]) && j+1 < charList.Count && char.IsLowSurrogate(charList[j+1]))
                                    {
                                        if (messageBuffer != "")
                                            sectionImage = DrawText(sectionImage, messageBuffer, messageFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
                                        SKPaint fallbackFont = GetFallbackFont(char.ConvertToUtf32(charList[j], charList[j+1]), renderOptions);
                                        fallbackFont.Color = renderOptions.MessageColor;
                                        sectionImage = DrawText(sectionImage, charList[j].ToString() + charList[j+1].ToString(), fallbackFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, false, default_x);
                                        messageBuffer = "";
                                        j++;
                                    }
                                    else if (new StringInfo(charList[j].ToString()).LengthInTextElements == 0 || !renderFont.ContainsGlyphs(charList[j].ToString()))
                                    {
                                        if (messageBuffer != "")
                                            sectionImage = DrawText(sectionImage, messageBuffer, messageFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
                                        SKPaint fallbackFont = GetFallbackFont(charList[j], renderOptions);
                                        fallbackFont.Color = renderOptions.MessageColor;
                                        sectionImage = DrawText(sectionImage, charList[j].ToString(), fallbackFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, false, default_x);
                                        messageBuffer = "";
                                    }
                                    else
                                    {
                                        messageBuffer += charList[j];
                                    }
                                }
                            }
                            else
                            {
                                bool bitsPrinted = false;
                                try
                                {
                                    if (bitsCount > 0 && output.Any(char.IsDigit) && output.Any(char.IsLetter))
                                    {
                                        int bitsIndex = output.IndexOfAny("0123456789".ToCharArray());
                                        string outputPrefix = output.Substring(0, bitsIndex).ToLower();
                                        if (cheerEmotes.Any(x => x.prefix.ToLower() == outputPrefix))
                                        {
                                            CheerEmote currentCheerEmote = cheerEmotes.Find(x => x.prefix.ToLower() == outputPrefix);
                                            int bitsAmount = Int32.Parse(output.Substring(bitsIndex));
                                            bitsCount -= bitsAmount;
                                            KeyValuePair<int, TwitchEmote> tierList = currentCheerEmote.getTier(bitsAmount);
                                            GifEmote emote = new GifEmote(new Point(drawPos.X, drawPos.Y), tierList.Value.name, tierList.Value.codec, tierList.Value.imageScale, tierList.Value.emote_frames);
                                            currentGifEmotes.Add(emote);
                                            drawPos.X += (int)((tierList.Value.width / tierList.Value.imageScale) * renderOptions.EmoteScale + (3 * renderOptions.EmoteScale));
                                            sectionImage = DrawText(sectionImage, bitsAmount.ToString(), messageFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
                                            bitsPrinted = true;
                                        }
                                    }
                                }
                                catch
                                { }
                                if (!bitsPrinted)
                                    sectionImage = DrawText(sectionImage, output, messageFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
                            }
                        }
                    }
                }
                else
                {
                    //Is a first party emote
                    string emoteId = fragment.emoticon.emoticon_id;
                    if (chatEmotes.Any(x => x.id == emoteId))
                    {
                        TwitchEmote currentEmote = chatEmotes.Where(x => x.id == emoteId).First();
                        if (currentEmote.imageType == "png")
                        {
                            SKBitmap emoteImage = currentEmote.emote_frames[0];
                            float imageWidth = emoteImage.Width * (float)renderOptions.EmoteScale;
                            if (drawPos.X + imageWidth > canvasSize.Width)
                                sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);
                            using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
                            {
                                float imageHeight = emoteImage.Height * (float)renderOptions.EmoteScale;
                                float left = (float)drawPos.X;
                                float right = imageWidth + left;
                                float top = (float)((sectionImage.Height - imageHeight) / 2);
                                float bottom = imageHeight + top;
                                //SKRect emoteRect = new SKRect(imageLeft, imageTop, imageLeft + imageWidth, imageTop + imageHeight);
                                //sectionImageCanvas.DrawBitmap(emoteImage, emoteRect, imagePaint);
                                emoteList.Add(emoteImage);
                                emotePositionList.Add(new SKRect(left, top + (renderOptions.SectionHeight * imageList.Count), right, bottom + (renderOptions.SectionHeight * imageList.Count)));
                            }
                            drawPos.X += (int)Math.Ceiling(imageWidth + (3 * renderOptions.EmoteScale));
                        }
                        else
                        {
                            GifEmote emote = new GifEmote(new Point(drawPos.X, drawPos.Y), currentEmote.name, currentEmote.codec, currentEmote.imageScale, currentEmote.emote_frames);
                            currentGifEmotes.Add(emote);
                            drawPos.X += (int)Math.Floor((currentEmote.width / currentEmote.imageScale) * renderOptions.EmoteScale + (3 * renderOptions.EmoteScale));
                        }
                    }
                    else
                    {
                        //Probably an old emote that was removed
                        sectionImage = DrawText(sectionImage, fragment.text, messageFont, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, true, default_x);
                    }
                }
            }

            imageList.Add(sectionImage);

            SKBitmap paddingImage = new SKBitmap((int)canvasSize.Width, (int)Math.Floor(.4 * renderOptions.SectionHeight));
            using (SKCanvas paddingImageCanvas = new SKCanvas(paddingImage))
                paddingImageCanvas.Clear(renderOptions.BackgroundColor);
            imageList.Add(paddingImage);
            return sectionImage;
        }
        public static SKBitmap DrawText(SKBitmap sectionImage, string message, SKPaint messageFont, List<SKBitmap> imageList, ChatRenderOptions renderOptions, List<GifEmote> currentGifEmotes, Size canvasSize, ref Point drawPos, bool padding, int default_x)
        {
            float textWidth;
            bool isRtl = isRTL(message);
            try
            {
                if (isRtl)
                {
                    SKShaper messageShape = new SKShaper(messageFont.Typeface);
                    SKShaper.Result measure = messageShape.Shape(message, messageFont);
                    textWidth = measure.Points[measure.Points.Length - 1].X;
                }
                else
                {
                    textWidth = messageFont.MeasureText(message);
                }
            }
            catch { return sectionImage; }
            if (drawPos.X + textWidth + 3 > canvasSize.Width)
                sectionImage = AddImageSection(sectionImage, imageList, renderOptions, currentGifEmotes, canvasSize, ref drawPos, default_x);

            float xPos = (float)drawPos.X;
            float yPos = (float)(((canvasSize.Height - renderOptions.FontSize) / 2) + renderOptions.FontSize) - (float)(renderOptions.EmoteScale * 2);
            using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImage))
            {
                if (renderOptions.Outline)
                {
                    SKPath outlinePath = messageFont.GetTextPath(message, xPos, yPos);
                    SKPaint outlinePaint = new SKPaint() { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(renderOptions.OutlineSize * renderOptions.EmoteScale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
                    sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
                }

                try
                {
                    sectionImageCanvas.DrawShapedText(message, xPos, yPos, messageFont);
                }
                catch
                {
                    sectionImageCanvas.DrawText(message, xPos, yPos, messageFont);
                }
            }
            if (!isRtl)
            {
                drawPos.X += (int)Math.Floor(textWidth + (padding ? (int)Math.Floor(4 * renderOptions.EmoteScale) : 0));
            }
            else
            {
                drawPos.X += (int)Math.Floor(textWidth + (padding ? (int)Math.Floor(8 * renderOptions.EmoteScale) : 0));
            }
            
            return sectionImage;
        }
        private static SKBitmap AddImageSection(SKBitmap sectionImage, List<SKBitmap> imageList, ChatRenderOptions renderOptions, List<GifEmote> currentGifEmotes, Size canvasSize, ref Point drawPos, int default_x)
        {
            imageList.Add(sectionImage);
            SKBitmap newImage = new SKBitmap((int)canvasSize.Width, (int)canvasSize.Height);
            using (SKCanvas paddingImageCanvas = new SKCanvas(newImage))
                paddingImageCanvas.Clear(renderOptions.BackgroundColor);
            drawPos.X = default_x;
            drawPos.Y += sectionImage.Height;
            return newImage;
        }
        private SKColor GenerateUserColor(SKColor userColor, SKColor background_color, ChatRenderOptions renderOptions)
        {
            float backgroundHue, backgroundSaturation, backgroundBrightness;
            background_color.ToHsl(out backgroundHue, out backgroundSaturation, out backgroundBrightness);
            float userHue, userSaturation, userBrightness;
            userColor.ToHsl(out userHue, out userSaturation, out userBrightness);

            if (backgroundBrightness < 25 || renderOptions.Outline)
            {
                //Dark background or black outline
                if (userBrightness < 45)
                    userBrightness = 45;
                if (userSaturation > 80)
                    userSaturation = 80;
                SKColor newColor = SKColor.FromHsl(userHue, userSaturation, userBrightness);
                return newColor;
            }

            if (Math.Abs(backgroundBrightness - userBrightness) < 10 && backgroundBrightness > 50)
            {
                userBrightness -= 20;
                SKColor newColor = SKColor.FromHsl(userHue, userSaturation, userBrightness);
                return newColor;
            }

            return userColor;
        }
        public static SKPaint GetFallbackFont(int input, ChatRenderOptions renderOptions)
        {
            if (fallbackCache.ContainsKey(input))
                return fallbackCache[input];

            SKPaint newPaint = new SKPaint() { Typeface = fontManager.MatchCharacter(input), LcdRenderText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            fallbackCache.TryAdd(input, newPaint);
            return newPaint;
        }
        private static bool isNotAscii(char input)
        {
            return input > 127;
        }
        /*
         *  Swaps the order of groups of RTL words, preserves order of LTR words
         *  ex:
         *  Arabic1 Arabic2 Engligh1 English2 Ararbic3 Arabic4 -> Arabic2 Arabic1 English1 English2 Arabic4 Arabic3
         */
        static string[] SwapRTL(string[] words)
        {
            List<string> finalWords = new List<string>();
            Stack<string> rtlStack = new Stack<string>();
            foreach (var word in words)
            {
                if (isRTL(word))
                {
                    rtlStack.Push(word);
                }
                else
                {
                    while (rtlStack.Count > 0)
                        finalWords.Add(rtlStack.Pop());
                    finalWords.Add(word);
                }
            }
            while (rtlStack.Count > 0)
                finalWords.Add(rtlStack.Pop());
            return finalWords.ToArray();
        }
        static bool isRTL(string message)
        {
            if (message.Length > 0)
            {
                if (message[0] >= '\u0591' && message[0] <= '\u07FF')
                    return true;
                else
                    return false;
            }
            else
            {
                return false;
            }
        }
        public ChatRoot ParseJson()
        {
            ChatRoot chat = new ChatRoot();

            using (FileStream fs = new FileStream(renderOptions.InputFile, FileMode.Open, FileAccess.Read))
            {
                using (JsonDocument jsonDocument = JsonDocument.Parse(fs))
                {
                    if (jsonDocument.RootElement.TryGetProperty("streamer", out JsonElement streamerJson))
                    {
                        chat.streamer = streamerJson.Deserialize<Streamer>();
                    }
                    if (jsonDocument.RootElement.TryGetProperty("video", out JsonElement videoJson))
                    {
                        if (videoJson.TryGetProperty("start", out JsonElement videoStartJson) && videoJson.TryGetProperty("end", out JsonElement videoEndJson))
                        {
                            chat.video = videoJson.Deserialize<VideoTime>();
                        }
                    }
                    if (jsonDocument.RootElement.TryGetProperty("emotes", out JsonElement emotesJson))
                    {
                        chat.emotes = emotesJson.Deserialize<Emotes>();
                    }
                    if (jsonDocument.RootElement.TryGetProperty("comments", out JsonElement commentsJson))
                    {
                        chat.comments = commentsJson.Deserialize<List<Comment>>();
                    }
                }
            }

            if (chat.streamer == null)
            {
                chat.streamer = new Streamer();
                chat.streamer.id = int.Parse(chat.comments.First().channel_id);
                chat.streamer.name = TwitchHelper.GetStreamerName(chat.streamer.id);
            }

            return chat;
        }
    }
}
