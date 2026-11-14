using System.Buffers;
using System.Runtime.CompilerServices;
using SkiaSharp;
using SkiaSharp.HarfBuzz;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Extensions;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.RenderTests
{
    // Unit tests for ChatRenderer's private helpers (reached with [UnsafeAccessor]): comment timing (dispersion/jitter/flooring),
    // filtering, tick math, mask extraction, animation timing, RTL handling, text measuring/wrapping, colours and emoji lookup.
    // These run without ffmpeg and pin down edge cases that are too fiddly to see in a rendered frame.
    public class ChatRendererHelperTests
    {
        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "DisperseCommentOffsets")]
        private static extern void DisperseCommentOffsets(ChatRenderer? @null, List<Comment> comments);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "JitterCommentOffsets")]
        private static extern void JitterCommentOffsets(ChatRenderer? @null, List<Comment> comments);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "FloorCommentOffsets")]
        private static extern void FloorCommentOffsets(ChatRenderer renderer, List<Comment> comments);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "RemoveRestrictedComments")]
        private static extern void RemoveRestrictedComments(ChatRenderer renderer, List<Comment> comments);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetVideoTicks")]
        private static extern (int startTick, int totalTicks) GetVideoTicks(ChatRenderer renderer);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_chatRoot")]
        private static extern void SetChatRoot(ChatRenderer renderer, ChatRoot chatRoot);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SetFrameMask")]
        private static extern void SetFrameMask(ChatRenderer? @null, ReadOnlySpan<byte> frame, Span<byte> maskBytes);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "ComputeAnimFrameIndex")]
        private static extern int ComputeAnimFrameIndex(ChatRenderer? @null, TwitchEmote emote, long currentTickMs);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "IsNotAscii")]
        private static extern bool IsNotAscii(ChatRenderer? @null, char input);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "IsRightToLeft")]
        private static extern bool IsRightToLeft(ChatRenderer? @null, ReadOnlySpan<char> message);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SwapRightToLeft")]
        private static extern int SwapRightToLeft(ChatRenderer? @null, MemoryExtensions.SpanSplitEnumerator<char> enumerator, Span<Range> destination);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "MeasureText")]
        private static extern float MeasureText(ChatRenderer? @null, ReadOnlySpan<char> text, SKPaint textFont, bool? isRtl, SKShaper? shaper);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SubstringToTextWidth")]
        private static extern ReadOnlySpan<char> SubstringToTextWidth(ChatRenderer? @null, ReadOnlySpan<char> text, SKPaint textFont, int maxWidth, bool isRtl, SearchValues<char> delimiters);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "GetInterTypeface")]
        private static extern SKTypeface GetInterTypeface(ChatRenderer? @null, SKFontStyle fontStyle);

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "AdjustColorVisibility")]
        private static extern SKColor AdjustColorVisibility(ChatRenderer? @null, SKColor foreground, SKColor background);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AdjustUsernameVisibility")]
        private static extern SKColor AdjustUsernameVisibility(ChatRenderer renderer, SKColor userColor, SKColor backgroundColor);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetMessageBackground")]
        private static extern SKColor GetMessageBackground(ChatRenderer renderer, int commentIndex, out SKPaint? paint);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetFallbackFont")]
        private static extern SKPaint GetFallbackFont(ChatRenderer renderer, int input);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainsEmoji")]
        private static extern bool ContainsEmoji(ChatRenderer renderer, ReadOnlySpan<char> text, out int firstEmoji);

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_emojiCache")]
        private static extern ref DisposableDictionary<string, SKImage> EmojiCache(ChatRenderer renderer);

        private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static ChatRenderer NewRenderer(ChatRenderOptions? options = null, RecordingTaskProgress? progress = null) =>
            new(options ?? new ChatRenderOptions(), progress ?? new RecordingTaskProgress());

        private static List<Comment> CommentsAt(params double[] offsets) =>
            offsets.Select(o => RenderHarness.MakeComment(o, "user", "hi")).ToList();

        private static double[] Offsets(List<Comment> comments) => comments.Select(c => c.content_offset_seconds).ToArray();

        /// <summary>
        /// Post-2022 chats only have whole-second offsets; when every comment's created_at agrees on the video start (within 1.5 s) the
        /// offsets are rebuilt from created_at, restoring sub-second timing. Guards the dispersion option's main path.
        /// </summary>
        [Fact]
        public void DisperseCommentOffsets_ConsistentCreatedAt_RestoresSubSecondOffsets()
        {
            double[] actual = [10.25, 10.75, 11.5, 12.0];
            var comments = actual.Select(a =>
            {
                var c = RenderHarness.MakeComment(Math.Floor(a), "user", "hi");
                c.created_at = Created.AddSeconds(a);
                return c;
            }).ToList();

            DisperseCommentOffsets(null, comments);

            Assert.Equal(actual, Offsets(comments));
        }

        /// <summary>A chat that already has fractional offsets (pre-2022, millisecond accurate) must be left untouched.</summary>
        [Fact]
        public void DisperseCommentOffsets_FractionalOffsets_LeavesChatUntouched()
        {
            var comments = CommentsAt(1, 1.5, 2);
            comments[0].created_at = Created.AddHours(1); // would otherwise trigger dispersion/jitter

            DisperseCommentOffsets(null, comments);

            Assert.Equal([1, 1.5, 2], Offsets(comments));
        }

        /// <summary>
        /// When created_at is too inconsistent (≥1.5 s drift) to trust, dispersion falls back to jittering comments that share a second,
        /// keeping their order and staying inside that second.
        /// </summary>
        [Fact]
        public void DisperseCommentOffsets_InconsistentCreatedAt_FallsBackToJitter()
        {
            var comments = CommentsAt(20, 20, 20, 20, 21);
            comments[4].created_at = Created.AddSeconds(30);

            DisperseCommentOffsets(null, comments);

            var offsets = Offsets(comments);
            Assert.Equal(20, offsets[0]);
            Assert.Equal(21, offsets[4]);
            Assert.All(offsets[..4], o => Assert.InRange(o, 20, 20.999));
            Assert.Equal(offsets.Order(), offsets);
            Assert.Contains(offsets, o => o % 1 != 0);
        }

        /// <summary>
        /// Jitter spreads comments sharing a whole second across that second, deterministically (seeded by the comment count), without
        /// reordering them or moving the first comment of the second. Guards render reproducibility and comment order.
        /// </summary>
        [Fact]
        public void JitterCommentOffsets_SharedSecond_SpreadsDeterministicallyAndKeepsOrder()
        {
            var first = CommentsAt(1, 2, 2, 2, 2, 3);
            var second = CommentsAt(1, 2, 2, 2, 2, 3);

            JitterCommentOffsets(null, first);
            JitterCommentOffsets(null, second);

            var offsets = Offsets(first);
            Assert.Equal(offsets, Offsets(second));
            Assert.Equal(1, offsets[0]);
            Assert.Equal(2, offsets[1]);
            Assert.Equal(3, offsets[5]);
            Assert.All(offsets[1..5], o => Assert.InRange(o, 2, 2.999));
            Assert.Equal(offsets.Order(), offsets);
            Assert.True(offsets[4] > 2);
        }

        /// <summary>
        /// If the next comment is less than a second away (mixed whole/fractional offsets), the spread is scaled down so jittered comments
        /// never overtake it; fractional groups are not jittered at all.
        /// </summary>
        [Fact]
        public void JitterCommentOffsets_NextCommentWithinASecond_NeverOvertakesIt()
        {
            var comments = CommentsAt(4, 4, 4, 4, 4.5, 6.5, 6.5);

            JitterCommentOffsets(null, comments);

            var offsets = Offsets(comments);
            Assert.All(offsets[..4], o => Assert.InRange(o, 4, 4.5));
            Assert.Equal(offsets.Order(), offsets);
            Assert.Equal([4.5, 6.5, 6.5], offsets[4..]);
        }

        /// <summary>
        /// Per the method's documentation, "a=1.0 b=2.0 c=2.0 d=2.0 e=2.0 f=3.0 becomes a=1.0 b=2.0 c=2.25 d=2.5 e=2.75 f=3.0": every
        /// comment after the first on a shared second must be moved off the whole second, and a pair of comments must be dispersed too.
        /// </summary>
        [Fact(Skip = "Bug: JitterCommentOffsets starts at startIndex = i + 1 and then skips c = 0 as well, so the second comment on a shared second is never dispersed (two comments on the same second are left identical).")]
        public void JitterCommentOffsets_SharedSecond_MovesEveryCommentButTheFirst()
        {
            var group = CommentsAt(1, 2, 2, 2, 2, 3);
            var pair = CommentsAt(5, 5, 6);

            JitterCommentOffsets(null, group);
            JitterCommentOffsets(null, pair);

            var offsets = Offsets(group);
            Assert.True(offsets[1] == 2 && offsets[2] > 2 && offsets[3] > offsets[2] && offsets[4] > offsets[3] && offsets[4] < 3, string.Join(", ", offsets));
            Assert.InRange(pair[1].content_offset_seconds, 5.01, 5.99);
        }

        /// <summary>
        /// Offsets are floored to the update rate (or to whole seconds for rates above 1 s) so a comment is shown on the update at or
        /// before its time; non-positive rates disable flooring. Guards when comments pop into the render.
        /// </summary>
        [Theory]
        [InlineData(0.0, 1.7, 1.7)]
        [InlineData(-1.0, 1.7, 1.7)]
        [InlineData(2.0, 3.9, 3.0)]
        [InlineData(1.5, 3.9, 3.0)]
        [InlineData(1.0, 2.99, 2.0)]
        [InlineData(0.5, 1.7, 1.5)]
        [InlineData(0.25, 1.9, 1.75)]
        public void FloorCommentOffsets_FloorsToUpdateRate(double updateRate, double offset, double expected)
        {
            using var renderer = NewRenderer(new ChatRenderOptions { UpdateRate = updateRate });
            var comments = CommentsAt(offset);

            FloorCommentOffsets(renderer, comments);

            Assert.Equal(expected, comments[0].content_offset_seconds, 9);
        }

        /// <summary>An offset that is already a multiple of the update rate must not move (0.6 s at the 0.2 s CLI default update rate).</summary>
        [Fact(Skip = "Bug: FloorCommentOffsets uses Math.Floor(offset / rate) * rate, and 0.6 / 0.2 == 2.9999999999999996 in floating point, so the comment is floored to 0.4 s and shown one update early.")]
        public void FloorCommentOffsets_ExactMultipleOfUpdateRate_IsUnchanged()
        {
            using var renderer = NewRenderer(new ChatRenderOptions { UpdateRate = 0.2 });
            var comments = CommentsAt(0.6, 1.2);

            FloorCommentOffsets(renderer, comments);

            Assert.Equal(0.6, comments[0].content_offset_seconds, 9);
            Assert.Equal(1.2, comments[1].content_offset_seconds, 9);
        }

        /// <summary>Without ignored users or banned words nothing is removed (and the method returns early).</summary>
        [Fact]
        public void RemoveRestrictedComments_NoFilters_KeepsEverything()
        {
            using var renderer = NewRenderer();
            var comments = CommentsAt(1, 2, 3);

            RemoveRestrictedComments(renderer, comments);

            Assert.Equal(3, comments.Count);
        }

        /// <summary>
        /// Ignored users match the login case-insensitively, and non-ASCII display names (which differ from the login) are matched too.
        /// Guards the "ignore users" option, e.g. for hiding bots.
        /// </summary>
        [Fact]
        public void RemoveRestrictedComments_IgnoredUsers_RemovesByLoginOrNonAsciiDisplayName()
        {
            using var renderer = NewRenderer(new ChatRenderOptions { IgnoreUsersArray = ["NIGHTBOT", "테스트"] });
            var comments = new List<Comment>
            {
                RenderHarness.MakeComment(0, "nightbot", "beep", displayName: "Nightbot"),
                RenderHarness.MakeComment(1, "alice", "hi"),
                RenderHarness.MakeComment(2, "test_user", "hello", displayName: "테스트"),
            };

            RemoveRestrictedComments(renderer, comments);

            Assert.Equal("alice", Assert.Single(comments).commenter.name);
        }

        /// <summary>
        /// Banned words are matched case-insensitively as whole words (digits and underscores also count as boundaries) and are regex
        /// escaped. Guards against both missed bans and over-matching words that merely contain the banned text.
        /// </summary>
        [Theory]
        [InlineData("bad", "this is bad", true)]
        [InlineData("bad", "BAD idea", true)]
        [InlineData("bad", "a_bad_b", true)]
        [InlineData("bad", "1bad2", true)]
        [InlineData("bad", "badger", false)]
        [InlineData("bad", "notbad", false)]
        [InlineData("a.b", "say a.b now", true)]
        [InlineData("a.b", "say axb now", false)]
        [InlineData("two words", "these two words here", true)]
        public void RemoveRestrictedComments_BannedWords_MatchWholeWords(string bannedWord, string body, bool removed)
        {
            using var renderer = NewRenderer(new ChatRenderOptions { BannedWordsArray = [bannedWord] });
            var comments = new List<Comment> { RenderHarness.MakeComment(0, "alice", body) };

            RemoveRestrictedComments(renderer, comments);

            Assert.Equal(removed, comments.Count == 0);
        }

        /// <summary>A banned word that starts with punctuation (e.g. a bot command like "!drop") must still remove messages using it.</summary>
        [Fact(Skip = "Bug: the banned-word regex requires a \\b word boundary, which never exists between a space/start of message and a word starting with punctuation, so \"!drop\" is never matched.")]
        public void RemoveRestrictedComments_BannedWordStartingWithPunctuation_IsRemoved()
        {
            using var renderer = NewRenderer(new ChatRenderOptions { BannedWordsArray = ["!drop"] });
            var comments = new List<Comment> { RenderHarness.MakeComment(0, "alice", "!drop"), RenderHarness.MakeComment(1, "bob", "type !drop now") };

            RemoveRestrictedComments(renderer, comments);

            Assert.Empty(comments);
        }

        /// <summary>
        /// The render covers floor(video.start)..ceil(video.end) of the chat unless both Start/EndOverride are set (trimmed render).
        /// Guards the number of frames rendered, i.e. the output video length.
        /// </summary>
        [Theory]
        [InlineData(0.0, 3.0, -1, -1, 10, 0, 30)]
        [InlineData(1.5, 4.25, -1, -1, 10, 10, 33)]
        [InlineData(0.0, 100.0, 5, 8, 30, 150, 90)]
        [InlineData(0.0, 3.0, 5, -1, 10, 0, 30)]
        public void GetVideoTicks_UsesChatRangeOrBothOverrides(double start, double end, int startOverride, int endOverride, int fps, int expectedStart, int expectedTotal)
        {
            using var renderer = NewRenderer(new ChatRenderOptions { Framerate = fps, StartOverride = startOverride, EndOverride = endOverride });
            SetChatRoot(renderer, new ChatRoot { video = new Video { start = start, end = end }, comments = [] });

            Assert.Equal((expectedStart, expectedTotal), GetVideoTicks(renderer));
        }

        /// <summary>
        /// The mask byte for each pixel must be that pixel's alpha, for every length (exercising the AVX2/SSSE3/NEON loops and the scalar
        /// tail). Guards the transparency mask users composite chat with.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(17)]
        [InlineData(1001)]
        public void SetFrameMask_CopiesAlphaChannel(int pixels)
        {
            var frame = new byte[pixels * 4];
            new Random(pixels).NextBytes(frame);
            var mask = new byte[pixels];

            SetFrameMask(null, frame, mask);

            for (var i = 0; i < pixels; i++)
            {
                Assert.Equal(frame[i * 4 + 3], mask[i]);
            }
        }

        private static async Task<TwitchEmote> LoadFixtureEmote(string name)
        {
            var chat = await ChatJson.DeserializeAsync(TestData.Path("Render", "synthetic_chat.json"), cancellationToken: TestContext.Current.CancellationToken);
            var data = chat.embeddedData.thirdParty.Single(e => e.name == name);
            return new TwitchEmote(data.data, null, EmoteProvider.ThirdParty, data.imageScale, data.id, data.name);
        }

        /// <summary>
        /// The fixture GIF has frames of 100, 200 and 300 ms; the frame index for a time must follow those durations and loop every 600 ms.
        /// Guards animated emote timing.
        /// </summary>
        [Theory]
        [InlineData(0, 0)]
        [InlineData(99, 0)]
        [InlineData(101, 1)]
        [InlineData(299, 1)]
        [InlineData(301, 2)]
        [InlineData(599, 2)]
        [InlineData(600, 0)]
        [InlineData(1_250, 0)]
        [InlineData(6_000_150, 1)]
        public async Task ComputeAnimFrameIndex_FollowsGifFrameDurations(long ms, int expectedFrame)
        {
            using var emote = await LoadFixtureEmote("AnimThird");

            Assert.Equal(3, emote.FrameCount);
            Assert.Equal(expectedFrame, ComputeAnimFrameIndex(null, emote, ms));
        }

        /// <summary>A frame starting at exactly 100 ms (or 300 ms) must be shown from that millisecond on, not one millisecond later.</summary>
        [Fact(Skip = "Bug: ComputeAnimFrameIndex uses 'imageFrame <= 0', so on an exact frame boundary the previous frame is returned; at frame rates whose ticks land on boundaries (e.g. 10 fps with 100 ms frames) frames show one tick late and the last frame is skipped.")]
        public async Task ComputeAnimFrameIndex_ExactFrameBoundary_ReturnsNextFrame()
        {
            using var emote = await LoadFixtureEmote("AnimThird");

            Assert.Equal(1, ComputeAnimFrameIndex(null, emote, 100));
            Assert.Equal(2, ComputeAnimFrameIndex(null, emote, 300));
        }

        /// <summary>Non-ASCII detection decides when display names need a fallback font and are checked against ignored users.</summary>
        [Theory]
        [InlineData('a', false)]
        [InlineData('\x7F', false)]
        [InlineData('\x80', true)]
        [InlineData('é', true)]
        [InlineData('漢', true)]
        public void IsNotAscii_DetectsCharsAbove127(char c, bool expected)
        {
            Assert.Equal(expected, IsNotAscii(null, c));
        }

        /// <summary>
        /// A word is treated as right-to-left when its first char is in the Hebrew/Arabic/Syriac/Thaana/NKo block (U+0591-U+07FF).
        /// Guards RTL word ordering and shaped text drawing.
        /// </summary>
        [Theory]
        [InlineData("שלום", true)]
        [InlineData("مرحبا", true)]
        [InlineData("\u0591", true)]
        [InlineData("\u07FF", true)]
        [InlineData("\u0800", false)]
        [InlineData("hello", false)]
        [InlineData("aשלום", false)]
        [InlineData("", false)]
        public void IsRightToLeft_ChecksFirstChar(string text, bool expected)
        {
            Assert.Equal(expected, IsRightToLeft(null, text));
        }

        /// <summary>
        /// Runs of consecutive RTL words are reversed in place while LTR words keep their position, and empty splits are dropped, so mixed
        /// Hebrew/English messages read correctly left to right. Guards RTL message layout.
        /// </summary>
        [Theory]
        [InlineData("hello world", "hello world")]
        [InlineData("hello שלום עולם world", "hello עולם שלום world")]
        [InlineData("אחת שתיים שלוש", "שלוש שתיים אחת")]
        [InlineData("a  b", "a b")]
        [InlineData("", "")]
        public void SwapRightToLeft_ReversesRtlRuns(string input, string expected)
        {
            var destination = new Range[input.Length + 1];

            var written = SwapRightToLeft(null, input.AsSpan().Split(' '), destination);

            Assert.Equal(expected, string.Join(' ', destination.Take(written).Select(r => input[r])));
        }

        private static SKPaint InterPaint() => new() { Typeface = GetInterTypeface(null, SKFontStyle.Normal), TextSize = 24 };

        /// <summary>
        /// LTR text is measured with Skia directly and RTL text with HarfBuzz shaping, auto-detected when isRtl is null; a supplied shaper
        /// gives the same result as a temporary one. Guards the widths used for word wrapping.
        /// </summary>
        [Fact]
        public void MeasureText_LtrAndRtl_AreMeasuredConsistently()
        {
            using var paint = InterPaint();
            using var shaper = new SKShaper(paint.Typeface);

            Assert.Equal(paint.MeasureText("hello"), MeasureText(null, "hello", paint, null, null));
            Assert.Equal(paint.MeasureText("hello"), MeasureText(null, "hello", paint, false, null));

            var rtl = MeasureText(null, "שלום", paint, null, null);
            Assert.True(rtl > 0);
            Assert.Equal(rtl, MeasureText(null, "שלום", paint, true, shaper));
        }

        /// <summary>
        /// Without delimiters the longest prefix that fits is returned; with '-' delimiters the cut happens after the last delimiter that
        /// fits; text that already fits is returned whole; RTL text is cut by shaped width. Guards how over-long words are wrapped.
        /// </summary>
        [Fact]
        public void SubstringToTextWidth_CutsToWidthOrLastDelimiter()
        {
            using var paint = InterPaint();
            var delimiters = SearchValues.Create("?-");

            const string ALPHABET = "abcdefghijklmnopqrstuvwxyz";
            var maxWidth = (int)paint.MeasureText("abcdefghij");
            var cut = SubstringToTextWidth(null, ALPHABET, paint, maxWidth, false, delimiters).ToString();
            Assert.StartsWith(cut, ALPHABET);
            Assert.True(paint.MeasureText(cut) <= maxWidth);
            Assert.True(paint.MeasureText(ALPHABET[..(cut.Length + 1)]) > maxWidth);

            const string DASHED = "aaaa-bbbb-cccc-dddd";
            Assert.Equal("aaaa-bbbb-", SubstringToTextWidth(null, DASHED, paint, (int)paint.MeasureText("aaaa-bbbb-cc"), false, delimiters).ToString());
            Assert.Equal(DASHED, SubstringToTextWidth(null, DASHED, paint, 10_000, false, delimiters).ToString());

            const string HEBREW = "שלוםשלוםשלוםשלום";
            var rtlMax = (int)(MeasureText(null, HEBREW, paint, true, null) / 2);
            var rtlCut = SubstringToTextWidth(null, HEBREW, paint, rtlMax, true, delimiters);
            Assert.InRange(rtlCut.Length, 1, HEBREW.Length - 1);
            Assert.True(MeasureText(null, rtlCut, paint, true, null) <= rtlMax);
        }

        /// <summary>The embedded Inter font must load in every style the CLI/GUI offer, so the default font never depends on the OS.</summary>
        [Theory]
        [InlineData(400, SKFontStyleSlant.Upright)]
        [InlineData(700, SKFontStyleSlant.Upright)]
        [InlineData(400, SKFontStyleSlant.Italic)]
        [InlineData(700, SKFontStyleSlant.Italic)]
        public void GetInterTypeface_LoadsEmbeddedInterInEveryStyle(int weight, SKFontStyleSlant slant)
        {
            var style = (weight, slant) switch
            {
                (700, SKFontStyleSlant.Upright) => SKFontStyle.Bold,
                (400, SKFontStyleSlant.Italic) => SKFontStyle.Italic,
                (700, SKFontStyleSlant.Italic) => SKFontStyle.BoldItalic,
                _ => SKFontStyle.Normal,
            };

            using var typeface = GetInterTypeface(null, style);

            Assert.Equal("Inter", typeface.FamilyName);
            Assert.Equal(weight, typeface.FontStyle.Weight);
            Assert.Equal(slant, typeface.FontStyle.Slant);
        }

        /// <summary>
        /// Username colours are adjusted against the outline colour when outlines are on, against an opaque background, blended by alpha
        /// for a semi-transparent background, and left alone when the background is mostly transparent (its lightness is unknown).
        /// Guards the "readable colors" option.
        /// </summary>
        [Fact]
        public void AdjustUsernameVisibility_DependsOnOutlineAndBackgroundAlpha()
        {
            var user = new SKColor(0xFF0000FF);
            var opaque = new SKColor(0xFF111111);
            var translucent = new SKColor(0xC8111111);

            using var plain = NewRenderer(new ChatRenderOptions());
            using var outlined = NewRenderer(new ChatRenderOptions { Outline = true });

            Assert.Equal(user, AdjustUsernameVisibility(plain, user, SKColors.Transparent));
            Assert.Equal(AdjustColorVisibility(null, user, opaque), AdjustUsernameVisibility(plain, user, opaque));
            Assert.Equal(user.Lerp(AdjustColorVisibility(null, user, translucent), 0xC8 / 255f), AdjustUsernameVisibility(plain, user, translucent));
            Assert.Equal(AdjustColorVisibility(null, user, SKColors.Black), AdjustUsernameVisibility(outlined, user, SKColors.Transparent));
            Assert.NotEqual(user, AdjustUsernameVisibility(outlined, user, SKColors.Transparent));
        }

        /// <summary>
        /// On a saturated background, a saturated username whose hue is far (>35°) from the background's keeps its hue (only saturation is
        /// capped); hue is only pushed away when the two are close. Guards against needlessly recolouring readable names.
        /// </summary>
        [Fact]
        public void AdjustColorVisibility_SaturatedColoursFarApartInHue_KeepHue()
        {
            AdjustColorVisibility(null, new SKColor(0xFFFF0000), new SKColor(0xFF0000FF)).ToHsl(out var hue, out var saturation, out _);

            Assert.True(hue < 1 || hue > 359, $"hue {hue}");
            Assert.Equal(90, saturation, 1.0); // HSL → 8-bit RGB → HSL round trip
        }

        /// <summary>
        /// Message backgrounds: default for normal rows, alternate colour on odd rows, highlight colour for highlighted users matched by
        /// login or display name (case-insensitive), and the dedicated alternate-highlight paint on odd highlighted rows.
        /// Guards the row colours and the paint cache.
        /// </summary>
        [Fact]
        public void GetMessageBackground_PicksRowColour()
        {
            var options = new ChatRenderOptions
            {
                BackgroundColor = new SKColor(0xFF111111), AlternateBackgroundColor = new SKColor(0xFF191919), HighlightUserColor = new SKColor(0xFF503010),
                AlternateMessageBackgrounds = true, HighlightUsersArray = ["CAROL", "dave"],
            };
            using var renderer = NewRenderer(options);
            SetChatRoot(renderer, new ChatRoot
            {
                comments =
                [
                    RenderHarness.MakeComment(0, "alice", "a"), RenderHarness.MakeComment(0, "bob", "b"),
                    RenderHarness.MakeComment(0, "carol_tv", "c", displayName: "Carol"), RenderHarness.MakeComment(0, "dave", "d"),
                ]
            });

            Assert.Equal(options.BackgroundColor, GetMessageBackground(renderer, 0, out var paint0));
            Assert.Null(paint0);

            Assert.Equal(options.AlternateBackgroundColor, GetMessageBackground(renderer, 1, out var paint1));
            Assert.Same(options.AlternateBackgroundPaint, paint1);

            Assert.Equal(options.HighlightUserColor, GetMessageBackground(renderer, 2, out var paint2));
            Assert.Equal(options.HighlightUserColor, paint2!.Color);
            GetMessageBackground(renderer, 2, out var paint2Again);
            Assert.Same(paint2, paint2Again);

            var color3 = GetMessageBackground(renderer, 3, out var paint3);
            Assert.Same(options.AlternateBackgroundHighlightUserPaint, paint3);
            Assert.Equal(paint3!.Color, color3);
        }

        /// <summary>
        /// Fallback fonts are cached per codepoint at the message font size, and a missing-typeface warning is logged at most once per
        /// render instead of once per character. Guards log spam and per-character typeface lookups.
        /// </summary>
        [Fact]
        public void GetFallbackFont_IsCachedAndWarnsAtMostOnce()
        {
            var progress = new RecordingTaskProgress();
            using var renderer = NewRenderer(new ChatRenderOptions { FontSize = 18 }, progress);

            var first = GetFallbackFont(renderer, '漢');
            Assert.Same(first, GetFallbackFont(renderer, '漢'));
            Assert.Equal(18, first.TextSize);
            Assert.NotNull(first.Typeface);

            GetFallbackFont(renderer, 0x10FFFD);
            GetFallbackFont(renderer, 0x10FFFC);
            Assert.True(progress.Warnings.Count <= 1);
        }

        private static ChatRenderer RendererWithEmojis(params string[] emojis)
        {
            var renderer = NewRenderer();
            using var bitmap = new SKBitmap(1, 1);
            foreach (var emoji in emojis)
            {
                EmojiCache(renderer).Add(emoji, SKImage.FromBitmap(bitmap));
            }

            return renderer;
        }

        /// <summary>
        /// Emoji detection returns the index of the first emoji in a word, ignores variation selectors (U+FE0F) and finds emojis inside
        /// ZWJ sequences the vendor has no combined image for. Guards which words take the emoji drawing path.
        /// </summary>
        [Theory]
        [InlineData("hello", false, -1)]
        [InlineData("\U0001F600", true, 0)]
        [InlineData("ab\U0001F600", true, 2)]
        [InlineData("☺\uFE0F", true, 0)]
        [InlineData("x\U0001F468\u200D\U0001F469", true, 1)]
        [InlineData("é漢", false, -1)]
        public void ContainsEmoji_FindsFirstKnownEmoji(string text, bool expected, int expectedIndex)
        {
            using var renderer = RendererWithEmojis("\U0001F600", "☺", "\U0001F469");

            Assert.Equal(expected, ContainsEmoji(renderer, text, out var index));
            Assert.Equal(expectedIndex, index);
        }

        /// <summary>A word whose grapheme cluster is longer than 16 UTF-16 chars (e.g. zalgo text) must not crash emoji detection.</summary>
        [Fact(Skip = "Bug: ContainsEmoji (and DrawEmojiMessage) allocate a larger lookupKey for clusters over 16 chars but still copy into the 16-char stackSpace, so CopyTo throws ArgumentException and the whole render fails whenever an emoji vendor is enabled.")]
        public void ContainsEmoji_LongGraphemeCluster_DoesNotThrow()
        {
            using var renderer = RendererWithEmojis("\U0001F600");
            var zalgo = "a" + new string('\u0337', 20);

            Assert.False(ContainsEmoji(renderer, zalgo, out _));
        }

        /// <summary>The constructor decides block-art pre-wrapping from the chat width: only chats wider than ~29 characters pre-wrap.</summary>
        [Theory]
        [InlineData(400, true)]
        [InlineData(300, false)]
        public void Constructor_ComputesBlockArtPreWrap(int chatWidth, bool expected)
        {
            var options = new ChatRenderOptions { FontSize = 12, ChatWidth = chatWidth };
            using var _ = NewRenderer(options);

            Assert.Equal(29.166 * 12 - options.SidePadding * 2, options.BlockArtPreWrapWidth, 6);
            Assert.Equal(expected, options.BlockArtPreWrap);
        }

        /// <summary>ParseJsonAsync loads the chat (comments, streamer, embedded images) into the renderer and returns that same object.</summary>
        [Fact]
        public async Task ParseJsonAsync_LoadsFixtureIntoRenderer()
        {
            using var renderer = NewRenderer(new ChatRenderOptions { InputFile = TestData.Path("Render", "synthetic_chat.json") });

            var chat = await renderer.ParseJsonAsync(TestContext.Current.CancellationToken);

            Assert.Same(chat, renderer.chatRoot);
            Assert.Equal(3, chat.comments.Count);
            Assert.Equal(1000, chat.streamer.id);
            Assert.Equal(3, chat.video.end);
            Assert.Equal(3, chat.embeddedData.thirdParty.Count);
            Assert.Single(chat.embeddedData.firstParty);
            Assert.Equal(4, chat.embeddedData.twitchBadges.Count);
            Assert.Equal(2, Assert.Single(chat.embeddedData.twitchBits).tierList.Count);
        }

        /// <summary>Dispose releases the caches and chat, and is idempotent (the GUI may dispose a renderer more than once).</summary>
        [Fact]
        public void Dispose_IsIdempotentAndReleasesChat()
        {
            var renderer = NewRenderer();

            renderer.Dispose();
            renderer.Dispose();

            Assert.True(renderer.Disposed);
            Assert.Null(renderer.chatRoot);
        }
    }
}
