using NeoSmart.Unicode;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public sealed class ChatRenderer
    {
        static readonly string[] defaultColors = new string[] { "#FF0000", "#0000FF", "#00FF00", "#B22222", "#FF7F50", "#9ACD32", "#FF4500", "#2E8B57", "#DAA520", "#D2691E", "#5F9EA0", "#1E90FF", "#FF69B4", "#8A2BE2", "#00FF7F" };
        public ChatRoot chatRoot { get; set; } = new ChatRoot();

        private readonly ChatRenderOptions renderOptions;
        private List<ChatBadge> badgeList = new List<ChatBadge>();
        private List<TwitchEmote> emoteList = new List<TwitchEmote>();
        private List<TwitchEmote> emoteThirdList = new List<TwitchEmote>();
        private List<CheerEmote> cheermotesList = new List<CheerEmote>();
        private Dictionary<string, SKBitmap> emojiCache = new Dictionary<string, SKBitmap>();
        private ConcurrentDictionary<int, SKPaint> fallbackCache = new ConcurrentDictionary<int, SKPaint>();
        private SKFontManager fontManager = SKFontManager.CreateDefault();
        private SKPaint messageFont = new SKPaint();
        private SKPaint nameFont = new SKPaint();
        private SKPaint outlinePaint = new SKPaint();

        public ChatRenderer(ChatRenderOptions chatRenderOptions)
        {
            renderOptions = chatRenderOptions;
            renderOptions.TempFolder = Path.Combine(
                string.IsNullOrWhiteSpace(renderOptions.TempFolder) ? Path.GetTempPath() : renderOptions.TempFolder,
                "TwitchDownloader");
            renderOptions.BlockArtPreWrapWidth = 29.166 * renderOptions.FontSize - renderOptions.SidePadding * 2;
            renderOptions.BlockArtPreWrap = renderOptions.ChatWidth > renderOptions.BlockArtPreWrapWidth;
        }

        public async Task RenderVideoAsync(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
        {
            progress.Report(new ProgressReport(ReportType.Status, "Fetching Images"));
            await Task.Run(() => FetchImages(cancellationToken), cancellationToken);

            await Task.Run(ScaleImages, cancellationToken);
            FloorCommentOffsets(chatRoot.comments);

            outlinePaint = new SKPaint() { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(renderOptions.OutlineSize * renderOptions.ReferenceScale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, IsAutohinted = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            nameFont = new SKPaint() { LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            messageFont = new SKPaint() { LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High, Color = renderOptions.MessageColor };

            if (renderOptions.Font == "Inter Embedded")
            {
                nameFont.Typeface = GetInterTypeface(renderOptions.UsernameFontStyle);
                messageFont.Typeface = GetInterTypeface(renderOptions.MessageFontStyle);
            }
            else
            {
                nameFont.Typeface = SKTypeface.FromFamilyName(renderOptions.Font, renderOptions.UsernameFontStyle);
                messageFont.Typeface = SKTypeface.FromFamilyName(renderOptions.Font, renderOptions.MessageFontStyle);
            }

            // Rough estimation of the width of a single block art character
            renderOptions.BlockArtCharWidth = GetFallbackFont('█', renderOptions).MeasureText("█");

            (int startTick, int totalTicks) = GetVideoTicks();

            if (File.Exists(renderOptions.OutputFile))
                File.Delete(renderOptions.OutputFile);

            if (renderOptions.GenerateMask && File.Exists(renderOptions.MaskFile))
                File.Delete(renderOptions.MaskFile);

            FfmpegProcess ffmpegProcess = GetFfmpegProcess(0, false, progress);
            FfmpegProcess maskProcess = renderOptions.GenerateMask ? GetFfmpegProcess(0, true) : null;
            progress.Report(new ProgressReport(ReportType.StatusInfo, "Rendering Video: 0%"));

            try
            {
                await Task.Run(() => RenderVideoSection(startTick, startTick + totalTicks, ffmpegProcess, maskProcess, progress, cancellationToken), cancellationToken);
            }
            catch
            {
                ffmpegProcess.Process.Dispose();
                maskProcess?.Process.Dispose();
                GC.Collect();
                throw;
            }
        }

        /* Why are we doing this? The question is when to display a 0.5 second offset comment with an update rate of 1.
         * At the update frame at 0 seconds, or 1 second? We're choosing at 0 seconds here. Flooring to either the 
         * update rate, or if the update rate is greater than 1 just to the next whole number */
        private void FloorCommentOffsets(List<Comment> comments)
        {
            if (renderOptions.UpdateRate <= 0)
                return;

            foreach (var comment in comments)
            {
                if (renderOptions.UpdateRate > 1)
                {
                    comment.content_offset_seconds = Math.Floor(comment.content_offset_seconds);
                }
                else
                {
                    comment.content_offset_seconds = Math.Floor(comment.content_offset_seconds / renderOptions.UpdateRate) * renderOptions.UpdateRate;
                }
            }
        }

        private static SKTypeface GetInterTypeface(SKFontStyle fontStyle)
        {
            if (fontStyle == SKFontStyle.Bold)
            {
                using MemoryStream stream = new MemoryStream(Properties.Resources.InterBold);
                return SKTypeface.FromStream(stream);
            }
            else
            {
                using MemoryStream stream = new MemoryStream(Properties.Resources.Inter);
                return SKTypeface.FromStream(stream);
            }
        }

        private void RenderVideoSection(int startTick, int endTick, FfmpegProcess ffmpegProcess, FfmpegProcess maskProcess = null, IProgress<ProgressReport> progress = null, CancellationToken cancellationToken = new())
        {
            UpdateFrame latestUpdate = null;
            BinaryWriter ffmpegStream = new BinaryWriter(ffmpegProcess.Process.StandardInput.BaseStream);
            BinaryWriter maskStream = null;
            if (maskProcess != null)
                maskStream = new BinaryWriter(maskProcess.Process.StandardInput.BaseStream);

            DriveInfo outputDrive = DriveHelper.GetOutputDrive(ffmpegProcess);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // Measure some sample text to determine the text height, cannot assume it is font size
            SKRect sampleTextBounds = new SKRect();
            messageFont.MeasureText("abc123", ref sampleTextBounds);

            for (int currentTick = startTick; currentTick < endTick; currentTick++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (currentTick % renderOptions.UpdateFrame == 0)
                {
                    latestUpdate = GenerateUpdateFrame(currentTick, sampleTextBounds.Height, progress, latestUpdate);
                }

                using (SKBitmap frame = GetFrameFromTick(currentTick, sampleTextBounds.Height, progress, latestUpdate))
                {
                    DriveHelper.WaitForDrive(outputDrive, progress, cancellationToken).Wait(cancellationToken);

                    ffmpegStream.Write(frame.Bytes);

                    if (maskProcess != null)
                    {
                        DriveHelper.WaitForDrive(outputDrive, progress, cancellationToken).Wait(cancellationToken);

                        SetFrameMask(frame);
                        maskStream.Write(frame.Bytes);
                    }
                }

                if (progress != null)
                {
                    double percentDouble = (currentTick - startTick) / (double)(endTick - startTick) * 100.0;
                    int percentInt = (int)percentDouble;
                    progress.Report(new ProgressReport(percentInt));

                    int timeLeftInt = (int)(100.0 / percentDouble * stopwatch.Elapsed.TotalSeconds) - (int)stopwatch.Elapsed.TotalSeconds;
                    TimeSpan timeLeft = new TimeSpan(0, 0, timeLeftInt);
                    TimeSpan timeElapsed = new TimeSpan(0, 0, (int)stopwatch.Elapsed.TotalSeconds);
                    progress.Report(new ProgressReport(ReportType.StatusInfo, $"Rendering Video: {percentInt}% ({timeElapsed.ToString(@"h\hm\ms\s")} Elapsed | {timeLeft.ToString(@"h\hm\ms\s")} Remaining)"));
                }
            }

            stopwatch.Stop();
            progress?.Report(new ProgressReport(ReportType.StatusInfo, "Rendering Video: 100%"));
            progress?.Report(new ProgressReport(ReportType.Log, $"FINISHED. RENDER TIME: {(int)stopwatch.Elapsed.TotalSeconds}s SPEED: {((endTick - startTick) / renderOptions.Framerate / stopwatch.Elapsed.TotalSeconds).ToString("0.##")}x"));

            latestUpdate?.Image.Dispose();

            ffmpegStream.Dispose();
            maskStream?.Dispose();

            ffmpegProcess.Process.WaitForExit(100_000);
            maskProcess?.Process.WaitForExit(100_000);
        }

        private void SetFrameMask(SKBitmap frame)
        {
            IntPtr pixelsAddr = frame.GetPixels();
            int height = frame.Height;
            int width = frame.Width;
            unsafe
            {
                byte* ptr = (byte*)pixelsAddr.ToPointer();
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        byte alpha = *(ptr + 3); // alpha of the unmasked pixel
                        *ptr++ = alpha;
                        *ptr++ = alpha;
                        *ptr++ = alpha;
                        *ptr++ = 0xFF;
                    }
                }
            }
        }

        private FfmpegProcess GetFfmpegProcess(int partNumber, bool isMask, IProgress<ProgressReport> progress = null)
        {
            string savePath;
            if (partNumber == 0)
            {
                if (isMask)
                    savePath = renderOptions.MaskFile;
                else
                    savePath = renderOptions.OutputFile;
            }
            else
            {
                savePath = Path.Combine(renderOptions.TempFolder, Path.GetRandomFileName() + (isMask ? "_mask" : "") + Path.GetExtension(renderOptions.OutputFile));
            }

            string inputArgs = renderOptions.InputArgs.Replace("{fps}", renderOptions.Framerate.ToString())
                .Replace("{height}", renderOptions.ChatHeight.ToString()).Replace("{width}", renderOptions.ChatWidth.ToString())
                .Replace("{save_path}", savePath).Replace("{max_int}", int.MaxValue.ToString())
                .Replace("{pix_fmt}", new SKBitmap().ColorType == SKColorType.Rgba8888 ? "rgba" : "bgra");
            string outputArgs = renderOptions.OutputArgs.Replace("{fps}", renderOptions.Framerate.ToString())
                .Replace("{height}", renderOptions.ChatHeight.ToString()).Replace("{width}", renderOptions.ChatWidth.ToString())
                .Replace("{save_path}", savePath).Replace("{max_int}", int.MaxValue.ToString());

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
                    RedirectStandardError = true,
                }
            };

            if (renderOptions.LogFfmpegOutput && progress != null)
            {
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        progress.Report(new ProgressReport() { ReportType = ReportType.FfmpegLog, Data = e.Data });
                    }
                };
            }

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            return new FfmpegProcess(process, savePath);
        }

        private SKBitmap GetFrameFromTick(int currentTick, float sampleTextHeight, IProgress<ProgressReport> progress, UpdateFrame currentFrame = null)
        {
            currentFrame ??= GenerateUpdateFrame(currentTick, sampleTextHeight, progress);
            SKBitmap frame = DrawAnimatedEmotes(currentFrame.Image, currentFrame.Comments, currentTick);
            return frame;
        }

        private SKBitmap DrawAnimatedEmotes(SKBitmap updateFrame, List<CommentSection> comments, int currentTick)
        {
            SKBitmap newFrame = updateFrame.Copy();
            int frameHeight = renderOptions.ChatHeight;
            int currentTickMs = (int)(currentTick * 1000 * (1.0 / renderOptions.Framerate));
            using (SKCanvas frameCanvas = new SKCanvas(newFrame))
            {
                foreach (var comment in comments.Reverse<CommentSection>())
                {
                    frameHeight -= comment.Image.Height + renderOptions.VerticalPadding;
                    foreach ((Point drawPoint, TwitchEmote emote) in comment.Emotes)
                    {
                        if (emote.FrameCount > 1)
                        {
                            int frameIndex = emote.EmoteFrameDurations.Count - 1;
                            int imageFrame = currentTickMs % emote.EmoteFrameDurations.Sum(x => x * 10);
                            for (int i = 0; i < emote.EmoteFrameDurations.Count; i++)
                            {
                                if (imageFrame - emote.EmoteFrameDurations[i] * 10 <= 0)
                                {
                                    frameIndex = i;
                                    break;
                                }
                                imageFrame -= emote.EmoteFrameDurations[i] * 10;
                            }

                            frameCanvas.DrawBitmap(emote.EmoteFrames[frameIndex], drawPoint.X, drawPoint.Y + frameHeight);
                        }
                    }
                }
            }
            return newFrame;
        }

        private UpdateFrame GenerateUpdateFrame(int currentTick, float sampleTextHeight, IProgress<ProgressReport> progress, UpdateFrame lastUpdate = null)
        {
            SKBitmap newFrame = new SKBitmap(renderOptions.ChatWidth, renderOptions.ChatHeight);
            double currentTimeSeconds = currentTick / (double)renderOptions.Framerate;
            int newestCommentIndex = chatRoot.comments.FindLastIndex(x => x.content_offset_seconds <= currentTimeSeconds);

            if (newestCommentIndex == lastUpdate?.CommentIndex)
            {
                return lastUpdate;
            }
            lastUpdate?.Image.Dispose();

            List<CommentSection> commentList = lastUpdate?.Comments ?? new List<CommentSection>();

            int oldCommentIndex = -1;
            if (commentList.Count > 0)
            {
                oldCommentIndex = commentList.Last().CommentIndex;
            }

            if (newestCommentIndex > oldCommentIndex)
            {
                int currentIndex = oldCommentIndex + 1;

                while (newestCommentIndex >= currentIndex)
                {
                    // Skip comments from ignored users
                    if (renderOptions.IgnoreUsersArray.Contains(chatRoot.comments[currentIndex].commenter.name))
                    {
                        currentIndex++;
                        continue;
                    }

                    // Skip comments containing banned words
                    if (renderOptions.BannedWordsArray.Contains(chatRoot.comments[currentIndex].message.body))
                    {
                        currentIndex++;
                        continue;
                    }

                    // Draw the new comments
                    CommentSection comment = GenerateCommentSection(currentIndex, sampleTextHeight, progress);
                    if (comment != null)
                    {
                        commentList.Add(comment);
                    }
                    currentIndex++;
                }
            }

            using (SKCanvas frameCanvas = new SKCanvas(newFrame))
            {
                int commentsDrawn = 0;
                int commentListIndex = commentList.Count - 1;
                int frameHeight = renderOptions.ChatHeight;
                frameCanvas.Clear(renderOptions.BackgroundColor);

                while (commentListIndex >= 0 && frameHeight > -renderOptions.VerticalPadding)
                {
                    var comment = commentList[commentListIndex];
                    frameHeight -= comment.Image.Height + renderOptions.VerticalPadding;
                    frameCanvas.DrawBitmap(comment.Image, 0, frameHeight);

                    for (int i = 0; i < comment.Emotes.Count; i++)
                    {
                        (Point drawPoint, TwitchEmote emote) = comment.Emotes[i];

                        //Only draw static emotes
                        if (emote.FrameCount == 1)
                        {
                            frameCanvas.DrawBitmap(emote.EmoteFrames[0], drawPoint.X, drawPoint.Y + frameHeight);
                        }
                    }
                    commentsDrawn++;
                    commentListIndex--;
                }

                int removeCount = commentList.Count - commentsDrawn;
                for (int i = 0; i < removeCount; i++)
                {
                    commentList[i].Image.Dispose();
                }
                commentList.RemoveRange(0, removeCount);
            }

            return new UpdateFrame() { Image = newFrame, Comments = commentList, CommentIndex = newestCommentIndex };
        }

        // I would prefer if this and its sub-methods were in the CommentSection class ~ScrubN
        private CommentSection GenerateCommentSection(int commentIndex, float sampleTextHeight, IProgress<ProgressReport> progress)
        {
            CommentSection newSection = new CommentSection();
            List<(Point, TwitchEmote)> emoteSectionList = new List<(Point, TwitchEmote)>();
            Comment comment = chatRoot.comments[commentIndex];
            List<SKBitmap> sectionImages = new List<SKBitmap>();
            Point drawPos = new Point();
            Point defaultPos = new Point();
            bool ascentMessage = false;
            defaultPos.X = renderOptions.SidePadding;

            if (comment.source != "chat")
            {
                return null;
            }
            if (comment.message.user_notice_params != null && comment.message.user_notice_params.msg_id != null)
            {
                if (comment.message.user_notice_params.msg_id is not "highlighted-message" and not "sub" and not "resub" and not "subgift" and not "")
                {
                    return null;
                }
                if (!renderOptions.SubMessages && (comment.message.user_notice_params.msg_id is "sub" or "resub" or "subgift"))
                {
                    return null;
                }
                if (comment.message.user_notice_params.msg_id == "highlighted-message" && comment.message.fragments == null && comment.message.body != null)
                {
                    comment.message.fragments = new List<Fragment> { new Fragment() };
                    comment.message.fragments[0].text = comment.message.body;
                }
            }
            if (comment.message.fragments == null || comment.commenter == null)
            {
                return null;
            }

            AddImageSection(sectionImages, ref drawPos, defaultPos);
            defaultPos.Y = (int)(((renderOptions.SectionHeight - sampleTextHeight) / 2.0) + sampleTextHeight);
            drawPos.Y = defaultPos.Y;

            if ((comment.message.user_notice_params != null && (comment.message.user_notice_params.msg_id is "sub" or "resub" or "subgift")) || IsSubMessage(comment))
            {
                ascentMessage = true;
                drawPos.X += renderOptions.AccentIndentWidth;
                defaultPos.X = drawPos.X;
                DrawMessage(comment, sectionImages, emoteSectionList, ref drawPos, defaultPos, progress);
            }
            else
            {
                if (renderOptions.Timestamp)
                {
                    DrawTimestamp(comment, sectionImages, ref drawPos, ref defaultPos);
                }
                if (renderOptions.ChatBadges)
                {
                    DrawBadges(comment, sectionImages, ref drawPos);
                }
                DrawUsername(comment, sectionImages, ref drawPos, progress);
                DrawMessage(comment, sectionImages, emoteSectionList, ref drawPos, defaultPos, progress);
            }

            SKBitmap finalBitmap = CombineImages(sectionImages, ascentMessage);
            newSection.Image = finalBitmap;
            newSection.Emotes = emoteSectionList;
            newSection.CommentIndex = commentIndex;

            return newSection;
        }

        private static bool IsSubMessage(Comment comment)
        {
            //If it looks like a duck, swims like a duck, and quacks like a duck, then it probably is a duck
            if (comment.message.body.StartsWith(comment.commenter.display_name + " subscribed at Tier") || comment.message.body.StartsWith(comment.commenter.display_name + " subscribed with Prime") || comment.message.body.StartsWith(comment.commenter.display_name + " is gifting") || comment.message.body.StartsWith(comment.commenter.display_name + " gifted a Tier"))
                return true;

            return false;
        }

        private SKBitmap CombineImages(List<SKBitmap> sectionImages, bool ascent)
        {
            SKBitmap finalBitmap = new SKBitmap(renderOptions.ChatWidth, sectionImages.Sum(x => x.Height));
            using (SKCanvas finalCanvas = new SKCanvas(finalBitmap))
            {
                for (int i = 0; i < sectionImages.Count; i++)
                {
                    finalCanvas.DrawBitmap(sectionImages[i], 0, i * renderOptions.SectionHeight);
                    sectionImages[i].Dispose();
                }

                if (ascent)
                    finalCanvas.DrawRect(renderOptions.SidePadding, 0, renderOptions.AccentStrokeWidth, finalBitmap.Height, new SKPaint() { Color = SKColor.Parse("#7b2cf2") });
            }
            sectionImages.Clear();
            return finalBitmap;
        }

        private static string GetKeyName(IEnumerable<Codepoint> codepoints)
        {
            List<string> codepointList = new List<string>();
            foreach (Codepoint codepoint in codepoints)
            {
                if (codepoint.Value != 65039)
                {
                    codepointList.Add(codepoint.Value.ToString("X"));
                }
            }

            string emojiKey = string.Join(' ', codepointList);
            return emojiKey;
        }

        private void DrawMessage(Comment comment, List<SKBitmap> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, IProgress<ProgressReport> progress)
        {
            int bitsCount = comment.message.bits_spent;
            foreach (var fragment in comment.message.fragments)
            {
                if (fragment.emoticon == null)
                {
                    // Either text or third party emote
                    string[] fragmentParts = SwapRightToLeft(fragment.text.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    for (int i = 0; i < fragmentParts.Length; i++)
                    {
                        string fragmentString = fragmentParts[i];

                        DrawFragmentPart(sectionImages, emotePositionList, ref drawPos, defaultPos, progress, bitsCount, fragmentString);
                    }
                }
                else
                {
                    DrawFirstPartyEmote(sectionImages, emotePositionList, ref drawPos, defaultPos, fragment);
                }
            }
        }

        private void DrawFragmentPart(List<SKBitmap> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, IProgress<ProgressReport> progress, int bitsCount, string fragmentString)
        {
            if (emoteThirdList.Any(x => x.Name == fragmentString))
            {
                DrawThirdPartyEmote(sectionImages, emotePositionList, ref drawPos, defaultPos, fragmentString);
            }
            else if (ChatRenderRegex.EmojiRegex().IsMatch(fragmentString.AsSpan()))
            {
                DrawEmojiMessage(sectionImages, emotePositionList, ref drawPos, defaultPos, progress, bitsCount, fragmentString);
            }
            else if (!messageFont.ContainsGlyphs(fragmentString) || new StringInfo(fragmentString).LengthInTextElements < fragmentString.Length)
            {
                DrawNonFontMessage(sectionImages, ref drawPos, defaultPos, progress, fragmentString);
            }
            else
            {
                DrawRegularMessage(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, fragmentString);
            }
        }

        private void DrawThirdPartyEmote(List<SKBitmap> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, string fragmentString)
        {
            TwitchEmote twitchEmote = emoteThirdList.First(x => x.Name == fragmentString);
            Point emotePoint = new Point();
            if (!twitchEmote.IsZeroWidth)
            {
                if (drawPos.X + twitchEmote.Width > renderOptions.ChatWidth - renderOptions.SidePadding - defaultPos.X)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }

                emotePoint.X = drawPos.X;
                drawPos.X += twitchEmote.Width + renderOptions.EmoteSpacing;
            }
            else
            {
                emotePoint.X = drawPos.X - renderOptions.EmoteSpacing - twitchEmote.Width;
            }
            emotePoint.Y = (int)(sectionImages.Sum(x => x.Height) - renderOptions.SectionHeight + ((renderOptions.SectionHeight - twitchEmote.Height) / 2.0));
            emotePositionList.Add((emotePoint, twitchEmote));
        }

#pragma warning disable IDE0057
        private void DrawEmojiMessage(List<SKBitmap> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, IProgress<ProgressReport> progress, int bitsCount, string fragmentString)
        {
            ReadOnlySpan<char> fragmentSpan = fragmentString.AsSpan();
            StringBuilder nonEmojiBuffer = new();
            while (fragmentSpan.Length > 0)
            {
                // Old LINQ method. Leaving this for reference
                //List<SingleEmoji> emojiMatches = Emoji.All.Where(x => fragmentString.StartsWith(x.ToString()) && fragmentString.Contains(x.Sequence.AsString.Trim('\uFE0F'))).ToList();

                List<SingleEmoji> emojiMatches = new List<SingleEmoji>();
                foreach (var emoji in Emoji.All)
                {
                    if (fragmentSpan.StartsWith(emoji.ToString()))
                    {
                        emojiMatches.Add(emoji);
                    }
                }

                // Make sure the found emojis actually exist in our cache
                int emojiMatchesCount = emojiMatches.Count;
                for (int j = 0; j < emojiMatchesCount; j++)
                {
                    if (!emojiCache.ContainsKey(GetKeyName(emojiMatches[j].Sequence.Codepoints)))
                    {
                        emojiMatches.RemoveAt(j);
                        emojiMatchesCount--;
                        j--;
                    }
                }

                if (emojiMatchesCount > 0)
                {
                    if (nonEmojiBuffer.Length > 0)
                    {
                        DrawFragmentPart(sectionImages, emotePositionList, ref drawPos, defaultPos, progress, bitsCount, nonEmojiBuffer.ToString());
                        nonEmojiBuffer.Clear();
                    }

                    SingleEmoji selectedEmoji = emojiMatches.OrderByDescending(x => x.Sequence.Codepoints.Count()).First();
                    SKBitmap emojiImage = emojiCache[GetKeyName(selectedEmoji.Sequence.Codepoints)];

                    if (drawPos.X + emojiImage.Width > renderOptions.ChatWidth - renderOptions.SidePadding - defaultPos.X)
                    {
                        AddImageSection(sectionImages, ref drawPos, defaultPos);
                    }

                    Point emotePoint = new Point
                    {
                        X = drawPos.X + (int)Math.Ceiling(renderOptions.EmoteSpacing / 2d), // emotePoint.X halfway through emote padding
                        Y = (int)((renderOptions.SectionHeight - emojiImage.Height) / 2.0)
                    };

                    using (SKCanvas canvas = new SKCanvas(sectionImages.Last()))
                    {
                        canvas.DrawBitmap(emojiImage, emotePoint.X, emotePoint.Y);
                    }

                    drawPos.X += emojiImage.Width + renderOptions.EmoteSpacing;

                    try
                    {
                        fragmentSpan = fragmentSpan.Slice(selectedEmoji.Sequence.AsString.Trim('\uFE0F').Length);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // Once in a blue moon this might happen
                        fragmentSpan = fragmentSpan.Slice(1);
                    }
                }
                else
                {
                    nonEmojiBuffer.Append(fragmentSpan[0]);
                    fragmentSpan = fragmentSpan.Slice(1);
                }
            }
            if (nonEmojiBuffer.Length > 0)
            {
                DrawText(nonEmojiBuffer.ToString(), messageFont, false, sectionImages, ref drawPos, defaultPos);
                nonEmojiBuffer.Clear();
            }
        }
#pragma warning restore IDE0057

        private void DrawNonFontMessage(List<SKBitmap> sectionImages, ref Point drawPos, Point defaultPos, IProgress<ProgressReport> progress, string fragmentString)
        {
            ReadOnlySpan<char> fragmentSpan = fragmentString.Trim('\uFE0F').AsSpan();

            if (ChatRenderRegex.BlockArtRegex().IsMatch(fragmentSpan))
            {
                // Very rough estimation of width of block art
                int textWidth = (int)(fragmentSpan.Length * renderOptions.BlockArtCharWidth);
                if (renderOptions.BlockArtPreWrap && drawPos.X + textWidth > renderOptions.BlockArtPreWrapWidth)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }
            }

            // We cannot draw nonFont chars individually or Arabic script looks improper https://github.com/lay295/TwitchDownloader/issues/484
            // The fragment has either surrogate pairs or characters not in the messageFont
            var inFontBuffer = new StringBuilder();
            var nonFontBuffer = new StringBuilder();
            for (int j = 0; j < fragmentSpan.Length; j++)
            {
                if (char.IsHighSurrogate(fragmentSpan[j]) && j + 1 < fragmentSpan.Length && char.IsLowSurrogate(fragmentSpan[j + 1]))
                {
                    if (inFontBuffer.Length > 0)
                    {
                        DrawText(inFontBuffer.ToString(), messageFont, false, sectionImages, ref drawPos, defaultPos);
                        inFontBuffer.Clear();
                    }
                    if (nonFontBuffer.Length > 0)
                    {
                        using SKPaint nonFontFallbackFont = GetFallbackFont(nonFontBuffer[0], renderOptions, progress).Clone();
                        nonFontFallbackFont.Color = renderOptions.MessageColor;
                        DrawText(nonFontBuffer.ToString(), nonFontFallbackFont, false, sectionImages, ref drawPos, defaultPos);
                        nonFontBuffer.Clear();
                    }
                    int utf32Char = char.ConvertToUtf32(fragmentSpan[j], fragmentSpan[j + 1]);
                    //Don't attempt to draw U+E0000
                    if (utf32Char != 917504)
                    {
                        using SKPaint highSurrogateFallbackFont = GetFallbackFont(utf32Char, renderOptions, progress).Clone();
                        highSurrogateFallbackFont.Color = renderOptions.MessageColor;
                        DrawText(fragmentSpan.Slice(j, 2).ToString(), highSurrogateFallbackFont, false, sectionImages, ref drawPos, defaultPos);
                    }
                    j++;
                }
                else if (!messageFont.ContainsGlyphs(fragmentSpan.Slice(j, 1)) || new StringInfo(fragmentSpan[j].ToString()).LengthInTextElements == 0)
                {
                    if (inFontBuffer.Length > 0)
                    {
                        DrawText(inFontBuffer.ToString(), messageFont, false, sectionImages, ref drawPos, defaultPos);
                        inFontBuffer.Clear();
                    }

                    nonFontBuffer.Append(fragmentSpan[j]);
                }
                else
                {
                    if (nonFontBuffer.Length > 0)
                    {
                        using SKPaint fallbackFont = GetFallbackFont(nonFontBuffer[0], renderOptions, progress).Clone();
                        fallbackFont.Color = renderOptions.MessageColor;
                        DrawText(nonFontBuffer.ToString(), fallbackFont, false, sectionImages, ref drawPos, defaultPos);
                        nonFontBuffer.Clear();
                    }

                    inFontBuffer.Append(fragmentSpan[j]);
                }
            }
            // Only one or the other should occur
            if (nonFontBuffer.Length > 0)
            {
                using SKPaint fallbackFont = GetFallbackFont(nonFontBuffer[0], renderOptions, progress).Clone();
                fallbackFont.Color = renderOptions.MessageColor;
                DrawText(nonFontBuffer.ToString(), fallbackFont, true, sectionImages, ref drawPos, defaultPos);
                nonFontBuffer.Clear();
            }
            if (inFontBuffer.Length > 0)
            {
                DrawText(inFontBuffer.ToString(), messageFont, true, sectionImages, ref drawPos, defaultPos);
                inFontBuffer.Clear();
            }
        }

        private void DrawRegularMessage(List<SKBitmap> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, int bitsCount, string fragmentString)
        {
            bool bitsPrinted = false;
            try
            {
                if (bitsCount > 0 && fragmentString.Any(char.IsDigit) && fragmentString.Any(char.IsLetter))
                {
                    int bitsIndex = fragmentString.IndexOfAny("0123456789".ToCharArray());
                    string outputPrefix = fragmentString.Substring(0, bitsIndex).ToLower();
                    if (cheermotesList.Any(x => x.prefix.ToLower() == outputPrefix))
                    {
                        CheerEmote currentCheerEmote = cheermotesList.First(x => x.prefix.ToLower() == outputPrefix);
                        int bitsAmount = Int32.Parse(fragmentString.Substring(bitsIndex));
                        bitsCount -= bitsAmount;
                        KeyValuePair<int, TwitchEmote> tierList = currentCheerEmote.getTier(bitsAmount);
                        TwitchEmote twitchEmote = tierList.Value;
                        if (drawPos.X + twitchEmote.Width > renderOptions.ChatWidth - renderOptions.SidePadding - defaultPos.X)
                        {
                            AddImageSection(sectionImages, ref drawPos, defaultPos);
                        }
                        Point emotePoint = new Point();
                        emotePoint.X = drawPos.X;
                        emotePoint.Y = (int)(sectionImages.Sum(x => x.Height) - renderOptions.SectionHeight + ((renderOptions.SectionHeight - twitchEmote.Height) / 2.0));
                        emotePositionList.Add((emotePoint, twitchEmote));
                        drawPos.X += twitchEmote.Width + renderOptions.EmoteSpacing;
                        bitsPrinted = true;
                    }
                }
            }
            catch { }
            if (!bitsPrinted)
            {
                DrawText(fragmentString, messageFont, true, sectionImages, ref drawPos, defaultPos);
            }
        }

        private void DrawFirstPartyEmote(List<SKBitmap> sectionImages, List<(Point, TwitchEmote)> emotePositionList, ref Point drawPos, Point defaultPos, Fragment fragment)
        {
            // First party emote
            string emoteId = fragment.emoticon.emoticon_id;
            if (emoteList.Any(x => x.Id == emoteId))
            {
                TwitchEmote twitchEmote = emoteList.First(x => x.Id == emoteId);
                if (drawPos.X + twitchEmote.Width > renderOptions.ChatWidth - renderOptions.SidePadding - defaultPos.X)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }
                Point emotePoint = new Point();
                emotePoint.X = drawPos.X;
                emotePoint.Y = (int)(sectionImages.Sum(x => x.Height) - renderOptions.SectionHeight + ((renderOptions.SectionHeight - twitchEmote.Height) / 2.0));
                emotePositionList.Add((emotePoint, twitchEmote));
                drawPos.X += twitchEmote.Width + renderOptions.EmoteSpacing;
            }
            else
            {
                // Probably an old emote that was removed
                DrawText(fragment.text, messageFont, true, sectionImages, ref drawPos, defaultPos);
            }
        }

        private void DrawText(string drawText, SKPaint textFont, bool padding, List<SKBitmap> sectionImages, ref Point drawPos, Point defaultPos)
        {
            bool isRtl = IsRightToLeft(drawText);
            float textWidth = MeasureText(drawText, textFont, isRtl);
            int effectiveChatWidth = renderOptions.ChatWidth - renderOptions.SidePadding - defaultPos.X;

            // while drawText is wider than the chat width
            while (textWidth > effectiveChatWidth)
            {
                string newDrawText = SubstringToTextWidth(drawText, textFont, effectiveChatWidth, isRtl, new char[] { '?', '-' });

                DrawText(newDrawText, textFont, padding, sectionImages, ref drawPos, defaultPos);

                drawText = drawText[newDrawText.Length..];
                textWidth = MeasureText(drawText, textFont, isRtl);
            }
            if (drawPos.X + textWidth > effectiveChatWidth)
            {
                AddImageSection(sectionImages, ref drawPos, defaultPos);
            }

            using (SKCanvas sectionImageCanvas = new SKCanvas(sectionImages.Last()))
            {
                if (renderOptions.Outline)
                {
                    SKPath outlinePath;
                    if (isRtl)
                    {
                        // There is currently an issue with SKPath.GetTextPath where RTL is not respected so we need to reverse the drawText
                        string reversedText = new string(drawText.Reverse().ToArray());
                        outlinePath = textFont.GetTextPath(reversedText, drawPos.X, drawPos.Y);
                    }
                    else
                    {
                        outlinePath = textFont.GetTextPath(drawText, drawPos.X, drawPos.Y);
                    }
                    SKPaint outlinePaint = new SKPaint() { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(renderOptions.OutlineSize * renderOptions.ReferenceScale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
                    sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
                    outlinePath.Dispose();
                    outlinePaint.Dispose();
                }

                if (ChatRenderRegex.RtlRegex().IsMatch(drawText.AsSpan()))
                {
                    sectionImageCanvas.DrawShapedText(drawText, drawPos.X, drawPos.Y, textFont);
                }
                else
                {
                    sectionImageCanvas.DrawText(drawText, drawPos.X, drawPos.Y, textFont);
                }
            }

            drawPos.X += (int)Math.Floor(textWidth + (padding ? renderOptions.WordSpacing : 0));
        }

#pragma warning disable IDE0057
        /// <summary>
        /// Produces a <see langword="string"/> less than or equal to <paramref name="maxWidth"/> when drawn with <paramref name="textFont"/> OR substringed to the last index of any character in <paramref name="delimiters"/>.
        /// </summary>
        /// <returns>A shortened in visual width or delimited <see langword="string"/>, whichever comes first.</returns>
        private static string SubstringToTextWidth(string text, SKPaint textFont, int maxWidth, bool isRtl, char[] delimiters)
        {
            ReadOnlySpan<char> inputText = text.AsSpan();

            // input text was already less than max width
            if (MeasureText(inputText, textFont, isRtl) <= maxWidth)
            {
                return text;
            }

            // Cut in half until <= width
            int length = inputText.Length;
            do
            {
                length /= 2;
            }
            while (MeasureText(inputText.Slice(0, length), textFont, isRtl) > maxWidth);

            // Add chars until greater than width, then remove the last
            do
            {
                length++;
            } while (MeasureText(inputText.Slice(0, length), textFont, isRtl) < maxWidth);
            inputText = inputText.Slice(0, length - 1);

            // Cut at the last delimiter character if applicable
            int delimiterIndex = inputText.LastIndexOfAny(delimiters);
            if (delimiterIndex != -1)
            {
                return inputText.Slice(0, delimiterIndex + 1).ToString();
            }

            return inputText.ToString();
        }
#pragma warning restore IDE0057

        private static float MeasureText(ReadOnlySpan<char> text, SKPaint textFont, bool? isRtl = null)
        {
            isRtl ??= IsRightToLeft(text[0].ToString());

            if (isRtl == false)
            {
                return textFont.MeasureText(text);
            }
            else
            {
                return MeasureRtlText(text, textFont);
            }
        }

        private static float MeasureRtlText(ReadOnlySpan<char> rtlText, SKPaint textFont)
            => MeasureRtlText(rtlText.ToString(), textFont);

        private static float MeasureRtlText(string rtlText, SKPaint textFont)
        {
            using SKShaper messageShape = new SKShaper(textFont.Typeface);
            SKShaper.Result measure = messageShape.Shape(rtlText, textFont);
            return measure.Width;
        }

        private void DrawUsername(Comment comment, List<SKBitmap> sectionImages, ref Point drawPos, IProgress<ProgressReport> progress)
        {
            using SKCanvas sectionImageCanvas = new SKCanvas(sectionImages.Last());
            SKColor userColor = SKColor.Parse(comment.message.user_color ?? defaultColors[Math.Abs(comment.commenter.display_name.GetHashCode()) % defaultColors.Length]);
            userColor = GenerateUserColor(userColor, renderOptions.BackgroundColor, renderOptions);

            SKPaint userPaint = comment.commenter.display_name.Any(IsNotAscii)
                ? GetFallbackFont(comment.commenter.display_name.Where(IsNotAscii).First(), renderOptions, progress).Clone()
                : nameFont.Clone();

            userPaint.Color = userColor;
            string userName = comment.commenter.display_name + ":";
            int textWidth = (int)userPaint.MeasureText(userName);
            if (renderOptions.Outline)
            {
                SKPath outlinePath = userPaint.GetTextPath(userName, drawPos.X, drawPos.Y);
                sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
            }
            userPaint.Color = userColor;
            sectionImageCanvas.DrawText(userName, drawPos.X, drawPos.Y, userPaint);
            drawPos.X += textWidth + renderOptions.WordSpacing;
            userPaint.Dispose();
        }

        private static SKColor GenerateUserColor(SKColor userColor, SKColor background_color, ChatRenderOptions renderOptions)
        {
            background_color.ToHsl(out _, out _, out float backgroundBrightness);
            userColor.ToHsl(out float userHue, out float userSaturation, out float userBrightness);

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

#if DEBUG
        //For debugging, works on Windows only
        private static void OpenImage(SKBitmap newBitmap)
        {
            string tempFile = Path.GetFileNameWithoutExtension(Path.GetTempFileName()) + ".png";
            using (FileStream fs = new FileStream(tempFile, FileMode.Create))
                newBitmap.Encode(SKEncodedImageFormat.Png, 100).SaveTo(fs);

            Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
        }
#endif
        private void DrawBadges(Comment comment, List<SKBitmap> sectionImages, ref Point drawPos)
        {
            using SKCanvas sectionImageCanvas = new SKCanvas(sectionImages.Last());
            List<(SKBitmap, ChatBadgeType)> badgeImages = ParseCommentBadges(comment);
            foreach (var (badgeImage, badgeType) in badgeImages)
            {
                //Don't render filtered out badges
                if (((ChatBadgeType)renderOptions.ChatBadgeMask).HasFlag(badgeType))
                    continue;

                float badgeY = (float)((renderOptions.SectionHeight - badgeImage.Height) / 2.0);
                sectionImageCanvas.DrawBitmap(badgeImage, drawPos.X, badgeY);
                drawPos.X += badgeImage.Width + renderOptions.WordSpacing / 2;
            }
        }

        private List<(SKBitmap badgeImage, ChatBadgeType badgeType)> ParseCommentBadges(Comment comment)
        {
            List<(SKBitmap, ChatBadgeType)> returnList = new List<(SKBitmap, ChatBadgeType)>();

            if (comment.message.user_badges != null)
            {
                foreach (var badge in comment.message.user_badges)
                {
                    bool foundBadge = false;
                    string id = badge._id.ToString();
                    string version = badge.version.ToString();

                    foreach (var cachedBadge in badgeList)
                    {
                        if (cachedBadge.Name != id)
                            continue;

                        foreach (var cachedVersion in cachedBadge.Versions)
                        {
                            if (cachedVersion.Key == version)
                            {
                                returnList.Add((cachedVersion.Value, cachedBadge.Type));
                                foundBadge = true;
                                break;
                            }
                        }

                        if (foundBadge)
                            break;
                    }
                }
            }

            return returnList;
        }

        private void DrawTimestamp(Comment comment, List<SKBitmap> sectionImages, ref Point drawPos, ref Point defaultPos)
        {
            using SKCanvas sectionImageCanvas = new SKCanvas(sectionImages.Last());
            TimeSpan timestamp = new TimeSpan(0, 0, (int)comment.content_offset_seconds);
            string timeString = "";

            if (timestamp.Hours >= 1)
                timeString = timestamp.ToString(@"h\:mm\:ss");
            else
                timeString = timestamp.ToString(@"m\:ss");
            int textWidth = (int)messageFont.MeasureText(Regex.Replace(timeString, "[0-9]", "0"));
            if (renderOptions.Outline)
            {
                SKPath outlinePath = messageFont.GetTextPath(timeString, drawPos.X, drawPos.Y);
                sectionImageCanvas.DrawPath(outlinePath, outlinePaint);
            }
            sectionImageCanvas.DrawText(timeString, drawPos.X, drawPos.Y, messageFont);
            drawPos.X += textWidth + (renderOptions.WordSpacing * 2);
            defaultPos.X = drawPos.X;
        }

        private void AddImageSection(List<SKBitmap> sectionImages, ref Point drawPos, Point defaultPos)
        {
            drawPos.X = defaultPos.X;
            drawPos.Y = defaultPos.Y;
            sectionImages.Add(new SKBitmap(renderOptions.ChatWidth, renderOptions.SectionHeight));
        }

        /// <summary>
        /// Fetches the emotes/badges/bits/emojis needed to render
        /// </summary>
        /// <remarks>chatRoot.embeddedData will be empty after calling this to save on memory!</remarks>
        private async Task FetchImages(CancellationToken cancellationToken)
        {
            Task<List<ChatBadge>> badgeTask = TwitchHelper.GetChatBadges(chatRoot.streamer.id, renderOptions.TempFolder, chatRoot.embeddedData, renderOptions.Offline);
            Task<List<TwitchEmote>> emoteTask = TwitchHelper.GetEmotes(chatRoot.comments, renderOptions.TempFolder, chatRoot.embeddedData, renderOptions.Offline);
            Task<List<TwitchEmote>> emoteThirdTask = TwitchHelper.GetThirdPartyEmotes(chatRoot.streamer.id, renderOptions.TempFolder, chatRoot.embeddedData, renderOptions.BttvEmotes, renderOptions.FfzEmotes, renderOptions.StvEmotes, renderOptions.Offline, cancellationToken);
            Task<List<CheerEmote>> cheerTask = TwitchHelper.GetBits(renderOptions.TempFolder, chatRoot.streamer.id.ToString(), chatRoot.embeddedData, renderOptions.Offline);
            Task<Dictionary<string, SKBitmap>> emojiTask = TwitchHelper.GetTwitterEmojis(renderOptions.TempFolder);

            await Task.WhenAll(badgeTask, emoteTask, emoteThirdTask, cheerTask, emojiTask);

            // Clear chatRoot.embeddedData to save on some memory
            chatRoot.embeddedData = null;

            badgeList = badgeTask.Result;
            emoteList = emoteTask.Result;
            emoteThirdList = emoteThirdTask.Result;
            cheermotesList = cheerTask.Result;
            emojiCache = emojiTask.Result;
        }

        //Precompute scaled images so we don't have to scale every frame
        private void ScaleImages()
        {
            foreach (var emote in emoteList.Union(emoteThirdList))
            {
                double newScale = (2.0 / emote.ImageScale) * renderOptions.ReferenceScale * renderOptions.EmoteScale;
                if (newScale != 1.0)
                    emote.Resize(newScale);
            }

            foreach (var badge in badgeList)
            {
                //Assume badges are always 2x scale, not 1x or 4x
                if (renderOptions.ReferenceScale != 1.0)
                    badge.Resize(renderOptions.ReferenceScale * renderOptions.BadgeScale);
            }

            foreach (var cheer in cheermotesList)
            {
                //Assume cheermotes are always 2x scale, not 1x or 4x
                if (renderOptions.ReferenceScale != 1.0)
                    cheer.Resize(renderOptions.ReferenceScale * renderOptions.EmoteScale);
            }

            //Assume emojis are 4x (they're 72x72)
            double emojiScale = 0.5 * renderOptions.ReferenceScale * renderOptions.EmojiScale;
            List<string> emojiKeys = new List<string>(emojiCache.Keys);
            foreach (var emojiKey in emojiKeys)
            {
                SKImageInfo imageInfo = new SKImageInfo((int)(emojiCache[emojiKey].Width * emojiScale), (int)(emojiCache[emojiKey].Height * emojiScale));
                SKBitmap newBitmap = new SKBitmap(imageInfo);
                emojiCache[emojiKey].ScalePixels(newBitmap, SKFilterQuality.High);
                emojiCache[emojiKey].Dispose();
                emojiCache[emojiKey] = newBitmap;
            }
        }

        private (int startTick, int totalTicks) GetVideoTicks()
        {
            if (renderOptions.StartOverride != -1 && renderOptions.EndOverride != -1)
            {
                int startSeconds = renderOptions.StartOverride;
                int videoStartTick = startSeconds * renderOptions.Framerate;
                int totalTicks = renderOptions.EndOverride * renderOptions.Framerate - videoStartTick;
                return (videoStartTick, totalTicks);
            }
            else if (chatRoot.video != null)
            {
                int startSeconds = (int)Math.Floor(chatRoot.video.start);
                int videoStartTick = startSeconds * renderOptions.Framerate;
                int totalTicks = (int)Math.Ceiling(chatRoot.video.end * renderOptions.Framerate) - videoStartTick;
                return (videoStartTick, totalTicks);
            }
            else
            {
                int videoStartTick = (int)Math.Floor(chatRoot.comments.First().content_offset_seconds * renderOptions.Framerate);
                int totalTicks = (int)Math.Ceiling(chatRoot.comments.Last().content_offset_seconds * renderOptions.Framerate) - videoStartTick;
                return (videoStartTick, totalTicks);
            }
        }

        public SKPaint GetFallbackFont(int input, ChatRenderOptions renderOptions, IProgress<ProgressReport> progress = null)
        {
            if (fallbackCache.ContainsKey(input))
                return fallbackCache[input];

            SKPaint newPaint = new SKPaint() { Typeface = fontManager.MatchCharacter(input), LcdRenderText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, SubpixelText = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };

            if (newPaint.Typeface == null)
            {
                newPaint.Typeface = SKTypeface.Default;
                progress?.Report(new ProgressReport(ReportType.Log, "No valid typefaces were found for some messages."));
            }

            fallbackCache.TryAdd(input, newPaint);
            return newPaint;
        }

        private static bool IsNotAscii(char input)
        {
            return input > 127;
        }

        private static string[] SwapRightToLeft(string[] words)
        {
            List<string> finalWords = new List<string>();
            Stack<string> rtlStack = new Stack<string>();
            foreach (var word in words)
            {
                if (IsRightToLeft(word))
                {
                    rtlStack.Push(word);
                }
                else
                {
                    while (rtlStack.Count > 0)
                    {
                        finalWords.Add(rtlStack.Pop());
                    }
                    finalWords.Add(word);
                }
            }
            while (rtlStack.Count > 0)
            {
                finalWords.Add(rtlStack.Pop());
            }
            return finalWords.ToArray();
        }

        private static bool IsRightToLeft(string message)
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

        public async Task<ChatRoot> ParseJsonAsync(CancellationToken cancellationToken = new())
        {
            chatRoot = await ChatJson.DeserializeAsync(renderOptions.InputFile, true, true, cancellationToken);

            chatRoot.streamer ??= new Streamer
            {
                id = int.Parse(chatRoot.comments.First().channel_id),
                name = await TwitchHelper.GetStreamerName(int.Parse(chatRoot.comments.First().channel_id))
            };

            return chatRoot;
        }

        ~ChatRenderer()
        {
            chatRoot = null;
            badgeList = null;
            emoteList = null;
            emoteThirdList = null;
            cheermotesList = null;
            emojiCache = null;
            fallbackCache = null;
            fontManager.Dispose();
            outlinePaint.Dispose();
            nameFont.Dispose();
            messageFont.Dispose();
        }
    }
}
