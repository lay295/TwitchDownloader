using SkiaSharp;
using SkiaSharp.HarfBuzz;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Models.Render;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Services;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore
{
    public sealed partial class ChatRenderer : IDisposable
    {
        private const char ZERO_WIDTH_JOINER = '\u200D';
        public bool Disposed { get; private set; } = false;
        public ChatRoot chatRoot { get; private set; } = new ChatRoot();

        private static readonly SKColor Purple = new(0xFF7B2CF2); // AARRGGBB
        private static readonly SKColor[] DefaultUsernameColors = [new(0xFFFF0000), new(0xFF0000FF), new(0xFF00FF00), new(0xFFB22222), new(0xFFFF7F50), new(0xFF9ACD32), new(0xFFFF4500), new(0xFF2E8B57), new(0xFFDAA520), new(0xFFD2691E), new(0xFF5F9EA0), new(0xFF1E90FF), new(0xFFFF69B4), new(0xFF8A2BE2), new(0xFF00FF7F)];

        private static readonly string[] DefaultAvatarUrls =
        [
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/75305d54-c7cc-40d1-bb9c-91fbe85943c7-profile_image-70x70.png",
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/ebe4cd89-b4f4-4cd9-adac-2f30151b4209-profile_image-70x70.png",
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/215b7342-def9-11e9-9a66-784f43822e80-profile_image-70x70.png",
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/cdd517fe-def4-11e9-948e-784f43822e80-profile_image-70x70.png",
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/41780b5a-def8-11e9-94d9-784f43822e80-profile_image-70x70.png",
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/13e5fa74-defa-11e9-809c-784f43822e80-profile_image-70x70.png",
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/de130ab0-def7-11e9-b668-784f43822e80-profile_image-70x70.png",
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/ead5c8b2-a4c9-4724-b1dd-9f00b46cbd3d-profile_image-70x70.png",
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/ce57700a-def9-11e9-842d-784f43822e80-profile_image-70x70.png",
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/998f01ae-def8-11e9-b95c-784f43822e80-profile_image-70x70.png",
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/dbdc9198-def8-11e9-8681-784f43822e80-profile_image-70x70.png",
            "https://static-cdn.jtvnw.net/user-default-pictures-uv/294c98b5-e34d-42cd-a8f0-140b72fba9b0-profile_image-70x70.png",
        ];

        [GeneratedRegex("[\u0591-\u07FF\uFB1D-\uFDFD\uFE70-\uFEFC]")]
        private static partial Regex RtlRegex { get; }

        [GeneratedRegex("[\u2500-\u257F\u2580-\u259F\u2800-\u28FF]")]
        private static partial Regex BlockArtRegex { get; }

        private static readonly SearchValues<char> DigitChars = SearchValues.Create("0123456789");

        private readonly ITaskProgress _progress;
        private readonly ChatRenderOptions renderOptions;
        private readonly string _cacheDir;
        private DisposableDictionary<string, ChatBadge> _badgeCache = [];
        private DisposableDictionary<string, TwitchEmote> _emoteCache = [];
        private DisposableDictionary<string, TwitchEmote> _emoteThirdCache = [];
        private DisposableDictionary<string, CheerEmote> _cheermoteCache = [];
        private DisposableDictionary<string, SKImage> _emojiCache = [];
        private DisposableDictionary<string, SKImage> _avatarCache = [];
        private DisposableDictionary<int, SKPaint> _fallbackFontCache = [];
        private DisposableDictionary<SKColor, SKPaint> _paintCache = [];
        private readonly SectionImageCache _sectionImageCache = new();
        private bool noFallbackFontFound = false;
        private readonly SKFontManager fontManager = SKFontManager.CreateDefault();
        private SKPaint messageFont;
        private SKPaint nameFont;
        private SKPaint outlinePaint;
        private readonly HighlightIcons highlightIcons;
        private int _usernameCenteredY;

        private Dictionary<int, string[]> AllEmojiSequences => field ??=
            _emojiCache.Keys
                .GroupBy(x => Rune.GetRuneAt(x, 0).Value)
                .ToDictionary(x => x.Key, x => x.ToArray());

        // Persistent buffer for the composited animated-emote frame, reused across ticks so that an
        // unchanged frame does not need to be copied and recomposited. See DrawAnimatedEmotes.
        private SKBitmap _animComposedFrame;
        private SKCanvas _animCanvas;
        private int _animByteSize;
        private int _animComposedForCommentIndex = int.MinValue;
        private readonly List<int> _animLastFrameIndices = [];

        public ChatRenderer(ChatRenderOptions chatRenderOptions, ITaskProgress progress)
        {
            renderOptions = chatRenderOptions;
            _cacheDir = CacheDirectoryService.GetCacheDirectory(renderOptions.TempFolder);
            renderOptions.BlockArtPreWrapWidth = 29.166 * renderOptions.FontSize - renderOptions.SidePadding * 2;
            renderOptions.BlockArtPreWrap = renderOptions.ChatWidth > renderOptions.BlockArtPreWrapWidth;
            _progress = progress;
            outlinePaint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = (float)(renderOptions.OutlineSize * renderOptions.ReferenceScale), StrokeJoin = SKStrokeJoin.Round, Color = SKColors.Black, IsAntialias = true, IsAutohinted = true, LcdRenderText = true, SubpixelText = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            nameFont = new SKPaint { LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.EffectiveUsernameFontSize, IsAntialias = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            messageFont = new SKPaint { LcdRenderText = true, SubpixelText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High, Color = renderOptions.MessageColor };
            highlightIcons = new HighlightIcons(renderOptions, _cacheDir, Purple, outlinePaint);
        }

        public async Task RenderVideoAsync(CancellationToken cancellationToken)
        {
            var outputFileInfo = TwitchHelper.ClaimFile(renderOptions.OutputFile, renderOptions.FileCollisionCallback, _progress);
            renderOptions.OutputFile = outputFileInfo.FullName;
            var maskFileInfo = renderOptions.GenerateMask ? TwitchHelper.ClaimFile(renderOptions.MaskFile, renderOptions.FileCollisionCallback, _progress) : null;

            // Open the destination files so that they exist in the filesystem.
            await using var outputFs = outputFileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var maskFs = maskFileInfo?.Open(FileMode.Create, FileAccess.Write, FileShare.Read);

            try
            {
                await RenderAsyncImpl(outputFileInfo, outputFs, maskFileInfo, maskFs, cancellationToken);
            }
            catch
            {
                await Task.Delay(100, CancellationToken.None);

                TwitchHelper.CleanUpClaimedFile(outputFileInfo, outputFs, _progress);
                TwitchHelper.CleanUpClaimedFile(maskFileInfo, maskFs, _progress);

                throw;
            }
        }

        private async Task RenderAsyncImpl(FileInfo outputFileInfo, FileStream outputFs, FileInfo maskFileInfo, FileStream maskFs, CancellationToken cancellationToken)
        {
            _progress.SetStatus("Fetching Images [1/2]");
            await Task.Run(() => FetchScaledImages(cancellationToken), cancellationToken);

            if (renderOptions.DisperseCommentOffsets)
            {
                DisperseCommentOffsets(chatRoot.comments);
            }
            FloorCommentOffsets(chatRoot.comments);

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

            // Cache the rendered timestamp widths
            renderOptions.TimestampWidths = !renderOptions.Timestamp ? Array.Empty<int>() : new[]
            {
                (int)messageFont.MeasureText("0:00"),
                (int)messageFont.MeasureText("00:00"),
                (int)messageFont.MeasureText("0:00:00"),
                (int)messageFont.MeasureText("00:00:00")
            };

            // Cache the username vertical centering offset. The username font may be sized differently from
            // the message font (UsernameFontScale), so it needs to be centered using its own metrics rather
            // than the message-derived sectionDefaultYPos. This is constant for the whole render.
            SKRect usernameBounds = new SKRect();
            nameFont.MeasureText("ABC123", ref usernameBounds);
            _usernameCenteredY = (int)(((renderOptions.SectionHeight - usernameBounds.Height) / 2.0) + usernameBounds.Height);

            // Rough estimation of the width of a single block art character
            renderOptions.BlockArtCharWidth = GetFallbackFont('█').MeasureText("█");

            RemoveRestrictedComments(chatRoot.comments);

            (int startTick, int totalTicks) = GetVideoTicks();

            // Delete the files as it is not guaranteed that the overwrite flag is passed in the FFmpeg args.
            outputFs.Close();
            outputFileInfo.Refresh();
            if (outputFileInfo.Exists)
                outputFileInfo.Delete();

            maskFs?.Close();
            maskFileInfo?.Refresh();
            if (renderOptions.GenerateMask && maskFileInfo!.Exists)
                maskFileInfo.Delete();

            FfmpegProcess ffmpegProcess = GetFfmpegProcess(outputFileInfo);
            FfmpegProcess maskProcess = renderOptions.GenerateMask ? GetFfmpegProcess(maskFileInfo, true) : null;
            _progress.SetTemplateStatus(@"Rendering Video {0}% ({1:h\hm\ms\s} Elapsed | {2:h\hm\ms\s} Remaining)", 0, TimeSpan.Zero, TimeSpan.Zero);

            try
            {
                await Task.Run(() => RenderVideoSection(startTick, startTick + totalTicks, ffmpegProcess, maskProcess, cancellationToken), cancellationToken);
            }
            catch
            {
                ffmpegProcess.Dispose();
                maskProcess?.Dispose();
                GC.Collect();
                throw;
            }
        }

        /* Due to Twitch changing the API to return only whole number offsets, renders have become less readable.
         * To get around this we can disperse comment offsets according to their creation date milliseconds to
         * help bring back the better readability of comments coming in 1-by-1 */
        private static void DisperseCommentOffsets(List<Comment> comments)
        {
            // Enumerating over a span is faster than a list
            var commentSpan = CollectionsMarshal.AsSpan(comments);

            foreach (var c in commentSpan)
            {
                if (c.content_offset_seconds % 1 == 0 && c.created_at.Millisecond != 0)
                {
                    const int MILLIS_PER_HALF_SECOND = 500;
                    const double MILLIS_PER_SECOND = 1000.0;
                    // Finding the difference between the creation dates and offsets is inconsistent. This approximation looks better more often.
                    c.content_offset_seconds += (c.created_at.Millisecond - MILLIS_PER_HALF_SECOND) / MILLIS_PER_SECOND;
                }
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

        private void RemoveRestrictedComments(List<Comment> comments)
        {
            if (renderOptions.IgnoreUsersArray.Length == 0 && renderOptions.BannedWordsArray.Length == 0)
            {
                return;
            }

            var ignoredUsers = new HashSet<string>(renderOptions.IgnoreUsersArray, StringComparer.InvariantCultureIgnoreCase);

            Regex bannedWordsRegex = null;
            if (renderOptions.BannedWordsArray.Length > 0)
            {
                var bannedWords = string.Join('|', renderOptions.BannedWordsArray.Select(Regex.Escape));
                bannedWordsRegex = new Regex(@$"(?<=\b|[\d\p{{Pc}}]){bannedWords}(?=\b|[\d\p{{Pc}}])",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            for (var i = comments.Count - 1; i >= 0; i--)
            {
                var comment = comments[i];
                var commenter = comment.commenter;

                if (ignoredUsers.Contains(commenter.name) // ASCII login name
                    || (commenter.display_name.Any(IsNotAscii) && ignoredUsers.Contains(commenter.display_name)) // Potentially non-ASCII display name
                    || (bannedWordsRegex is not null && bannedWordsRegex.IsMatch(comment.message.body))) // Banned words
                {
                    comments.RemoveAt(i);
                }
            }
        }

        private static SKTypeface GetInterTypeface(SKFontStyle fontStyle)
        {
            MemoryStream stream = null;
            try {
                if (fontStyle == SKFontStyle.Bold)
                    stream = new MemoryStream(Properties.Resources.InterBold);
                else if (fontStyle == SKFontStyle.Italic)
                    stream = new MemoryStream(Properties.Resources.InterItalic);
                else if (fontStyle == SKFontStyle.BoldItalic)
                    stream = new MemoryStream(Properties.Resources.InterBoldItalic);
                else
                    stream = new MemoryStream(Properties.Resources.Inter);

                return SKTypeface.FromStream(stream);
            }
            finally
            {
                stream?.Dispose();
            }
        }

        private void RenderVideoSection(int startTick, int endTick, FfmpegProcess ffmpegProcess, FfmpegProcess maskProcess = null, CancellationToken cancellationToken = new())
        {
            var ffmpegStream = ffmpegProcess.StandardInput.BaseStream;
            var maskStream = maskProcess?.StandardInput.BaseStream;

            DriveInfo outputDrive = DriveHelper.GetOutputDrive(ffmpegProcess.SavePath);

            Stopwatch stopwatch = Stopwatch.StartNew();

            // Measure some sample text to determine the text height, cannot assume it is font size
            SKRect sampleTextBounds = new SKRect();
            messageFont.MeasureText("ABC123", ref sampleTextBounds);
            int sectionDefaultYPos = (int)(((renderOptions.SectionHeight - sampleTextBounds.Height) / 2.0) + sampleTextBounds.Height);

            byte[] maskBytes = null;
            UpdateFrame latestUpdate = null;
            for (int currentTick = startTick; currentTick < endTick; currentTick++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (currentTick % renderOptions.UpdateFrame == 0)
                {
                    latestUpdate = GenerateUpdateFrame(currentTick, sectionDefaultYPos, latestUpdate);
                }

                var frame = GetFrameFromTick(currentTick, sectionDefaultYPos, latestUpdate);

                if (!renderOptions.SkipDriveWaiting)
                    DriveHelper.WaitForDrive(outputDrive, _progress);

                var frameSpan = frame.GetPixelSpan();
                ffmpegStream.Write(frameSpan);

                if (maskProcess != null)
                {
                    maskBytes ??= GC.AllocateUninitializedArray<byte>(frameSpan.Length / 4);
                    SetFrameMask(frameSpan, maskBytes);

                    if (!renderOptions.SkipDriveWaiting)
                        DriveHelper.WaitForDrive(outputDrive, _progress);

                    maskStream.Write(maskBytes);
                }

                if (currentTick % 3 == 0)
                {
                    var percent = (currentTick - startTick) / (double)(endTick - startTick) * 100;
                    var elapsed = stopwatch.Elapsed;
                    var elapsedSeconds = elapsed.TotalSeconds;

                    var secondsLeft = unchecked((int)(100 / percent * elapsedSeconds - elapsedSeconds));
                    _progress.ReportProgress((int)Math.Round(percent), elapsed, TimeSpan.FromSeconds(secondsLeft));
                }
            }

            stopwatch.Stop();
            _progress.ReportProgress(100, stopwatch.Elapsed, TimeSpan.Zero);
            _progress.LogInfo($"FINISHED. RENDER TIME: {stopwatch.Elapsed.TotalSeconds:F1}s SPEED: {(endTick - startTick) / (double)renderOptions.Framerate / stopwatch.Elapsed.TotalSeconds:F2}x");

            latestUpdate?.Image.Dispose();
            latestUpdate?.Comments.ForEach(x => x.Image.Dispose());

            ffmpegStream.Dispose();
            maskStream?.Dispose();

            ffmpegProcess.WaitForExit(100_000);
            maskProcess?.WaitForExit(100_000);
        }

        private static unsafe void SetFrameMask(ReadOnlySpan<byte> frame, Span<byte> maskBytes)
        {
            Debug.Assert(frame.Length == maskBytes.Length * 4); // 32bpp -> 8bpp

            var produced = 0;
            var outCount = maskBytes.Length;

            fixed (byte* pFrame = frame)
            fixed (byte* pMask = maskBytes)
            {
                if (Avx2.IsSupported)
                {
                    // Take every 4th byte. AVX2 shuffles each 128-bit lane independently
                    var shuffleMask = Vector256.Create(
                        3, 7, 11, 15, 0x80, 0x80, 0x80, 0x80,
                        0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
                        3, 7, 11, 15, 0x80, 0x80, 0x80, 0x80,
                        0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80
                    );

                    for (; produced + 8 <= outCount; produced += 8)
                    {
                        var vec = Avx.LoadVector256(pFrame + produced * 4);
                        var shuffle = Avx2.Shuffle(vec, shuffleMask);

                        var lo = shuffle.GetLower().AsUInt32().ToScalar();
                        var hi = shuffle.GetUpper().AsUInt32().ToScalar();
                        *(ulong*)(pMask + produced) = lo | ((ulong)hi << 32);
                    }
                }
                else if (Ssse3.IsSupported)
                {
                    // Take every 4th byte
                    var shuffleMask = Vector128.Create(
                        3, 7, 11, 15,
                        0x80, 0x80, 0x80, 0x80,
                        0x80, 0x80, 0x80, 0x80,
                        0x80, 0x80, 0x80, 0x80
                    );

                    for (; produced + 4 <= outCount; produced += 4)
                    {
                        var vec = Sse2.LoadVector128(pFrame + produced * 4);
                        var shuffle = Ssse3.Shuffle(vec, shuffleMask);

                        *(uint*)(pMask + produced) = shuffle.AsUInt32().ToScalar();
                    }
                }
                else if (AdvSimd.Arm64.IsSupported)
                {
                    // Take every 4th byte
                    var tableIdx = Vector128.Create(
                        3, 7, 11, 15,
                        0xFF, 0xFF, 0xFF, 0xFF,
                        0xFF, 0xFF, 0xFF, 0xFF,
                        0xFF, 0xFF, 0xFF, 0xFF
                    );

                    for (; produced + 4 <= outCount; produced += 4)
                    {
                        var vec = AdvSimd.LoadVector128(pFrame + produced * 4);
                        var shuffle = AdvSimd.Arm64.VectorTableLookup(vec, tableIdx);

                        *(uint*)(pMask + produced) = shuffle.AsUInt32().ToScalar();
                    }
                }

                // Scalar fallback for when SIMD is unavailable/finish copy if vector size % outCount != 0
                var pF = pFrame + produced * 4 + 3;
                var pM = pMask + produced;
                var frameEnd = pFrame + frame.Length;
                for (; pF < frameEnd; pF += 4)
                {
                    *pM++ = *pF;
                }
            }
        }

        private FfmpegProcess GetFfmpegProcess(FileInfo fileInfo, bool isMask = false)
        {
            string savePath = fileInfo.FullName;
            var pixFmt = isMask
                ? "gray"
                : SKImageInfo.PlatformColorType == SKColorType.Bgra8888 ? "bgra" : "rgba";

            string inputArgs = new StringBuilder(renderOptions.InputArgs)
                .Replace("{fps}", renderOptions.Framerate.ToString())
                .Replace("{height}", renderOptions.ChatHeight.ToString())
                .Replace("{width}", renderOptions.ChatWidth.ToString())
                .Replace("{save_path}", savePath)
                .Replace("{max_int}", int.MaxValue.ToString())
                .Replace("{pix_fmt}", pixFmt)
                .ToString();
            string outputArgs = new StringBuilder(renderOptions.OutputArgs)
                .Replace("{fps}", renderOptions.Framerate.ToString())
                .Replace("{height}", renderOptions.ChatHeight.ToString())
                .Replace("{width}", renderOptions.ChatWidth.ToString())
                .Replace("{save_path}", savePath)
                .Replace("{max_int}", int.MaxValue.ToString())
                .ToString();

            var process = new FfmpegProcess
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
                },
                SavePath = savePath
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    _progress.LogFfmpeg(e.Data);
                }
            };

            _progress.LogVerbose($"Running \"{renderOptions.FfmpegPath}\" in \"{process.StartInfo.WorkingDirectory}\" with args: {process.StartInfo.Arguments}");

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            return process;
        }

        private SKBitmap GetFrameFromTick(int currentTick, int sectionDefaultYPos, UpdateFrame currentFrame = null)
        {
            currentFrame ??= GenerateUpdateFrame(currentTick, sectionDefaultYPos);
            return DrawAnimatedEmotes(currentFrame, currentTick);
        }

        private SKBitmap DrawAnimatedEmotes(UpdateFrame currentFrame, int currentTick)
        {
            var updateFrame = currentFrame.Image;
            var comments = currentFrame.Comments;
            var commentIndex = currentFrame.CommentIndex;
            var currentTickMs = (long)(currentTick / (double)renderOptions.Framerate * 1000);

            var hasAnimatedEmotes = false;
            foreach (var comment in comments)
            {
                foreach (var (_, emote) in comment.Emotes)
                {
                    if (emote.FrameCount > 1)
                    {
                        hasAnimatedEmotes = true;
                        break;
                    }
                }

                if (hasAnimatedEmotes) break;
            }

            if (!hasAnimatedEmotes)
            {
                InvalidateAnimCache();
                return updateFrame;
            }

            // Reuse the previously composited frame when the visible comments and every animated emote's
            // frame index are unchanged since the last tick. At high frame rates the animation advances
            // more slowly than the video, so most ticks hit this path and skip the copy + compositing.
            if (_animComposedFrame != null && commentIndex == _animComposedForCommentIndex && AnimCacheIsValid(comments, currentTickMs))
            {
                return _animComposedFrame;
            }

            ComposeAnimatedFrame(updateFrame, comments, currentTickMs);
            _animComposedForCommentIndex = commentIndex;
            RecordAnimFrameIndices(comments, currentTickMs);
            return _animComposedFrame;
        }

        /// <summary>
        /// Composites the current animated-emote frames onto a copy of <paramref name="updateFrame"/> held in
        /// the persistent <see cref="_animComposedFrame"/> buffer.
        /// </summary>
        private void ComposeAnimatedFrame(SKBitmap updateFrame, List<CommentSection> comments, long currentTickMs)
        {
            if (_animComposedFrame == null)
            {
                _animComposedFrame = new SKBitmap(updateFrame.Info);
                _animByteSize = updateFrame.Info.BytesSize;
                _animCanvas = new SKCanvas(_animComposedFrame);
            }

            // Copy the background pixels straight into the buffer the canvas is bound to. CopyTo(bitmap) always
            // allocates a new buffer, even if the old buffer is the same size, so a raw memcpy into the existing
            // buffer is used instead.
            unsafe
            {
                Buffer.MemoryCopy((void*)updateFrame.GetPixels(), (void*)_animComposedFrame.GetPixels(), _animByteSize, _animByteSize);
            }

            var frameHeight = renderOptions.ChatHeight;
            var verticalPadding = renderOptions.VerticalPadding;
            for (var c = comments.Count - 1; c >= 0; c--)
            {
                var comment = comments[c];
                frameHeight -= comment.Image.Info.Height + verticalPadding;
                foreach (var (drawPoint, emote) in comment.Emotes)
                {
                    if (emote.FrameCount > 1)
                    {
                        _animCanvas.DrawImage(emote.EmoteFrames[ComputeAnimFrameIndex(emote, currentTickMs)], drawPoint.X, drawPoint.Y + frameHeight);
                    }
                }
            }
        }

        private static int ComputeAnimFrameIndex(TwitchEmote emote, long currentTickMs)
        {
            var imageFrame = currentTickMs % (emote.TotalDuration * 10);
            var durations = emote.EmoteFrameDurations;
            for (var i = 0; i < durations.Count; i++)
            {
                imageFrame -= durations[i] * 10;

                if (imageFrame <= 0) return i;
            }

            return emote.EmoteFrameDurations.Count - 1;
        }

        /// <summary>Returns <see langword="true"/> when every animated emote is on the same frame index as when the cache was built.</summary>
        private bool AnimCacheIsValid(List<CommentSection> comments, long currentTickMs)
        {
            var i = 0;
            for (var c = comments.Count - 1; c >= 0; c--)
            {
                foreach (var (_, emote) in comments[c].Emotes)
                {
                    if (emote.FrameCount > 1)
                    {
                        if (i >= _animLastFrameIndices.Count) return false;
                        if (ComputeAnimFrameIndex(emote, currentTickMs) != _animLastFrameIndices[i]) return false;
                        i++;
                    }
                }
            }

            return i == _animLastFrameIndices.Count;
        }

        private void RecordAnimFrameIndices(List<CommentSection> comments, long currentTickMs)
        {
            _animLastFrameIndices.Clear();
            for (var c = comments.Count - 1; c >= 0; c--)
            {
                foreach (var (_, emote) in comments[c].Emotes)
                {
                    if (emote.FrameCount > 1) _animLastFrameIndices.Add(ComputeAnimFrameIndex(emote, currentTickMs));
                }
            }
        }

        private void InvalidateAnimCache()
        {
            _animComposedForCommentIndex = int.MinValue;
            _animLastFrameIndices.Clear();
        }

        private int GetNewestCommentIndex(int lastIndex, double currentTimeSeconds)
        {
            var commentSpan = CollectionsMarshal.AsSpan(chatRoot.comments);

            // Offset dispersal plus update-rate flooring can leave offsets locally out of order by up
            // to 1.2 seconds, so stopping at the first comment past the current time can miss comments
            // that have already appeared. Scan to the first comment more than two seconds ahead, then
            // search backwards for the newest comment at or before the current time.
            var scanEnd = lastIndex;
            while (scanEnd + 1 < commentSpan.Length && commentSpan[scanEnd + 1].content_offset_seconds <= currentTimeSeconds + 2.0)
            {
                scanEnd++;
            }

            for (var i = scanEnd; i > lastIndex; i--)
            {
                if (commentSpan[i].content_offset_seconds <= currentTimeSeconds)
                {
                    return i;
                }
            }

            return lastIndex;
        }

        private UpdateFrame GenerateUpdateFrame(int currentTick, int sectionDefaultYPos, UpdateFrame lastUpdate = null)
        {
            var currentTimeSeconds = currentTick / (double)renderOptions.Framerate;
            var newestCommentIndex = GetNewestCommentIndex(lastUpdate?.CommentIndex ?? -1, currentTimeSeconds);
            if (lastUpdate is not null && newestCommentIndex == lastUpdate.CommentIndex)
            {
                return lastUpdate;
            }

            lastUpdate ??= new UpdateFrame
            {
                Image = new SKBitmap(renderOptions.ChatWidth, renderOptions.ChatHeight)
            };

            var commentList = lastUpdate.Comments ?? [];

            int oldCommentIndex = -1;
            if (commentList.Count > 0)
            {
                oldCommentIndex = commentList[^1].CommentIndex;
            }
            else if (newestCommentIndex > 100)
            {
                // If we are starting partially through the comment list, we don't want to needlessly render *every* comment before our starting comment.
                // Skipping to 100 comments before our starting index should be more than enough to fill the frame with previous comments
                oldCommentIndex = newestCommentIndex - 100;
            }

            if (newestCommentIndex > oldCommentIndex)
            {
                int currentIndex = oldCommentIndex + 1;

                do
                {
                    CommentSection comment = GenerateCommentSection(currentIndex, sectionDefaultYPos);
                    if (comment != null)
                    {
                        commentList.Add(comment);
                    }
                    currentIndex++;
                }
                while (newestCommentIndex >= currentIndex);
            }

            using (var frameCanvas = new SKCanvas(lastUpdate.Image))
            {
                int commentsDrawn = 0;
                int commentListIndex = commentList.Count - 1;
                int frameHeight = renderOptions.ChatHeight;
                var frameWidth = lastUpdate.Image.Width;
                frameCanvas.Clear(renderOptions.BackgroundColor);

                while (commentListIndex >= 0 && frameHeight > -renderOptions.VerticalPadding)
                {
                    var comment = commentList[commentListIndex];
                    var commentHeight = comment.Image.Info.Height;
                    frameHeight -= commentHeight + renderOptions.VerticalPadding;

                    var backgroundColor = GetMessageBackground(comment.CommentIndex, out var backgroundPaint);
                    if (backgroundColor != renderOptions.BackgroundColor)
                    {
                        frameCanvas.DrawRect(0, frameHeight - renderOptions.VerticalPadding / 2f, frameWidth, commentHeight + renderOptions.VerticalPadding, backgroundPaint);
                    }

                    frameCanvas.DrawBitmap(comment.Image.Bitmap, 0, frameHeight);

                    foreach (var (drawPoint, emote) in comment.Emotes)
                    {
                        //Only draw static emotes
                        if (emote.FrameCount == 1)
                        {
                            frameCanvas.DrawImage(emote.EmoteFrames[0], drawPoint.X, drawPoint.Y + frameHeight);
                        }
                    }
                    commentsDrawn++;
                    commentListIndex--;
                }

                int removeCount = commentList.Count - commentsDrawn;
                for (int i = 0; i < removeCount; i++)
                {
                    _sectionImageCache.Return(commentList[i].Image);
                }
                commentList.RemoveRange(0, removeCount);
            }

            lastUpdate.Comments = commentList;
            lastUpdate.CommentIndex = newestCommentIndex;
            return lastUpdate;
        }

        private SKColor GetMessageBackground(int commentIndex, [AllowNull] out SKPaint paint)
        {
            var commenter = chatRoot.comments[commentIndex].commenter;
            var isHighlighted = renderOptions.HighlightUsersArray.Length > 0
                                && (renderOptions.HighlightUsersArray.Contains(commenter.name, StringComparer.OrdinalIgnoreCase)
                                    || renderOptions.HighlightUsersArray.Contains(commenter.display_name, StringComparer.OrdinalIgnoreCase));

            if (isHighlighted)
            {
                if (renderOptions.AlternateMessageBackgrounds && commentIndex % 2 == 1)
                {
                    paint = renderOptions.AlternateBackgroundHighlightUserPaint;
                    return paint.Color;
                }

                paint = GetCachedPaint(renderOptions.HighlightUserColor);
                return renderOptions.HighlightUserColor;
            }

            if (renderOptions.AlternateMessageBackgrounds && commentIndex % 2 == 1)
            {
                paint = renderOptions.AlternateBackgroundPaint;
                return renderOptions.AlternateBackgroundColor;
            }

            paint = null;
            return renderOptions.BackgroundColor;
        }

        private CommentSection GenerateCommentSection(int commentIndex, int sectionDefaultYPos)
        {
            CommentSection newSection = new CommentSection();
            List<EmotePosition> emoteSectionList = new List<EmotePosition>();
            Comment comment = chatRoot.comments[commentIndex];
            List<SectionImage> sectionImages = [];
            Point drawPos = new Point();
            Point defaultPos = new Point();
            var highlightType = HighlightType.Unknown;
            defaultPos.X = renderOptions.SidePadding;

            if (comment.message.user_notice_params?.msg_id != null)
            {
                if (comment.message.user_notice_params.msg_id is not "highlighted-message" and not "sub" and not "resub" and not "subgift" and not "")
                {
                    _progress.LogVerbose($"{comment._id} has invalid {nameof(comment.message.user_notice_params)}: {comment.message.user_notice_params.msg_id}.");
                    return null;
                }

                if (comment.message.user_notice_params.msg_id == "highlighted-message")
                {
                    if (comment.message.fragments == null && comment.message.body != null)
                    {
                        comment.message.fragments = [new Fragment { text = comment.message.body }];
                    }

                    highlightType = HighlightType.ChannelPointHighlight;
                }
            }

            if (comment.message.fragments == null || comment.commenter == null)
            {
                _progress.LogVerbose($"{comment._id} lacks fragments and/or a commenter.");
                return null;
            }

            AddImageSection(sectionImages, ref drawPos, defaultPos);
            defaultPos.Y = sectionDefaultYPos;
            drawPos.Y = defaultPos.Y;

            if (highlightType is HighlightType.Unknown)
            {
                highlightType = HighlightIcons.GetHighlightType(comment);
            }

            if (highlightType is not HighlightType.None)
            {
                if (highlightType is not HighlightType.ChannelPointHighlight && !renderOptions.SubMessages)
                {
                    return null;
                }

                DrawAccentedMessage(comment, sectionImages, emoteSectionList, highlightType, commentIndex, ref drawPos, defaultPos);
            }
            else
            {
                DrawNonAccentedMessage(comment, sectionImages, emoteSectionList, false, commentIndex, ref drawPos, ref defaultPos);
            }

            var finalImage = CombineImages(sectionImages, highlightType, commentIndex);
            newSection.Image = finalImage;
            newSection.Emotes = emoteSectionList;
            newSection.CommentIndex = commentIndex;

            return newSection;
        }

        private SectionImage CombineImages(List<SectionImage> sectionImages, HighlightType highlightType, int commentIndex)
        {
            var finalImage = _sectionImageCache.Rent(renderOptions.ChatWidth, sectionImages.Sum(x => x.Info.Height));
            var finalBitmapInfo = finalImage.Info;
            var finalCanvas = finalImage.Canvas;

            if (highlightType is HighlightType.PayingForward or HighlightType.ChannelPointHighlight or HighlightType.WatchStreak or HighlightType.Combo)
            {
                var accentColor = highlightType is HighlightType.PayingForward
                    ? new SKColor(0xFF26262C) // AARRGGBB
                    : new SKColor(0xFF80808C); // AARRGGBB

                var paint = GetCachedPaint(accentColor);
                finalCanvas.DrawRect(renderOptions.SidePadding, 0, renderOptions.AccentStrokeWidth, finalBitmapInfo.Height, paint);
            }
            else if (highlightType is not HighlightType.None)
            {
                const int OPAQUE_THRESHOLD = 245;
                var messageBackground = GetMessageBackground(commentIndex, out _);
                if (messageBackground.Alpha < OPAQUE_THRESHOLD)
                {
                    // Draw the highlight background only if the message background is opaque enough
                    var backgroundColor = new SKColor(0x1A6B6B6E); // AARRGGBB
                    var backgroundPaint = GetCachedPaint(backgroundColor);
                    finalCanvas.DrawRect(renderOptions.SidePadding, 0, finalBitmapInfo.Width - renderOptions.SidePadding * 2, finalBitmapInfo.Height, backgroundPaint);
                }

                var accentPaint = GetCachedPaint(Purple);
                finalCanvas.DrawRect(renderOptions.SidePadding, 0, renderOptions.AccentStrokeWidth, finalBitmapInfo.Height, accentPaint);
            }

            for (var i = 0; i < sectionImages.Count; i++)
            {
                finalCanvas.DrawBitmap(sectionImages[i].Bitmap, 0, i * renderOptions.SectionHeight);
                _sectionImageCache.Return(sectionImages[i]);
            }
            sectionImages.Clear();

            finalImage.Flush();
            return finalImage;
        }

        private void DrawNonAccentedMessage(Comment comment, List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, bool highlightWords, int commentIndex, ref Point drawPos, ref Point defaultPos)
        {
            if (renderOptions.Timestamp)
            {
                DrawTimestamp(comment, sectionImages, ref drawPos, ref defaultPos);
            }
            if (renderOptions.RenderUserAvatars)
            {
                DrawAvatar(comment, sectionImages, ref drawPos);
            }
            if (renderOptions.ChatBadges)
            {
                DrawBadges(comment, sectionImages, ref drawPos);
            }
            DrawUsername(comment, sectionImages, ref drawPos, defaultPos, commentIndex: commentIndex);
            DrawMessage(comment, sectionImages, emotePositionList, highlightWords, ref drawPos, defaultPos);

            foreach (var sectionImage in sectionImages)
            {
                sectionImage.Flush();
            }
        }

        private void DrawAccentedMessage(Comment comment, List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, HighlightType highlightType, int commentIndex, ref Point drawPos, Point defaultPos)
        {
            drawPos.X += renderOptions.AccentIndentWidth;
            defaultPos.X = drawPos.X;

            var highlightIcon = highlightIcons.GetHighlightIcon(highlightType, messageFont.Color);

            Point iconPoint = new()
            {
                X = drawPos.X,
                Y = (int)((renderOptions.SectionHeight - highlightIcon.Height) / 2.0)
            };

            switch (highlightType)
            {
                case HighlightType.SubscribedTier:
                case HighlightType.SubscribedPrime:
                    DrawSubscribeMessage(comment, sectionImages, emotePositionList, commentIndex, ref drawPos, defaultPos, highlightIcon, iconPoint);
                    break;
                case HighlightType.BitBadgeTierNotification:
                    DrawBitsBadgeTierMessage(comment, sectionImages, emotePositionList, ref drawPos, defaultPos, highlightIcon, iconPoint);
                    break;
                case HighlightType.WatchStreak:
                    DrawWatchStreakMessage(comment, sectionImages, emotePositionList, commentIndex, ref drawPos, defaultPos, highlightIcon, iconPoint);
                    break;
                case HighlightType.CharityDonation:
                    DrawCharityDonationMessage(comment, sectionImages, emotePositionList, ref drawPos, defaultPos, highlightIcon, iconPoint);
                    break;
                case HighlightType.GiftedMany:
                case HighlightType.GiftedSingle:
                case HighlightType.GiftedAnonymous:
                case HighlightType.ContinuingAnonymousGift:
                    DrawGiftMessage(comment, sectionImages, emotePositionList, ref drawPos, defaultPos, highlightIcon, iconPoint);
                    break;
                case HighlightType.ChannelPointHighlight:
                    DrawNonAccentedMessage(comment, sectionImages, emotePositionList, true, commentIndex, ref drawPos, ref defaultPos);
                    break;
                case HighlightType.ContinuingGift:
                case HighlightType.PayingForward:
                case HighlightType.Raid:
                case HighlightType.Combo:
                default:
                    DrawMessage(comment, sectionImages, emotePositionList, false, ref drawPos, defaultPos);
                    break;
            }

            foreach (var sectionImage in sectionImages)
            {
                sectionImage.Flush();
            }
        }

        private void DrawSubscribeMessage(Comment comment, List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, int commentIndex, ref Point drawPos, Point defaultPos, SKImage highlightIcon, Point iconPoint)
        {
            var canvas = sectionImages[^1].Canvas;
            canvas.DrawImage(highlightIcon, iconPoint.X, iconPoint.Y);

            Point customMessagePos = drawPos;
            drawPos.X += highlightIcon.Width + renderOptions.WordSpacing;
            defaultPos.X = drawPos.X;

            DrawUsername(comment, sectionImages, ref drawPos, defaultPos, false, Purple);
            AddImageSection(sectionImages, ref drawPos, defaultPos);

            // Remove the commenter's name from the resub message
            comment.message.body = comment.message.body[(comment.commenter.display_name.Length + 1)..];
            if (comment.message.fragments[0].text.Equals(comment.commenter.display_name, StringComparison.OrdinalIgnoreCase))
            {
                // Some older chat replays separate user names into separate fragments
                comment.message.fragments.RemoveAt(0);
            }
            else
            {
                comment.message.fragments[0].text = comment.message.fragments[0].text[(comment.commenter.display_name.Length + 1)..];
            }

            var (resubMessage, customResubMessage) = HighlightIcons.SplitSubComment(comment);
            DrawMessage(resubMessage, sectionImages, emotePositionList, false, ref drawPos, defaultPos);

            // Return if there is no custom resub message to draw
            if (customResubMessage is null)
            {
                return;
            }

            AddImageSection(sectionImages, ref drawPos, defaultPos);
            drawPos = customMessagePos;
            defaultPos = customMessagePos;
            DrawNonAccentedMessage(customResubMessage, sectionImages, emotePositionList, false, commentIndex, ref drawPos, ref defaultPos);
        }

        private void DrawBitsBadgeTierMessage(Comment comment, List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, ref Point drawPos, Point defaultPos, SKImage highlightIcon, Point iconPoint)
        {
            var canvas = sectionImages[^1].Canvas;

            canvas.DrawImage(highlightIcon, iconPoint.X, iconPoint.Y);
            drawPos.X += highlightIcon.Width + renderOptions.WordSpacing;
            defaultPos.X = drawPos.X;

            if (comment.message.fragments.Count == 1)
            {
                DrawUsername(comment, sectionImages, ref drawPos, defaultPos, false, messageFont.Color);

                var bitsBadgeVersion = comment.message.user_badges.FirstOrDefault(x => x._id == "bits")?.version;
                if (bitsBadgeVersion is not null)
                {
                    comment.message.body = bitsBadgeVersion.Length > 3
                        ? $"just earned a new {bitsBadgeVersion.AsSpan(0, bitsBadgeVersion.Length - 3)}K Bits badge!"
                        : $"just earned a new {bitsBadgeVersion} Bits badge!";
                }
                else
                {
                    comment.message.body = "just earned a new Bits badge!";
                }

                comment.message.fragments[0].text = comment.message.body;
            }
            else
            {
                // This should never be possible, but just in case.
                DrawUsername(comment, sectionImages, ref drawPos, defaultPos, true, messageFont.Color);
            }

            DrawMessage(comment, sectionImages, emotePositionList, false, ref drawPos, defaultPos);
        }

        private void DrawWatchStreakMessage(Comment comment, List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, int commentIndex, ref Point drawPos, Point defaultPos, SKImage highlightIcon, Point iconPoint)
        {
            var canvas = sectionImages[^1].Canvas;
            canvas.DrawImage(highlightIcon, iconPoint.X, iconPoint.Y);

            Point customMessagePos = drawPos;
            drawPos.X += highlightIcon.Width + renderOptions.WordSpacing;
            defaultPos.X = drawPos.X;

            DrawUsername(comment, sectionImages, ref drawPos, defaultPos, false, Purple);
            AddImageSection(sectionImages, ref drawPos, defaultPos);

            // Remove the commenter's name from the watch streak message
            comment.message.body = comment.message.body[(comment.commenter.display_name.Length + 1)..];
            if (comment.message.fragments[0].text.Equals(comment.commenter.display_name, StringComparison.OrdinalIgnoreCase))
            {
                // This is necessary for sub messages. We'll keep it around just in case.
                comment.message.fragments.RemoveAt(0);
            }
            else
            {
                comment.message.fragments[0].text = comment.message.fragments[0].text[(comment.commenter.display_name.Length + 1)..];
            }

            var (streakMessage, customMessage) = HighlightIcons.SplitWatchStreakComment(comment);
            DrawMessage(streakMessage, sectionImages, emotePositionList, false, ref drawPos, defaultPos);

            // Return if there is no custom message to draw
            if (customMessage is null)
            {
                return;
            }

            AddImageSection(sectionImages, ref drawPos, defaultPos);
            drawPos = customMessagePos;
            defaultPos = customMessagePos;
            DrawNonAccentedMessage(customMessage, sectionImages, emotePositionList, false, commentIndex, ref drawPos, ref defaultPos);
        }

        private void DrawCharityDonationMessage(Comment comment, List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, ref Point drawPos, Point defaultPos, SKImage highlightIcon, Point iconPoint)
        {
            var canvas = sectionImages[^1].Canvas;
            canvas.DrawImage(highlightIcon, iconPoint.X, iconPoint.Y);

            drawPos.X += highlightIcon.Width + renderOptions.WordSpacing;
            defaultPos.X = drawPos.X;

            DrawUsername(comment, sectionImages, ref drawPos, defaultPos, false, Purple);
            AddImageSection(sectionImages, ref drawPos, defaultPos);

            // Remove the commenter's name from the charity donation message
            comment.message.body = comment.message.body[(comment.commenter.display_name.Length + 2)..];
            comment.message.fragments[0].text = comment.message.fragments[0].text[(comment.commenter.display_name.Length + 2)..];

            DrawMessage(comment, sectionImages, emotePositionList, false, ref drawPos, defaultPos);
        }

        private void DrawGiftMessage(Comment comment, List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, ref Point drawPos, Point defaultPos, SKImage highlightIcon, Point iconPoint)
        {
            var canvas = sectionImages[^1].Canvas;

            canvas.DrawImage(highlightIcon, iconPoint.X, iconPoint.Y);
            drawPos.X += highlightIcon.Width + renderOptions.AccentIndentWidth - renderOptions.AccentStrokeWidth;
            defaultPos.X = drawPos.X;
            DrawMessage(comment, sectionImages, emotePositionList, false, ref drawPos, defaultPos);
        }

        private static readonly SearchValues<char> WhiteSpaceChars = SearchValues.Create("\t\n\v\f\r\u0020\u0085\u00a0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200a\u2028\u2029\u202f\u205f\u3000");

        private void DrawMessage(Comment comment, List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, bool highlightWords, ref Point drawPos, Point defaultPos)
        {
            int bitsCount = comment.message.bits_spent;
            foreach (var fragment in comment.message.fragments)
            {
                if (fragment.emoticon != null)
                {
                    DrawFirstPartyEmote(sectionImages, emotePositionList, ref drawPos, defaultPos, fragment, highlightWords);
                    continue;
                }

                // Either text or third party emote
                var fragmentSpan = fragment.text.AsSpan();
                var spaceCount = fragmentSpan.CountAny(WhiteSpaceChars);

                var fragmentParts = ArrayPool<Range>.Shared.Rent(spaceCount + 1);
                try
                {
                    var written = SwapRightToLeft(fragmentSpan.SplitAny(WhiteSpaceChars), fragmentParts);
                    foreach (var range in fragmentParts.Take(written))
                    {
                        var fragmentPart = fragmentSpan[range];
                        DrawFragmentPart(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, fragmentPart, highlightWords);
                    }
                }
                finally
                {
                    ArrayPool<Range>.Shared.Return(fragmentParts);
                }
            }
        }

        private void DrawFragmentPart(List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, ref Point drawPos, Point defaultPos, int bitsCount, ReadOnlySpan<char> fragmentPart, bool highlightWords, bool skipThird = false, bool skipEmoji = false, bool skipNonFont = false)
        {
            var thirdLookup = _emoteThirdCache.GetAlternateLookup<ReadOnlySpan<char>>();
            if (!skipThird && thirdLookup.TryGetValue(fragmentPart, out var emote))
            {
                DrawThirdPartyEmote(sectionImages, emotePositionList, ref drawPos, defaultPos, emote, highlightWords);
            }
            else if (bitsCount > 0 && TryDrawBits(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, fragmentPart))
            {
                // no-op
            }
            else if (!skipEmoji && renderOptions.EmojiVendor != EmojiVendor.None && ContainsEmoji(fragmentPart, out var firstEmoji))
            {
                DrawEmojiMessage(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, fragmentPart, highlightWords, firstEmoji);
            }
            else if (!skipNonFont && (!messageFont.ContainsGlyphs(fragmentPart) || fragmentPart.LengthInTextElements() < fragmentPart.Length))
            {
                DrawNonFontMessage(sectionImages, ref drawPos, defaultPos, fragmentPart, highlightWords);
            }
            else
            {
                DrawText(fragmentPart, messageFont, true, sectionImages, ref drawPos, defaultPos, highlightWords);
            }
        }

        private void DrawThirdPartyEmote(List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, ref Point drawPos, Point defaultPos, TwitchEmote twitchEmote, bool highlightWords)
        {
            SKImageInfo emoteInfo = twitchEmote.Info;
            Point emotePoint = new Point();
            if (!twitchEmote.IsZeroWidth)
            {
                if (drawPos.X + emoteInfo.Width > renderOptions.ChatWidth - renderOptions.SidePadding * 2)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }

                if (highlightWords)
                {
                    var canvas = sectionImages[^1].Canvas;
                    var paint = GetCachedPaint(Purple);
                    canvas.DrawRect(drawPos.X, 0, emoteInfo.Width + renderOptions.EmoteSpacing, renderOptions.SectionHeight, paint);
                }

                emotePoint.X = drawPos.X;
                drawPos.X += emoteInfo.Width + renderOptions.EmoteSpacing;
            }
            else
            {
                emotePoint.X = drawPos.X - renderOptions.EmoteSpacing - emoteInfo.Width;
            }
            emotePoint.Y = (int)(sectionImages.Sum(x => x.Info.Height) - renderOptions.SectionHeight + ((renderOptions.SectionHeight - emoteInfo.Height) / 2.0));
            emotePositionList.Add(new EmotePosition(emotePoint, twitchEmote));
        }

        private static readonly SearchValues<char> EmojiExcludeChars = SearchValues.Create('\uFE0F');

        private bool ContainsEmoji(ReadOnlySpan<char> text, out int firstEmoji)
        {
            var emojiLookup = _emojiCache.GetAlternateLookup<ReadOnlySpan<char>>();
            Span<char> stackSpace = stackalloc char[16];

            var fragmentSlice = text;
            var index = 0;
            while (true)
            {
                var elementLength = StringInfo.GetNextTextElementLength(fragmentSlice);
                if (elementLength == 0)
                {
                    firstEmoji = -1;
                    return false;
                }

                if (elementLength == 1 && char.IsAscii(fragmentSlice[0]))
                {
                    fragmentSlice = fragmentSlice[elementLength..];
                    index += elementLength;
                    continue;
                }

                var textElement = fragmentSlice[..elementLength];
                foreach (var range in textElement.Split(ZERO_WIDTH_JOINER)) // Will return textElement once when no ZWJ
                {
                    var subEmoji = textElement[range];

                    var lookupKey = subEmoji.Length <= stackSpace.Length ? stackSpace : new char[subEmoji.Length];
                    var written = subEmoji.CopyToExcept(stackSpace, EmojiExcludeChars);
                    if (emojiLookup.ContainsKey(lookupKey[..written]))
                    {
                        firstEmoji = index;
                        return true;
                    }
                }

                fragmentSlice = fragmentSlice[elementLength..];
                index += elementLength;
            }
        }

        private void DrawEmojiMessage(List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, ref Point drawPos, Point defaultPos, int bitsCount, ReadOnlySpan<char> fragment, bool highlightWords, int firstEmoji = -1)
        {
            var emojiMatches = new List<string>();
            var emojiLookup = _emojiCache.GetAlternateLookup<ReadOnlySpan<char>>();
            Span<char> stackSpace = stackalloc char[16];

            var fragmentSlice = fragment;
            var nonEmojiStart = 0;
            var nonEmojiLen = 0;
            if (firstEmoji > 0)
            {
                nonEmojiLen = firstEmoji;
                fragmentSlice = fragmentSlice[firstEmoji..];
            }

            while (true)
            {
                var elementLength = StringInfo.GetNextTextElementLength(fragmentSlice);
                if (elementLength == 0)
                {
                    break;
                }

                if (elementLength == 1 && char.IsAscii(fragmentSlice[0]))
                {
                    nonEmojiLen += elementLength;
                    fragmentSlice = fragmentSlice[elementLength..];
                    continue;
                }

                var textElement = fragmentSlice[..elementLength];
                fragmentSlice = fragmentSlice[elementLength..];

                var lookupKey = elementLength <= stackSpace.Length ? stackSpace : new char[elementLength];
                var written = textElement.CopyToExcept(stackSpace, EmojiExcludeChars);
                if (!emojiLookup.TryGetValue(lookupKey[..written], out var emojiImage))
                {
                    emojiImage = SplitZwjEmoji(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, highlightWords, textElement, emojiLookup);
                    emojiImage ??= LookupEmojiSlow(textElement, emojiMatches);
                    if (emojiImage is null)
                    {
                        nonEmojiLen += elementLength;
                        continue;
                    }
                }

                if (nonEmojiLen > 0)
                {
                    DrawFragmentPart(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, fragment.Slice(nonEmojiStart, nonEmojiLen), highlightWords, true, true);
                    nonEmojiStart += nonEmojiLen;
                    nonEmojiLen = 0;
                }

                SKImageInfo emojiImageInfo = emojiImage.Info;
                if (drawPos.X + emojiImageInfo.Width > renderOptions.ChatWidth - renderOptions.SidePadding * 2)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }

                Point emotePoint = new Point
                {
                    X = drawPos.X + (int)Math.Ceiling(renderOptions.EmoteSpacing / 2d), // emotePoint.X halfway through emote padding
                    Y = (int)((renderOptions.SectionHeight - emojiImageInfo.Height) / 2.0)
                };

                var canvas = sectionImages[^1].Canvas;
                if (highlightWords)
                {
                    var paint = GetCachedPaint(Purple);
                    canvas.DrawRect((int)(emotePoint.X - renderOptions.EmoteSpacing / 2d), 0, emojiImageInfo.Width + renderOptions.EmoteSpacing, renderOptions.SectionHeight, paint);
                }

                canvas.DrawImage(emojiImage, emotePoint.X, emotePoint.Y);
                nonEmojiStart += elementLength;

                drawPos.X += emojiImageInfo.Width + renderOptions.EmoteSpacing;
            }

            if (nonEmojiLen > 0)
            {
                DrawFragmentPart(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, fragment.Slice(nonEmojiStart, nonEmojiLen), highlightWords, true, true);
            }
        }

        [return: MaybeNull]
        private SKImage SplitZwjEmoji(List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, ref Point drawPos, Point defaultPos, int bitsCount, bool highlightWords, ReadOnlySpan<char> textElement,
            Dictionary<string, SKImage>.AlternateLookup<ReadOnlySpan<char>> emojiLookup)
        {
            Span<char> stackSpace = stackalloc char[16];

            if (!textElement.Contains(ZERO_WIDTH_JOINER))
            {
                return null;
            }

            ReadOnlySpan<char> subEmoji = default;
            foreach (var range in textElement.Split(ZERO_WIDTH_JOINER))
            {
                if (!subEmoji.IsEmpty)
                {
                    DrawEmojiMessage(sectionImages, emotePositionList, ref drawPos, defaultPos, bitsCount, subEmoji, highlightWords);
                }

                subEmoji = textElement[range];
            }

            var lookupKey = subEmoji.Length <= stackSpace.Length ? stackSpace : new char[subEmoji.Length];
            var written = subEmoji.CopyToExcept(lookupKey, EmojiExcludeChars);
            return emojiLookup.TryGetValue(lookupKey[..written], out var emojiImage)
                ? emojiImage
                : null;
        }

        [return: MaybeNull]
        private SKImage LookupEmojiSlow(ReadOnlySpan<char> textElement, List<string> emojiMatches)
        {
            var firstCodepoint = textElement.Length > 1 && char.IsHighSurrogate(textElement[0]) && char.IsLowSurrogate(textElement[1])
                ? char.ConvertToUtf32(textElement[0], textElement[1])
                : textElement[0];

            emojiMatches.Clear();
            if (AllEmojiSequences.TryGetValue(firstCodepoint, out var matches))
            {
                foreach (var emoji in matches)
                {
                    if (textElement.StartsWith(emoji))
                    {
                        emojiMatches.Add(emoji);
                    }
                }
            }

            var emojiMatchesCount = emojiMatches.Count;
            if (emojiMatchesCount == 0)
            {
                return null;
            }

            var selectedEmoji = emojiMatches.MaxBy(x => x.Length);
            return _emojiCache[selectedEmoji];
        }

        private void DrawNonFontMessage(List<SectionImage> sectionImages, ref Point drawPos, Point defaultPos, ReadOnlySpan<char> fragment, bool highlightWords)
        {
            fragment = fragment.Trim('\uFE0F');

            if (BlockArtRegex.IsMatch(fragment))
            {
                // Very rough estimation of width of block art
                var textWidth = (int)(fragment.Length * renderOptions.BlockArtCharWidth);
                if (renderOptions.BlockArtPreWrap && drawPos.X + textWidth > renderOptions.BlockArtPreWrapWidth)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }
            }

            // We cannot draw nonFont chars individually or Arabic script looks improper https://github.com/lay295/TwitchDownloader/issues/484
            // The fragment has either surrogate pairs or characters not in the messageFont
            var inFontBuffer = new StringBuilder();
            var nonFontBuffer = new StringBuilder();
            for (var j = 0; j < fragment.Length; j++)
            {
                if (char.IsHighSurrogate(fragment[j]) && j + 1 < fragment.Length && char.IsLowSurrogate(fragment[j + 1]))
                {
                    if (inFontBuffer.Length > 0)
                    {
                        DrawText(inFontBuffer.ToString(), messageFont, false, sectionImages, ref drawPos, defaultPos, highlightWords);
                        inFontBuffer.Clear();
                    }
                    if (nonFontBuffer.Length > 0)
                    {
                        var nonFontFallbackFont = GetFallbackFont(nonFontBuffer[0]);
                        nonFontFallbackFont.Color = renderOptions.MessageColor;
                        DrawText(nonFontBuffer.ToString(), nonFontFallbackFont, false, sectionImages, ref drawPos, defaultPos, highlightWords);
                        nonFontBuffer.Clear();
                    }

                    var utf32Char = char.ConvertToUtf32(fragment[j], fragment[j + 1]);
                    //Don't attempt to draw U+E0000
                    if (utf32Char != 0xE0000)
                    {
                        var highSurrogateFallbackFont = GetFallbackFont(utf32Char);
                        highSurrogateFallbackFont.Color = renderOptions.MessageColor;
                        DrawText(fragment.Slice(j, 2), highSurrogateFallbackFont, false, sectionImages, ref drawPos, defaultPos, highlightWords);
                    }
                    j++;
                }
                else if (!messageFont.ContainsGlyphs(fragment.Slice(j, 1)) || fragment.Slice(j, 1).LengthInTextElements() == 0)
                {
                    if (inFontBuffer.Length > 0)
                    {
                        DrawText(inFontBuffer.ToString(), messageFont, false, sectionImages, ref drawPos, defaultPos, highlightWords);
                        inFontBuffer.Clear();
                    }

                    nonFontBuffer.Append(fragment[j]);
                }
                else
                {
                    if (nonFontBuffer.Length > 0)
                    {
                        var fallbackFont = GetFallbackFont(nonFontBuffer[0]);
                        fallbackFont.Color = renderOptions.MessageColor;
                        DrawText(nonFontBuffer.ToString(), fallbackFont, false, sectionImages, ref drawPos, defaultPos, highlightWords);
                        nonFontBuffer.Clear();
                    }

                    inFontBuffer.Append(fragment[j]);
                }
            }

            // Only one or the other should occur
            if (nonFontBuffer.Length > 0)
            {
                var fallbackFont = GetFallbackFont(nonFontBuffer[0]);
                fallbackFont.Color = renderOptions.MessageColor;
                DrawText(nonFontBuffer.ToString(), fallbackFont, true, sectionImages, ref drawPos, defaultPos, highlightWords);
                nonFontBuffer.Clear();
            }
            if (inFontBuffer.Length > 0)
            {
                DrawText(inFontBuffer.ToString(), messageFont, true, sectionImages, ref drawPos, defaultPos, highlightWords);
                inFontBuffer.Clear();
            }
        }

        private bool TryDrawBits(List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, ref Point drawPos, Point defaultPos, int bitsCount, ReadOnlySpan<char> fragmentString)
        {
            if (bitsCount < 1)
            {
                Debug.Fail($"Check {nameof(bitsCount)} before calling TryDrawBits to avoid setting up the method call.");
                return false;
            }

            var bitsIndex = fragmentString.IndexOfAny(DigitChars);
            var cheermoteLookup = _cheermoteCache.GetAlternateLookup<ReadOnlySpan<char>>();
            if (bitsIndex > 0 && int.TryParse(fragmentString[bitsIndex..], out var bitsAmount) && cheermoteLookup.TryGetValue(fragmentString[..bitsIndex], out var currentCheerEmote))
            {
                var tieredEmote = currentCheerEmote.GetTier(bitsAmount).Value;
                var emoteImageInfo = tieredEmote.Info;
                if (drawPos.X + emoteImageInfo.Width > renderOptions.ChatWidth - renderOptions.SidePadding * 2)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }

                var emotePoint = new Point
                {
                    X = drawPos.X,
                    Y = (int)(sectionImages.Sum(x => x.Info.Height) - renderOptions.SectionHeight + ((renderOptions.SectionHeight - emoteImageInfo.Height) / 2.0))
                };
                emotePositionList.Add(new EmotePosition(emotePoint, tieredEmote));
                drawPos.X += emoteImageInfo.Width + renderOptions.EmoteSpacing;

                return true;
            }

            return false;
        }

        private void DrawFirstPartyEmote(List<SectionImage> sectionImages, List<EmotePosition> emotePositionList, ref Point drawPos, Point defaultPos, Fragment fragment, bool highlightWords)
        {
            // First party emote
            var emoteLookup = _emoteCache.GetAlternateLookup<ReadOnlySpan<char>>();
            if (emoteLookup.TryGetValue(fragment.emoticon.emoticon_id, out var emote))
            {
                SKImageInfo emoteInfo = emote.Info;
                if (drawPos.X + emoteInfo.Width > renderOptions.ChatWidth - renderOptions.SidePadding * 2)
                {
                    AddImageSection(sectionImages, ref drawPos, defaultPos);
                }
                Point emotePoint = new Point
                {
                    X = drawPos.X,
                    Y = (int)(sectionImages.Sum(x => x.Info.Height) - renderOptions.SectionHeight + ((renderOptions.SectionHeight - emoteInfo.Height) / 2.0))
                };

                if (highlightWords)
                {
                    var canvas = sectionImages[^1].Canvas;
                    var paint = GetCachedPaint(Purple);
                    canvas.DrawRect(drawPos.X, 0, emoteInfo.Width + renderOptions.EmoteSpacing, renderOptions.SectionHeight, paint);
                }

                emotePositionList.Add(new EmotePosition(emotePoint, emote));
                drawPos.X += emoteInfo.Width + renderOptions.EmoteSpacing;
            }
            else
            {
                // Probably an old emote that was removed
                DrawText(fragment.text, messageFont, true, sectionImages, ref drawPos, defaultPos, highlightWords);
            }
        }

        private static readonly SearchValues<char> DrawTextDelimiters = SearchValues.Create("?-");

        private void DrawText(ReadOnlySpan<char> drawText, SKPaint textFont, bool padding, List<SectionImage> sectionImages, ref Point drawPos, Point defaultPos, bool highlightWords, bool noWrap = false)
        {
            bool isRtl = IsRightToLeft(drawText);
            float textWidth = MeasureText(drawText, textFont, isRtl);
            int effectiveChatWidth = renderOptions.ChatWidth - renderOptions.SidePadding - defaultPos.X;

            while (!noWrap && textWidth > effectiveChatWidth)
            {
                var newDrawText = SubstringToTextWidth(drawText, textFont, effectiveChatWidth, isRtl, DrawTextDelimiters);
                var overrideWrap = false;

                if (newDrawText.Length == 0)
                {
                    // When chat width is small enough and font size is big enough, 1 character can be wider than effectiveChatWidth.
                    overrideWrap = true;
                    newDrawText = drawText[..StringInfo.GetNextTextElementLength(drawText)];
                }

                DrawText(newDrawText, textFont, padding, sectionImages, ref drawPos, defaultPos, highlightWords, overrideWrap);

                drawText = drawText[newDrawText.Length..];
                textWidth = MeasureText(drawText, textFont, isRtl);
            }
            if (drawPos.X + textWidth > renderOptions.ChatWidth - renderOptions.SidePadding * 2)
            {
                AddImageSection(sectionImages, ref drawPos, defaultPos);
            }

            var canvas = sectionImages[^1].Canvas;
            if (highlightWords)
            {
                var paint = GetCachedPaint(Purple);
                canvas.DrawRect(drawPos.X, 0, textWidth + (padding ? renderOptions.WordSpacing : 0), renderOptions.SectionHeight, paint);
            }

            if (renderOptions.Outline)
            {
                using var outlinePath = isRtl
                    ? textFont.GetShapedTextPath(drawText, drawPos.X, drawPos.Y)
                    : textFont.GetTextPath(drawText, drawPos.X, drawPos.Y);

                canvas.DrawPath(outlinePath, outlinePaint);
            }

            if (RtlRegex.IsMatch(drawText))
            {
                canvas.DrawShapedText(drawText.ToString(), drawPos.X, drawPos.Y, textFont);
            }
            else
            {
                canvas.DrawText(drawText, drawPos.X, drawPos.Y, textFont);
            }

            drawPos.X += (int)Math.Floor(textWidth + (padding ? renderOptions.WordSpacing : 0));
        }

        /// <summary>
        /// Produces a <see langword="string"/> less than or equal to <paramref name="maxWidth"/> when drawn with <paramref name="textFont"/> OR substringed to the last index of any character in <paramref name="delimiters"/>.
        /// </summary>
        /// <returns>A shortened in visual width or delimited <see langword="string"/>, whichever comes first.</returns>
        private static ReadOnlySpan<char> SubstringToTextWidth(ReadOnlySpan<char> text, SKPaint textFont, int maxWidth, bool isRtl, SearchValues<char> delimiters)
        {
            // If we are dealing with non-RTL and don't have any delimiters then SKPaint.BreakText is over 9x faster
            if (!isRtl && text.IndexOfAny(delimiters) == -1)
            {
                return SubstringToTextWidth(text, textFont, maxWidth);
            }

            using var shaper = isRtl
                ? new SKShaper(textFont.Typeface)
                : null;

            // Input text was already less than max width
            if (MeasureText(text, textFont, isRtl, shaper) <= maxWidth)
            {
                return text;
            }

            // Cut in half until <= width
            var length = text.Length;
            do
            {
                length /= 2;
            }
            while (MeasureText(text[..length], textFont, isRtl, shaper) > maxWidth);

            // Add chars until greater than width, then remove the last
            do
            {
                length++;
            } while (MeasureText(text[..length], textFont, isRtl, shaper) < maxWidth);
            text = text[..(length - 1)];

            // Cut at the last delimiter character if applicable
            var delimiterIndex = text.LastIndexOfAny(delimiters);
            if (delimiterIndex != -1)
            {
                return text[..(delimiterIndex + 1)];
            }

            return text;
        }

        /// <summary>
        /// Produces a <see cref="ReadOnlySpan{T}"/> less than or equal to <paramref name="maxWidth"/> when drawn with <paramref name="textFont"/>
        /// </summary>
        /// <returns>A shortened in visual width <see cref="ReadOnlySpan{T}"/>.</returns>
        /// <remarks>This is not compatible with text that needs to be shaped.</remarks>
        private static ReadOnlySpan<char> SubstringToTextWidth(ReadOnlySpan<char> text, SKPaint textFont, int maxWidth)
        {
            var length = (int)textFont.BreakText(text, maxWidth);
            return text[..length];
        }

        private static float MeasureText(ReadOnlySpan<char> text, SKPaint textFont, bool? isRtl = null, SKShaper shaper = null)
        {
            isRtl ??= IsRightToLeft(text);

            if (isRtl == false)
            {
                return textFont.MeasureText(text);
            }

            if (shaper == null)
            {
                return MeasureRtlText(text, textFont);
            }

            return MeasureRtlText(text, textFont, shaper);
        }

        private static float MeasureRtlText(ReadOnlySpan<char> rtlText, SKPaint textFont)
        {
            using var shaper = new SKShaper(textFont.Typeface);
            return MeasureRtlText(rtlText, textFont, shaper);
        }

        private static float MeasureRtlText(ReadOnlySpan<char> rtlText, SKPaint textFont, SKShaper shaper)
        {
            using var buffer = new HarfBuzzSharp.Buffer();
            buffer.Add(rtlText, textFont.TextEncoding);
            SKShaper.Result measure = shaper.Shape(buffer, textFont);
            return measure.Width;
        }

        private void DrawUsername(Comment comment, List<SectionImage> sectionImages, ref Point drawPos, Point defaultPos, bool appendColon = true, SKColor? colorOverride = null, int commentIndex = 0)
        {
            var userColor = colorOverride ?? (comment.message.user_color is not null
                ? SKColor.Parse(comment.message.user_color)
                : DefaultUsernameColors[Math.Abs(comment.commenter.display_name.GetHashCode()) % DefaultUsernameColors.Length]);

            if (colorOverride is null && renderOptions.AdjustUsernameVisibility)
            {
                var backgroundColor = GetMessageBackground(commentIndex, out _);
                userColor = AdjustUsernameVisibility(userColor, backgroundColor);
            }

            var userPaint = comment.commenter.display_name.Any(IsNotAscii)
                ? GetFallbackFont(comment.commenter.display_name.First(IsNotAscii))
                : nameFont;

            userPaint.Color = userColor;
            var userName = appendColon
                ? comment.commenter.display_name + ":"
                : comment.commenter.display_name;

            // Center the username using its own font metrics so a scaled username font stays vertically
            // centered within the section, then restore drawPos.Y for the message text that follows.
            int savedY = drawPos.Y;
            drawPos.Y = _usernameCenteredY;
            DrawText(userName, userPaint, true, sectionImages, ref drawPos, defaultPos, false);
            drawPos.Y = savedY;
        }

        private SKColor AdjustUsernameVisibility(SKColor userColor, SKColor backgroundColor)
        {
            const byte OPAQUE_THRESHOLD = byte.MaxValue / 2;
            if (!renderOptions.Outline && backgroundColor.Alpha < OPAQUE_THRESHOLD)
            {
                // Background lightness cannot be truly known.
                return userColor;
            }

            var newUserColor = AdjustColorVisibility(userColor, renderOptions.Outline ? outlinePaint.Color : backgroundColor);

            return renderOptions.Outline || backgroundColor.Alpha == byte.MaxValue
                ? newUserColor
                : userColor.Lerp(newUserColor, (float)backgroundColor.Alpha / byte.MaxValue);
        }

        private static SKColor AdjustColorVisibility(SKColor foreground, SKColor background)
        {
            background.ToHsl(out var bgHue, out var bgSat, out _);
            foreground.ToHsl(out var fgHue, out var fgSat, out var fgLight);

            // Adjust lightness
            if (background.RelativeLuminance() > 0.5)
            {
                // Bright background
                if (fgLight > 60)
                {
                    fgLight = 60;
                }

                if (bgSat <= 28)
                {
                    fgHue = fgHue switch
                    {
                        > 48 and < 90 => AdjustHue(fgHue, 48, 90), // Yellow-Lime
                        > 164 and < 186 => AdjustHue(fgHue, 164, 186), // Turquoise
                        _ => fgHue
                    };
                }
            }
            else
            {
                // Dark background
                if (fgLight < 40)
                {
                    fgLight = 40;
                }

                if (bgSat <= 28)
                {
                    fgHue = fgHue switch
                    {
                        > 224 and < 263 => AdjustHue(fgHue, 224, 264), // Blue-Purple
                        _ => fgHue
                    };
                }
            }

            // Adjust hue on colored backgrounds
            if (bgSat > 28 && fgSat > 28)
            {
                const float HUE_WIDTH = 360;
                const int ADJUST_THRESHOLD = 35;
                Debug.Assert(ADJUST_THRESHOLD < HUE_WIDTH / 2);

                // Compute computer lower and higher hue diff to ensure we wrap around for reds
                //       hue:   [||||||||||]
                //   not red:   [    ^     ]    ^
                // upper red: ^ [        ^ ]
                // lower red:   [^         ]^
                var hueDiff1 = fgHue - (bgHue > HUE_WIDTH / 2 ? bgHue - HUE_WIDTH : bgHue);
                var hueDiff2 = fgHue - (bgHue > HUE_WIDTH / 2 ? bgHue : bgHue + HUE_WIDTH);

                // Take smallest diff, or skip if both are >ADJUST_THRESHOLD
                float hueDiff;
                if (Math.Abs(hueDiff1) <= ADJUST_THRESHOLD) hueDiff = hueDiff1;
                else if (Math.Abs(hueDiff2) <= ADJUST_THRESHOLD) hueDiff = hueDiff2;
                else goto SkipHueAdjust;

                var diffSign = hueDiff < 0 ? -1 : 1; // Math.Sign returns 1, -1, or 0. We only want 1 or -1.
                fgHue = bgHue + ADJUST_THRESHOLD * diffSign;

                if (fgHue < 0) fgHue += HUE_WIDTH;
                fgHue %= HUE_WIDTH;

                SkipHueAdjust: ;
            }

            return SKColor.FromHsl(fgHue, Math.Min(fgSat, 90), fgLight);

            static float AdjustHue(float hue, float lowerClamp, float upperClamp)
            {
                var midpoint = (upperClamp + lowerClamp) / 2;
                return hue >= midpoint ? upperClamp : lowerClamp;
            }
        }

        private void DrawAvatar(Comment comment, List<SectionImage> sectionImages, ref Point drawPos)
        {
            var avatarUrl = comment.commenter.logo;

            if (string.IsNullOrWhiteSpace(avatarUrl) || !_avatarCache.TryGetValue(avatarUrl, out var avatarImage))
            {
                avatarUrl = DefaultAvatarUrls[Math.Abs(comment.commenter.display_name.GetHashCode()) % DefaultAvatarUrls.Length];
                if (!_avatarCache.TryGetValue(avatarUrl, out avatarImage))
                {
                    return;
                }
            }

            var canvas = sectionImages[^1].Canvas;

            var avatarY = (float)((renderOptions.SectionHeight - avatarImage.Height) / 2.0);
            canvas.DrawImage(avatarImage, drawPos.X, avatarY);
            drawPos.X += avatarImage.Width + renderOptions.WordSpacing;
        }

        private void DrawBadges(Comment comment, List<SectionImage> sectionImages, ref Point drawPos)
        {
            var canvas = sectionImages[^1].Canvas;
            var badgeImages = ParseCommentBadges(comment);
            foreach (var (badgeImage, badgeType) in badgeImages)
            {
                //Don't render filtered out badges
                if ((renderOptions.ChatBadgeMask & badgeType) != 0)
                    continue;

                float badgeY = (float)((renderOptions.SectionHeight - badgeImage.Height) / 2.0);
                canvas.DrawImage(badgeImage, drawPos.X, badgeY);
                drawPos.X += badgeImage.Width + renderOptions.WordSpacing / 2;
            }
        }

        private List<(SKImage, ChatBadgeType)> ParseCommentBadges(Comment comment)
        {
            var returnList = new List<(SKImage, ChatBadgeType)>();

            if (comment.message.user_badges == null)
                return returnList;

            foreach (var badge in comment.message.user_badges)
            {
                var id = badge._id;
                var version = badge.version;

                if (!_badgeCache.TryGetValue(id, out var cachedBadge))
                    continue;

                if (!cachedBadge.Versions.TryGetValue(version, out var badgeBitmap))
                    continue;

                returnList.Add((badgeBitmap, cachedBadge.Type));
            }

            return returnList;
        }

        private void DrawTimestamp(Comment comment, List<SectionImage> sectionImages, ref Point drawPos, ref Point defaultPos)
        {
            var canvas = sectionImages[^1].Canvas;
            var timestamp = new TimeSpan(0, 0, (int)comment.content_offset_seconds);

            const int MAX_TIMESTAMP_LENGTH = 8; // 48:00:00
            Span<char> timestampStackSpace = stackalloc char[MAX_TIMESTAMP_LENGTH];
            ReadOnlySpan<char> formattedTimestamp = timestamp.Ticks switch
            {
                >= 24 * TimeSpan.TicksPerHour => TimeSpanHFormat.ReusableInstance.Format(@"HH\:mm\:ss", timestamp),
                >= 1 * TimeSpan.TicksPerHour => timestamp.TryFormat(timestampStackSpace, out var charsWritten, @"h\:mm\:ss")
                    ? timestampStackSpace[..charsWritten]
                    : timestamp.ToString(@"h\:mm\:ss"),
                _ => timestamp.TryFormat(timestampStackSpace, out var charsWritten, @"m\:ss")
                    ? timestampStackSpace[..charsWritten]
                    : timestamp.ToString(@"m\:ss")
            };

            if (renderOptions.Outline)
            {
                using var outlinePath = messageFont.GetTextPath(formattedTimestamp, drawPos.X, drawPos.Y);
                canvas.DrawPath(outlinePath, outlinePaint);
            }

            canvas.DrawText(formattedTimestamp, drawPos.X, drawPos.Y, messageFont);

            // We use pre-defined widths so all timestamps have the same defaultPos regardless of individual character width
            var textWidth = timestamp.Ticks switch
            {
                >= 10 * TimeSpan.TicksPerHour => renderOptions.TimestampWidths[3],
                >= 1 * TimeSpan.TicksPerHour => renderOptions.TimestampWidths[2],
                >= 10 * TimeSpan.TicksPerMinute => renderOptions.TimestampWidths[1],
                _ => renderOptions.TimestampWidths[0]
            };
            drawPos.X += textWidth + renderOptions.WordSpacing * 2;
            defaultPos.X = drawPos.X;
        }

        private void AddImageSection(List<SectionImage> sectionImages, ref Point drawPos, Point defaultPos)
        {
            drawPos.X = defaultPos.X;
            drawPos.Y = defaultPos.Y;

            sectionImages.Add(_sectionImageCache.Rent(renderOptions.ChatWidth, renderOptions.SectionHeight));
        }

        /// <summary>
        /// Fetches the emotes/badges/bits/emojis needed to render scaled to 2x
        /// </summary>
        /// <remarks>chatRoot.embeddedData will be empty after calling this to save on memory!</remarks>
        private async Task FetchScaledImages(CancellationToken cancellationToken)
        {
            var badgeTask = GetScaledBadges(cancellationToken);
            var emoteTask = GetScaledEmotes(cancellationToken);
            var emoteThirdTask = GetScaledThirdEmotes(cancellationToken);
            var cheerTask = GetScaledBits(cancellationToken);
            var emojiTask = GetScaledEmojis(cancellationToken);
            var avatarTask = renderOptions.RenderUserAvatars ? GetScaledAvatars(cancellationToken) : Task.FromResult(new Dictionary<string, SKImage>());

            await Task.WhenAll(badgeTask, emoteTask, emoteThirdTask, cheerTask, emojiTask, avatarTask);

            // Clear chatRoot.embeddedData and manually call GC to save some memory
            chatRoot.embeddedData = null;
            GC.Collect();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            _badgeCache.AddRange(badgeTask.Result, x => x.Name, x => x);
            _emoteCache.AddRange(emoteTask.Result, x => x.Name, x => x);
            _emoteThirdCache.AddRange(emoteThirdTask.Result, x => x.Name, x => x);
            _cheermoteCache.AddRange(cheerTask.Result, x => x.prefix, x => x);
            _emojiCache.AddRange(emojiTask.Result);
            _avatarCache.AddRange(avatarTask.Result);
        }

        private async Task<List<ChatBadge>> GetScaledBadges(CancellationToken cancellationToken)
        {
            // Do not fetch if badges are disabled
            if (!renderOptions.ChatBadges)
            {
                return [];
            }

            var badgeTask = await TwitchHelper.GetChatBadges(chatRoot.comments, chatRoot.streamer.id, _cacheDir, _progress, chatRoot.embeddedData, renderOptions.Offline, cancellationToken);

            var newHeight = (int)Math.Round(36 * renderOptions.ReferenceScale * renderOptions.BadgeScale);
            var snapThreshold = (int)Math.Round(1 * renderOptions.ReferenceScale);
            foreach (var badge in badgeTask)
            {
                cancellationToken.ThrowIfCancellationRequested();

                badge.SnapResize(newHeight, snapThreshold, snapThreshold);
            }

            return badgeTask;
        }

        private async Task<List<TwitchEmote>> GetScaledEmotes(CancellationToken cancellationToken)
        {
            var emoteTask = await TwitchHelper.GetEmotes(chatRoot.comments, _cacheDir, _progress, chatRoot.embeddedData, renderOptions.Offline, cancellationToken);

            var snapThreshold = (int)Math.Round(4 * renderOptions.ReferenceScale);
            foreach (var emote in emoteTask)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var newScale = 2.0 / emote.ImageScale * renderOptions.ReferenceScale * renderOptions.EmoteScale;
                emote.SnapScale(newScale, snapThreshold, snapThreshold);
            }

            return emoteTask;
        }

        private async Task<List<TwitchEmote>> GetScaledThirdEmotes(CancellationToken cancellationToken)
        {
            var emoteThirdTask = await TwitchHelper.GetThirdPartyEmotes(chatRoot.comments, chatRoot.streamer.id, _cacheDir, _progress, chatRoot.embeddedData, renderOptions.BttvEmotes, renderOptions.FfzEmotes,
                renderOptions.StvEmotes, renderOptions.AllowUnlistedEmotes, renderOptions.Offline, cancellationToken);

            var snapThreshold = (int)Math.Round(4 * renderOptions.ReferenceScale);
            foreach (var emote in emoteThirdTask)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var newScale = 2.0 / emote.ImageScale * renderOptions.ReferenceScale * renderOptions.EmoteScale;
                emote.SnapScale(newScale, snapThreshold, snapThreshold);
            }

            return emoteThirdTask;
        }

        private async Task<List<CheerEmote>> GetScaledBits(CancellationToken cancellationToken)
        {
            var cheerTask = await TwitchHelper.GetBits(chatRoot.comments, _cacheDir, chatRoot.streamer.id.ToString(), _progress, chatRoot.embeddedData, renderOptions.Offline, cancellationToken);

            var snapThreshold = (int)Math.Round(4 * renderOptions.ReferenceScale);
            foreach (var cheer in cheerTask)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var imageScale = cheer.tierList.FirstOrDefault().Value?.ImageScale ?? 2;
                var newScale = 2.0 / imageScale * renderOptions.ReferenceScale * renderOptions.EmoteScale;
                cheer.SnapScale(newScale, snapThreshold, snapThreshold);
            }

            return cheerTask;
        }

        private async Task<Dictionary<string, SKImage>> GetScaledEmojis(CancellationToken cancellationToken)
        {
            var emojis = await TwitchHelper.GetEmojis(_cacheDir, renderOptions.EmojiVendor, _progress, cancellationToken);

            var newHeight = (int)Math.Round(36 * renderOptions.ReferenceScale * renderOptions.EmojiScale);

            return emojis.Keys.ToDictionary(x =>
            {
                var span = x.AsSpan();
                var sb = new StringBuilder(span.Length / 5); // '1234 '
                foreach (var range in span.SplitAny())
                {
                    var num = uint.Parse(span[range], NumberStyles.HexNumber);
                    if (num == 0xFE0F) continue;

                    sb.Append($"{new Rune(num)}");
                }

                return sb.ToString();
            }, x =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var bitmap = emojis[x];
                SKImageInfo oldEmojiInfo = bitmap.Info;
                SKImageInfo imageInfo = new SKImageInfo((int)(newHeight / (double)oldEmojiInfo.Height * oldEmojiInfo.Width), newHeight);
                using var newBitmap = new SKBitmap(imageInfo);
                bitmap.ScalePixels(newBitmap, SKFilterQuality.High);

                newBitmap.SetImmutable();
                return SKImage.FromBitmap(newBitmap);
            });
        }

        private async Task<Dictionary<string, SKImage>> GetScaledAvatars(CancellationToken cancellationToken)
        {
            var avatars = await TwitchHelper.GetAvatars(chatRoot.comments, DefaultAvatarUrls, _cacheDir, _progress, renderOptions.Offline, cancellationToken);

            var newHeight = (int)Math.Round(36 * renderOptions.ReferenceScale * renderOptions.AvatarScale);

            using var maskPath = new SKPath();
            var radius = newHeight / 2;
            maskPath.AddCircle(radius, radius, radius);

            return avatars.Keys.ToDictionary(x => x, x =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var oldBitmap = avatars[x];
                var oldImageInfo = oldBitmap.Info;
                var imageInfo = new SKImageInfo((int)(newHeight / (double)oldImageInfo.Height * oldImageInfo.Width), newHeight);
                using var newBitmap = new SKBitmap(imageInfo);
                oldBitmap.ScalePixels(newBitmap, SKFilterQuality.High);

                // Clip avatar to circle
                using (var canvas = new SKCanvas(newBitmap))
                {
                    canvas.ClipPath(maskPath, SKClipOperation.Difference, true);
                    canvas.Clear();
                }

                newBitmap.SetImmutable();
                return SKImage.FromBitmap(newBitmap);
            });
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
            else
            {
                int startSeconds = (int)Math.Floor(chatRoot.video.start);
                int videoStartTick = startSeconds * renderOptions.Framerate;
                int totalTicks = (int)Math.Ceiling(chatRoot.video.end * renderOptions.Framerate) - videoStartTick;
                return (videoStartTick, totalTicks);
            }
        }

        private SKPaint GetCachedPaint(SKColor color)
        {
            ref var paint = ref CollectionsMarshal.GetValueRefOrAddDefault(_paintCache, color, out var alreadyExists);
            if (alreadyExists)
            {
                return paint;
            }

            paint = new SKPaint { Color = color };
            return paint;
        }

        private SKPaint GetFallbackFont(int input)
        {
            ref var fallbackPaint = ref CollectionsMarshal.GetValueRefOrAddDefault(_fallbackFontCache, input, out bool alreadyExists);
            if (alreadyExists)
            {
                return fallbackPaint;
            }

            SKPaint newPaint = new SKPaint() { Typeface = fontManager.MatchCharacter(input), LcdRenderText = true, TextSize = (float)renderOptions.FontSize, IsAntialias = true, SubpixelText = true, IsAutohinted = true, HintingLevel = SKPaintHinting.Full, FilterQuality = SKFilterQuality.High };
            if (newPaint.Typeface == null)
            {
                newPaint.Typeface = SKTypeface.Default;
                if (!noFallbackFontFound)
                {
                    noFallbackFontFound = true;
                    _progress.LogWarning("No valid typefaces were found for some messages.");
                    _progress.LogVerbose($"Could not find typeface for codepoint: {input}");
                }
            }

            fallbackPaint = newPaint;
            return newPaint;
        }

        private static bool IsNotAscii(char input)
        {
            return input > 127;
        }

        /// <summary>Sorts the word order of a given <paramref name="enumerator"/> by RTL rules.</summary>
        /// <param name="enumerator">A span enumerator.</param>
        /// <param name="destination">The RTL-swapped output from enumerating the <paramref name="enumerator"/>.</param>
        /// <returns>The number of words written.</returns>
        /// <exception cref="IndexOutOfRangeException"><paramref name="destination"/> was too small.</exception>
        private static int SwapRightToLeft(MemoryExtensions.SpanSplitEnumerator<char> enumerator, Span<Range> destination)
        {
            var source = enumerator.Source;
            var rtlStack = ArrayPool<Range>.Shared.Rent(destination.Length);
            var rtlStackPos = 0;
            var destPos = 0;

            try
            {
                foreach (var range in enumerator)
                {
                    if (range.Start.Value == range.End.Value) continue;

                    if (IsRightToLeft(source[range]))
                    {
                        rtlStack[rtlStackPos] = range;
                        rtlStackPos++;
                    }
                    else
                    {
                        EmptyRtlStack(rtlStack, ref rtlStackPos, destination, ref destPos);

                        destination[destPos] = range;
                        destPos++;
                    }
                }

                EmptyRtlStack(rtlStack, ref rtlStackPos, destination, ref destPos);

                return destPos;
            }
            finally
            {
                ArrayPool<Range>.Shared.Return(rtlStack);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static void EmptyRtlStack(Span<Range> rtlStack, ref int rtlStackPos, Span<Range> destination, ref int destPos)
            {
                while (rtlStackPos > 0)
                {
                    destination[destPos] = rtlStack[rtlStackPos - 1];
                    rtlStackPos--;
                    destPos++;
                }
            }
        }

        private static bool IsRightToLeft(ReadOnlySpan<char> message)
        {
            if (message.Length > 0)
            {
                return message[0] >= '\u0591' && message[0] <= '\u07FF';
            }

            return false;
        }

        public async Task<ChatRoot> ParseJsonAsync(CancellationToken cancellationToken = new())
        {
            chatRoot = await ChatJson.DeserializeAsync(renderOptions.InputFile, true, false, true, cancellationToken);
            return chatRoot;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool isDisposing)
        {
            try
            {
                if (Disposed)
                {
                    return;
                }

                if (isDisposing)
                {
                    _badgeCache.Dispose();
                    _emoteCache.Dispose();
                    _emoteThirdCache.Dispose();
                    _cheermoteCache.Dispose();
                    _emojiCache.Dispose();
                    _avatarCache.Dispose();
                    _fallbackFontCache.Dispose();
                    _paintCache.Dispose();
                    _sectionImageCache?.Dispose();
                    fontManager?.Dispose();
                    nameFont?.Dispose();
                    messageFont?.Dispose();
                    outlinePaint?.Dispose();
                    highlightIcons?.Dispose();
                    _animCanvas?.Dispose();
                    _animComposedFrame?.Dispose();

                    _badgeCache.Clear();
                    _emoteCache.Clear();
                    _emoteThirdCache.Clear();
                    _cheermoteCache.Clear();
                    _emojiCache.Clear();
                    _avatarCache.Clear();
                    _fallbackFontCache.Clear();
                    _paintCache.Clear();

                    // Let the GC collect the caches immediately
                    chatRoot = null;
                    _badgeCache = null;
                    _emoteCache = null;
                    _emoteThirdCache = null;
                    _cheermoteCache = null;
                    _emojiCache = null;
                    _avatarCache = null;
                    _fallbackFontCache = null;
                    _paintCache = null;
                }
            }
            finally
            {
                Disposed = true;
            }
        }
    }
}
