using System.Runtime.CompilerServices;
using SkiaSharp;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tests.RenderTests
{
    // End-to-end tests for ChatRenderer.RenderVideoAsync. The chat render is the most complex feature of TwitchDownloader
    // (layout, emotes, badges, highlights, animation, ffmpeg piping), so these render the synthetic fixture offline through a real
    // ffmpeg into raw RGBA frames and check exact pixels. Every test needs ffmpeg and is skipped locally when it is missing.
    // Layout reference at the harness' 12pt font: section height 20px, vertical padding 12px, emotes 28px, badges 18px.
    public class ChatRendererRenderTests
    {
        private const int ROW_PITCH = 32; // section height + vertical padding for a one-line comment at 12pt
        private static readonly SKColor DarkAccent = new(0xFF26262C); // PayingForward
        private static readonly SKColor GrayAccent = new(0xFF80808C); // WatchStreak, Combo, ChannelPointHighlight

        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        /// <summary>
        /// Renders the committed fixture and verifies the whole pipeline: one frame per tick for the chat's 3 seconds, an untouched
        /// background above the messages, every embedded badge/emote/cheermote drawn (including a zero-width emote on top of a static one),
        /// comments appearing only once their offset is reached, and progress/status reporting.
        /// Guards against regressions anywhere in parse → fetch images → layout → draw → pipe to ffmpeg.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_Fixture_DrawsEveryEmbeddedImageAtTheRightTime()
        {
            using var harness = new RenderHarness();
            harness.Options.DisperseCommentOffsets = true;

            var video = await harness.RenderAsync(cancellationToken: Ct);

            Assert.True(video.IsWholeFrames);
            Assert.Equal(30, video.FrameCount); // video.end (3 s) * 10 fps
            Assert.Equal(RenderHarness.Background, video.Pixel(29, 0, 0));
            SKColor[] expected =
            [
                FixtureColors.FirstPartyEmote, FixtureColors.StaticThirdParty, FixtureColors.ZeroWidth, FixtureColors.SubscriberBadge,
                FixtureColors.ModeratorBadge, FixtureColors.BroadcasterBadge, FixtureColors.Cheer1, FixtureColors.Cheer100
            ];
            foreach (var color in expected)
            {
                Assert.True(video.Count(29, color) > 0, $"{color} was not drawn in the last frame");
            }

            // Bob's cheer has an offset of 1 s, so it must first appear on tick 10.
            Assert.Equal(0, video.Count(9, FixtureColors.Cheer100));
            Assert.True(video.Count(10, FixtureColors.Cheer100) > 0);

            Assert.Contains("Fetching Images [1/2]", harness.Progress.Statuses);
            Assert.Contains(100, harness.Progress.Percentages);
            Assert.Contains(harness.Progress.Info, x => x.StartsWith("FINISHED"));
            Assert.Contains(harness.Progress.Verbose, x => x.Contains("rawvideo"));
        }

        /// <summary>
        /// Renders with the CLI's default output arguments (libx264, yuv420p, mp4) and probes the result with ffmpeg: the codec,
        /// resolution, frame rate and frame count must match the options. Guards the {width}/{height}/{fps}/{save_path} placeholder
        /// substitution and the pixel-format handshake with ffmpeg that every real user relies on.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_DefaultCliOutputArgs_ProducesH264WithRequestedSizeRateAndLength()
        {
            using var harness = new RenderHarness();
            harness.Options.OutputFile = harness.Temp.Combine("chat.mp4");
            harness.Options.OutputArgs = "-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"";

            await harness.RenderOnlyAsync(cancellationToken: Ct);

            var (_, probe) = RenderHarness.RunFfmpeg("-hide_banner", "-i", harness.Options.OutputFile);
            Assert.Contains("Video: h264", probe);
            Assert.Contains("300x200", probe);
            Assert.Contains("10 fps", probe);

            var decoded = harness.Temp.Combine("decoded.rgba");
            var (exitCode, stderr) = RenderHarness.RunFfmpeg("-v", "error", "-i", harness.Options.OutputFile, "-f", "rawvideo", "-pix_fmt", "rgba", decoded);
            Assert.True(exitCode == 0, stderr);
            var video = RenderedVideo.Read(decoded, 300, 200, 4);
            Assert.Equal(30, video.FrameCount);
            var corner = video.Pixel(29, 0, 0);
            Assert.InRange(corner.Red, 0x08, 0x1A); // lossy, but still the dark background
        }

        /// <summary>
        /// With GenerateMask and a transparent background, a second "_mask" video must be written whose value is the alpha of the main
        /// render: 0 where nothing is drawn and 255 inside an opaque emote or a sub message's accent stroke. Guards the mask file naming,
        /// the second ffmpeg process and SetFrameMask's alpha extraction, which users depend on for compositing chat over gameplay.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_GenerateMask_WritesAlphaMaskAlongsideVideo()
        {
            using var harness = new RenderHarness();
            harness.Options.BackgroundColor = SKColors.Transparent;
            harness.Options.GenerateMask = true;

            var video = await harness.RenderAsync(c => c.comments =
            [
                RenderHarness.MakeComment(0, "dave", "Dave gifted a Tier 1 sub to Eve! ", displayName: "Dave"),
                RenderHarness.EmoteComment(0),
            ], Ct);
            var mask = RenderedVideo.Read(harness.Temp.Combine("chat_mask.rgba"), 300, 200, 4); // gray mask converted to rgba by the output args

            Assert.Equal(video.FrameCount, mask.FrameCount);
            Assert.Equal(0, video.Pixel(29, 0, 0).Alpha);
            Assert.Equal(0, mask.Pixel(29, 0, 0).Red);

            var emoteX = video.FirstColumnOf(29, FixtureColors.FirstPartyEmote);
            Assert.NotEqual(-1, emoteX);
            var emoteY = 200 - ROW_PITCH + 10; // middle of the last row
            Assert.Equal(FixtureColors.FirstPartyEmote, video.Pixel(29, emoteX + 5, emoteY));
            Assert.Equal(255, mask.Pixel(29, emoteX + 5, emoteY).Red);

            var accentY = 200 - 2 * ROW_PITCH + 10; // middle of the gift row; the accent stroke starts at the 3px side padding
            Assert.Equal(FixtureColors.Purple, video.Pixel(29, 4, accentY));
            Assert.Equal(255, mask.Pixel(29, 4, accentY).Red);
        }

        /// <summary>
        /// Enabling timestamps must reserve space before the username so the message (here an emote) starts further right.
        /// Guards the timestamp drawing branch and the defaultPos shift that keeps wrapped lines aligned after the timestamp.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_Timestamps_ShiftMessageRight()
        {
            using var without = new RenderHarness();
            var plain = await without.RenderAsync(c => c.comments = [RenderHarness.EmoteComment(0)], Ct);

            using var with = new RenderHarness();
            with.Options.Timestamp = true;
            var stamped = await with.RenderAsync(c => c.comments = [RenderHarness.EmoteComment(0)], Ct);

            Assert.True(stamped.FirstColumnOf(29, FixtureColors.FirstPartyEmote) > plain.FirstColumnOf(29, FixtureColors.FirstPartyEmote) + 10);
        }

        /// <summary>
        /// Renders a 1 s window (Start/EndOverride) at 25 h into a chat with comments whose timestamps fall in the m:ss, mm:ss, h:mm:ss,
        /// hh:mm:ss and ≥24 h formats. Each format has its own pre-measured width, so emotes in later rows must start at or right of
        /// earlier ones. Also uses an update interval (7 ticks) that does not divide the start tick, exercising the "no update frame yet"
        /// path. Guards the trim overrides and the 24 h+ timestamp formatter (which TimeSpan's h format cannot produce).
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_TrimmedWindowWithLongTimestamps_RendersEveryTimestampFormat()
        {
            using var harness = new RenderHarness();
            harness.Options.Timestamp = true;
            harness.Options.StartOverride = 90_000;
            harness.Options.EndOverride = 90_001;
            harness.Options.UpdateRate = 0.7;
            double[] offsets = [5, 700, 3_700, 37_000, 90_000];

            var video = await harness.RenderAsync(c => c.comments = offsets.Select(o => RenderHarness.EmoteComment(o)).ToList(), Ct);

            Assert.Equal(10, video.FrameCount);
            var xs = new int[offsets.Length];
            for (var row = 0; row < offsets.Length; row++)
            {
                var y = 200 - (offsets.Length - row) * ROW_PITCH + 10;
                xs[row] = Enumerable.Range(0, 300).FirstOrDefault(x => video.Pixel(9, x, y) == FixtureColors.FirstPartyEmote, -1);
                Assert.True(xs[row] > 0, $"row {row} has no emote");
            }

            Assert.True(xs[0] < xs[1] && xs[1] < xs[2] && xs[2] < xs[3] && xs[3] == xs[4], string.Join(", ", xs));
        }

        [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "DefaultAvatarUrls")]
        private static extern ref string[] DefaultAvatarUrls(ChatRenderer? @null);

        private static void CacheAvatar(string avatarDir, string url, SKColor color)
        {
            using var bitmap = new SKBitmap(70, 70);
            bitmap.Erase(color);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(avatarDir, Path.GetFileNameWithoutExtension(url) + "_2.jpg"), data.ToArray());
        }

        /// <summary>
        /// With avatars enabled, a commenter's cached avatar is drawn (clipped to a circle), and commenters without a logo or with an
        /// uncached logo get one of Twitch's default avatars. With avatars disabled nothing is drawn.
        /// Guards avatar loading from the cache, scaling/clipping and the default-avatar fallback.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RenderVideoAsync_Avatars_DrawnFromCacheOnlyWhenEnabled(bool renderAvatars)
        {
            var avatarColor = new SKColor(0xFF0080FF);
            var defaultAvatarColor = new SKColor(0xFF00A0A0);
            const string LOGO = "https://static-cdn.jtvnw.net/jtv_user_pictures/test-avatar-profile_image-70x70.png";

            using var harness = new RenderHarness();
            harness.Options.RenderUserAvatars = renderAvatars;
            var avatarDir = Directory.CreateDirectory(harness.Temp.Combine("TwitchDownloader", "avatars")).FullName;
            CacheAvatar(avatarDir, LOGO, avatarColor);
            foreach (var url in DefaultAvatarUrls(null))
            {
                CacheAvatar(avatarDir, url, defaultAvatarColor);
            }

            var video = await harness.RenderAsync(c => c.comments =
            [
                RenderHarness.MakeComment(0, "nologo", "no logo"),
                RenderHarness.MakeComment(0, "uncached", "uncached logo", logo: "https://static-cdn.jtvnw.net/jtv_user_pictures/missing-profile_image-70x70.png"),
                RenderHarness.MakeComment(0, "withlogo", "my avatar", logo: LOGO),
            ], Ct);

            Assert.Equal(renderAvatars, video.Count(29, avatarColor) > 0);
            Assert.Equal(renderAvatars, video.Count(29, defaultAvatarColor) > 0);
        }

        /// <summary>
        /// Badges are drawn only when ChatBadges is enabled, and badge types set in ChatBadgeMask are skipped; unknown badge ids/versions
        /// and a null badge list (old chats) are ignored rather than crashing. Guards the badge filter users rely on to hide e.g. mod badges.
        /// </summary>
        [Theory]
        [InlineData(true, (ChatBadgeType)0, true, true)]
        [InlineData(true, ChatBadgeType.Moderator, true, false)]
        [InlineData(false, (ChatBadgeType)0, false, false)]
        public async Task RenderVideoAsync_Badges_RespectEnableFlagAndMask(bool chatBadges, ChatBadgeType mask, bool expectSub, bool expectMod)
        {
            using var harness = new RenderHarness();
            harness.Options.ChatBadges = chatBadges;
            harness.Options.ChatBadgeMask = mask;
            var noBadgeList = RenderHarness.MakeComment(0, "old", "old chat");
            noBadgeList.message.user_badges = null;

            var video = await harness.RenderAsync(c => c.comments =
            [
                noBadgeList,
                RenderHarness.EmoteComment(0, badges:
                [
                    new UserBadge { _id = "subscriber", version = "0" }, new UserBadge { _id = "moderator", version = "1" },
                    new UserBadge { _id = "turbo", version = "1" }, new UserBadge { _id = "subscriber", version = "99" }
                ])
            ], Ct);

            Assert.Equal(expectSub, video.Count(29, FixtureColors.SubscriberBadge) > 0);
            Assert.Equal(expectMod, video.Count(29, FixtureColors.ModeratorBadge) > 0);
            Assert.True(video.Count(29, FixtureColors.FirstPartyEmote) > 0);
        }

        /// <summary>
        /// The outline option strokes text (usernames, timestamps, LTR and RTL messages) in black, so pixels darker than a mid-grey
        /// background appear only when it is enabled. Guards the outline paths in DrawText/DrawTimestamp, including the shaped RTL path.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RenderVideoAsync_Outline_DrawsDarkStrokeAroundText(bool outline)
        {
            var background = new SKColor(0xFF404040);
            using var harness = new RenderHarness();
            harness.Options.BackgroundColor = background;
            harness.Options.Outline = outline;
            harness.Options.Timestamp = true;

            var video = await harness.RenderAsync(c => c.comments = [RenderHarness.MakeComment(0, "alice", "hello world שלום עולם")], Ct);

            var darker = video.Count(29, p => p.Red < background.Red - 8);
            Assert.Equal(outline, darker > 0);
        }

        /// <summary>
        /// Message rows get the alternate background on odd comment indices and the highlight colour for highlighted users (matched
        /// case-insensitively), filling the full row width including the vertical padding. Guards GetMessageBackground's use in
        /// GenerateUpdateFrame and the row rectangle geometry.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_AlternateBackgroundsAndHighlightedUsers_FillMessageRows()
        {
            var alternate = new SKColor(0xFF191950);
            var highlight = new SKColor(0xFF503010);
            using var harness = new RenderHarness();
            harness.Options.AlternateMessageBackgrounds = true;
            harness.Options.AlternateBackgroundColor = alternate;
            harness.Options.HighlightUserColor = highlight;
            harness.Options.HighlightUsersArray = ["CAROL"];

            var video = await harness.RenderAsync(c => c.comments =
            [
                RenderHarness.MakeComment(0, "alice", "first"),
                RenderHarness.MakeComment(0, "bob", "second"),
                RenderHarness.MakeComment(0, "carol", "third"),
            ], Ct);

            const int X = 298;
            Assert.Equal(highlight, video.Pixel(29, X, 200 - 1 * ROW_PITCH + 10)); // carol, index 2
            Assert.Equal(alternate, video.Pixel(29, X, 200 - 2 * ROW_PITCH + 10)); // bob, index 1
            Assert.Equal(RenderHarness.Background, video.Pixel(29, X, 200 - 3 * ROW_PITCH + 10)); // alice, index 0
            Assert.Equal(RenderHarness.Background, video.Pixel(29, X, 10));
        }

        /// <summary>
        /// Renders every kind of highlighted/system message (subs and watch streaks with and without a custom message, including legacy
        /// split-name fragments, gifts, anonymous gifts, bits badge tiers, charity, channel point highlights, raids, combos, paying forward) plus
        /// messages that must be skipped (unknown user notice, missing fragments). The accent colours prove the highlight branches ran;
        /// with SubMessages off only the channel point highlight may remain. Guards every DrawXxxMessage path against crashes on real
        /// Twitch notice formats.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RenderVideoAsync_SystemMessages_DrawAccentsAndRespectSubMessages(bool subMessages)
        {
            using var harness = new RenderHarness();
            harness.Options.SubMessages = subMessages;
            harness.Options.ChatHeight = 600;
            harness.Options.StartOverride = 0;
            harness.Options.EndOverride = 1;

            var video = await harness.RenderAsync(c =>
            {
                var legacySub = RenderHarness.MakeComment(0, "carl", "Carl subscribed at Tier 2. They've subscribed for 3 months!", displayName: "Carl",
                    fragments: [new Fragment { text = "Carl" }, new Fragment { text = " subscribed at Tier 2. They've subscribed for 3 months!" }]);
                var channelPoints = RenderHarness.MakeComment(0, "ivy", "look at me", msgId: "highlighted-message");
                channelPoints.message.fragments = null;
                var noFragments = RenderHarness.MakeComment(0, "jay", "broken");
                noFragments.message.fragments = null;
                var legacyStreak = RenderHarness.MakeComment(0, "fred", "Fred watched 3 consecutive streams and sparked a watch streak! ", displayName: "Fred",
                    fragments: [new Fragment { text = "Fred" }, new Fragment { text = " watched 3 consecutive streams and sparked a watch streak! " }]);
                var splitBitsTier = RenderHarness.MakeComment(0, "oli", "bits badge tier notification ",
                    fragments: [new Fragment { text = "bits badge " }, new Fragment { text = "tier notification " }]);

                c.comments =
                [
                    RenderHarness.MakeComment(0, "ann", "an announcement", msgId: "announcement"),
                    noFragments,
                    legacySub,
                    RenderHarness.MakeComment(0, "bob", "Bob subscribed with Prime.", displayName: "Bob"),
                    RenderHarness.MakeComment(0, "dave", "Dave gifted a Tier 1 sub to Eve! ", displayName: "Dave"),
                    RenderHarness.MakeComment(0, "dave", "Dave is gifting 5 Tier 1 Subs to TestStreamer's community! ", displayName: "Dave"),
                    RenderHarness.MakeComment(0, "ananonymousgifter", "An anonymous user gifted a Tier 1 sub to Eve! ", displayName: "AnAnonymousGifter", userId: "274598607"),
                    RenderHarness.MakeComment(0, "eve", "Eve is continuing the Gift Sub they got from an anonymous user! ", displayName: "Eve"),
                    RenderHarness.MakeComment(0, "eve", "Eve is continuing the Gift Sub they got from Dave! ", displayName: "Eve"),
                    RenderHarness.MakeComment(0, "kim", "bits badge tier notification ", badges: [new UserBadge { _id = "bits", version = "1000" }]),
                    RenderHarness.MakeComment(0, "lee", "bits badge tier notification ", badges: [new UserBadge { _id = "bits", version = "100" }]),
                    RenderHarness.MakeComment(0, "max", "bits badge tier notification "),
                    splitBitsTier,
                    legacyStreak,
                    RenderHarness.MakeComment(0, "gina", "Gina: Donated 5.00 USD to support Charity! ", displayName: "Gina"),
                    RenderHarness.MakeComment(0, "hank", "15 raiders from Hank have joined! ", displayName: "Hank"),
                    RenderHarness.MakeComment(0, "nia", "Combo started! You have 30 seconds left to join. "),
                    channelPoints,
                    RenderHarness.MakeComment(0, "ivy", "look TestEmote StaticThird", msgId: "highlighted-message", fragments:
                    [
                        new Fragment { text = "look " }, new Fragment { text = "TestEmote", emoticon = new Emoticon { emoticon_id = "1001" } }, new Fragment { text = " StaticThird" }
                    ]),
                    RenderHarness.MakeComment(0, "alice", "Alice subscribed at Tier 1. They've subscribed for 5 months! hello there", displayName: "Alice", msgId: "resub"),
                    RenderHarness.MakeComment(0, "fred", "Fred watched 5 consecutive streams this month and sparked a watch streak! keep going", displayName: "Fred"),
                    RenderHarness.MakeComment(0, "eve", "Eve is paying forward the Gift they got from Dave to the community! ", displayName: "Eve"),
                ];
            }, Ct);

            var last = video.FrameCount - 1;
            Assert.True(video.Count(last, GrayAccent) > 0, "channel point highlight accent missing");
            Assert.True(video.Count(last, FixtureColors.Purple) > 0, "channel point word highlight missing");
            Assert.Equal(subMessages, video.Count(last, DarkAccent) > 0);
            Assert.Contains(harness.Progress.Verbose, x => x.Contains("invalid user_notice_params: announcement"));
            Assert.Contains(harness.Progress.Verbose, x => x.Contains("lacks fragments"));
        }

        /// <summary>
        /// Highlighted system messages (here a sub) should get the subtle grey highlight background when the message background is opaque,
        /// as the code comment in CombineImages states and as it did before #1601.
        /// </summary>
        [Fact(Skip = "Bug: ChatRenderer.CombineImages draws the sub-message highlight background only when the message background is NOT opaque (Alpha < 245); #1601 inverted the original condition.")]
        public async Task RenderVideoAsync_SubMessageOnOpaqueBackground_DrawsHighlightBackground()
        {
            using var harness = new RenderHarness();

            var video = await harness.RenderAsync(c => c.comments =
            [
                RenderHarness.MakeComment(0, "dave", "Dave gifted a Tier 1 sub to Eve! ", displayName: "Dave")
            ], Ct);

            // Right edge of the message area, inside the side padding, where no text is drawn.
            Assert.NotEqual(RenderHarness.Background, video.Pixel(29, 300 - 3 - 2, 200 - ROW_PITCH + 10));
        }

        /// <summary>
        /// Disabling BTTV, FFZ and 7TV must drop the embedded third-party emotes so their codes are drawn as plain text, while first-party
        /// emotes are unaffected. Guards the provider toggles users use to render "Twitch-only" chats.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_ThirdPartyProvidersDisabled_DrawsEmoteCodesAsText()
        {
            using var harness = new RenderHarness();
            harness.Options.BttvEmotes = false;
            harness.Options.FfzEmotes = false;
            harness.Options.StvEmotes = false;

            var video = await harness.RenderAsync(cancellationToken: Ct);

            Assert.True(video.Count(29, FixtureColors.FirstPartyEmote) > 0);
            Assert.Equal(0, video.Count(29, FixtureColors.StaticThirdParty));
            Assert.Equal(0, video.Count(29, FixtureColors.ZeroWidth));
            Assert.All(FixtureColors.AnimatedFrames, c => Assert.Equal(0, video.Count(29, c)));
        }

        /// <summary>
        /// An animated emote must show the GIF frame matching the video time. Frames last 100/200/300 ms (600 ms loop); at 7 fps the
        /// ticks fall at 0, 142, 285, 428, 571, 714 (→114), ... ms, never exactly on a frame boundary. Guards ComputeAnimFrameIndex and the
        /// cached composited frame that is reused while the frame index is unchanged (ticks 1-2, 3-4, ...).
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_AnimatedEmote_ShowsGifFrameForEachTick()
        {
            using var harness = new RenderHarness();
            harness.Options.Framerate = 7;
            harness.Options.StartOverride = 0;
            harness.Options.EndOverride = 2;

            var video = await harness.RenderAsync(c => c.comments = [RenderHarness.MakeComment(0, "alice", "AnimThird")], Ct);

            int[] expectedFrames = [0, 1, 1, 2, 2, 1, 1, 2, 2, 0, 1, 2, 2, 0];
            Assert.Equal(expectedFrames.Length, video.FrameCount);
            for (var tick = 0; tick < expectedFrames.Length; tick++)
            {
                var counts = FixtureColors.AnimatedFrames.Select(c => video.Count(tick, c)).ToArray();
                Assert.True(counts[expectedFrames[tick]] > 0, $"tick {tick}: expected frame {expectedFrames[tick]}, counts {string.Join(",", counts)}");
                Assert.Equal(counts[expectedFrames[tick]], counts.Sum());
            }
        }

        /// <summary>
        /// A static zero-width emote (e.g. a 7TV hat) placed after an animated emote must stay visible on top of it.
        /// </summary>
        [Fact(Skip = "Bug: static emotes are drawn into the update frame before animated emotes are composited, so a static zero-width emote over an animated emote is hidden underneath it.")]
        public async Task RenderVideoAsync_StaticZeroWidthOverAnimatedEmote_StaysOnTop()
        {
            using var harness = new RenderHarness();

            var video = await harness.RenderAsync(c => c.comments = [RenderHarness.MakeComment(0, "alice", "AnimThird ZeroWidth")], Ct);

            Assert.True(video.Count(29, FixtureColors.ZeroWidth) > 0);
        }

        /// <summary>
        /// Comment offsets are floored to the update rate and new comments only appear on update ticks: with a 1 s rate a comment at
        /// 1.5 s appears on tick 10, with a 2 s rate (floored to whole seconds, updated every 20 ticks) on tick 20, and with a rate of 0
        /// (no flooring, update every tick) on tick 15. Guards FloorCommentOffsets and the UpdateFrame cadence.
        /// </summary>
        [Theory]
        [InlineData(1.0, 10)]
        [InlineData(2.0, 20)]
        [InlineData(0.0, 15)]
        public async Task RenderVideoAsync_UpdateRate_ControlsWhenCommentsAppear(double updateRate, int firstVisibleTick)
        {
            using var harness = new RenderHarness();
            harness.Options.UpdateRate = updateRate;

            var video = await harness.RenderAsync(c => c.comments = [RenderHarness.EmoteComment(1.5)], Ct);

            Assert.Equal(0, video.Count(firstVisibleTick - 1, FixtureColors.FirstPartyEmote));
            Assert.True(video.Count(firstVisibleTick, FixtureColors.FirstPartyEmote) > 0);
        }

        /// <summary>
        /// A single comment containing ordinary words, a word wider than the chat, '-' delimited text, block art, CJK, Arabic and Hebrew
        /// (shaped RTL), words mixing in-font and fallback-font characters, astral-plane letters, an invisible U+E0000, a variation
        /// selector, a first-party emote missing from the chat file, and long runs of first-party, third-party and cheer emotes must wrap
        /// onto many lines, with each kind of emote continuing on a new line. Guards DrawText's wrapping/SubstringToTextWidth, the
        /// block-art pre-wrap, the fallback-font (DrawNonFontMessage) paths and image wrapping.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_LongAndUnusualText_WrapsOntoSeveralLines()
        {
            using var harness = new RenderHarness();
            harness.Options.ChatWidth = 400; // wider than the block art pre-wrap width at 12pt
            harness.Options.ChatHeight = 400;
            harness.Options.StartOverride = 0;
            harness.Options.EndOverride = 1;
            var fragments = new List<Fragment>
            {
                new()
                {
                    text = string.Join(' ',
                        "the quick brown fox jumps over the lazy dog",
                        new string('W', 80),
                        string.Join('-', Enumerable.Repeat("abcd", 30)),
                        new string('⣿', 40),
                        "漢字かな مرحبا بالعالم שלום a漢b x\U0001D518 漢\U0001D518",
                        "\U0001D518\U0001D52B\U0001D526 \U000E0000x ☺\uFE0F ")
                },
                new() { text = "MissingEmote", emoticon = new Emoticon { emoticon_id = "9999" } },
            };
            for (var i = 0; i < 15; i++)
            {
                fragments.Add(new Fragment { text = " " });
                fragments.Add(new Fragment { text = "TestEmote", emoticon = new Emoticon { emoticon_id = "1001" } });
            }

            fragments.Add(new Fragment { text = " " + string.Join(' ', Enumerable.Repeat("StaticThird", 15)) + " " + string.Join(' ', Enumerable.Repeat("Cheer1", 15)) });
            var body = string.Concat(fragments.Select(f => f.text));

            var video = await harness.RenderAsync(c => c.comments = [RenderHarness.MakeComment(0, "alice", body, fragments, bits: 15)], Ct);

            Assert.InRange(video.FirstNonBackgroundRow(9, RenderHarness.Background), 0, 400 - 10 * 20); // at least ten 20px lines tall
            foreach (var color in new[] { FixtureColors.FirstPartyEmote, FixtureColors.StaticThirdParty, FixtureColors.Cheer1 })
            {
                var rows = Enumerable.Range(0, 400).Where(y => Enumerable.Range(0, 400).Any(x => video.Pixel(9, x, y) == color)).ToList();
                Assert.True(rows.Count > 0 && rows[^1] - rows[0] >= 20, $"{color} does not span several lines");
            }
        }

        /// <summary>
        /// When the chat is so narrow (or the font so large) that a single character is wider than a line, each character must be placed
        /// on its own line instead of retrying forever. Guards the overrideWrap escape in DrawText against an infinite loop.
        /// </summary>
        [Fact(Timeout = 30_000)]
        public async Task RenderVideoAsync_CharacterWiderThanChat_IsDrawnWithoutHanging()
        {
            using var harness = new RenderHarness();
            harness.Options.ChatWidth = 40;
            harness.Options.ChatHeight = 400;
            harness.Options.FontSize = 48;

            var video = await harness.RenderAsync(c => c.comments = [RenderHarness.MakeComment(0, "a", "WWW")], Ct);

            Assert.Equal(30, video.FrameCount);
            Assert.True(video.FirstNonBackgroundRow(29, RenderHarness.Background) >= 0);
        }

        /// <summary>
        /// After a word made of characters wider than the chat has been placed character by character, the message must end on the line
        /// with its last character, not on an extra blank line (which pushes the text out of a short chat).
        /// </summary>
        [Fact(Skip = "Bug: when DrawText's overrideWrap loop consumes the whole word, it still runs the line-overflow check with the empty remainder and drawPos past the line end, so AddImageSection appends a blank line after the word.")]
        public async Task RenderVideoAsync_CharacterWiderThanChat_DoesNotAddTrailingBlankLine()
        {
            using var harness = new RenderHarness();
            harness.Options.ChatWidth = 40;
            harness.Options.ChatHeight = 100; // only the comment's last 80px line (+48px padding) is visible
            harness.Options.FontSize = 48;

            var video = await harness.RenderAsync(c => c.comments = [RenderHarness.MakeComment(0, "a", "WWW")], Ct);

            Assert.True(video.FirstNonBackgroundRow(29, RenderHarness.Background) >= 0);
        }

        /// <summary>
        /// "Zalgo" text (an ASCII letter followed by combining marks, very common in Twitch chat) must render.
        /// </summary>
        [Fact(Skip = "Bug: ReadOnlySpanExtensions.LengthInTextElements counts an ASCII letter and its following combining marks as separate elements, so its Debug.Assert fails and Debug builds of the renderer throw on text like \"e\\u0301\".")]
        public async Task RenderVideoAsync_AsciiLetterWithCombiningMarks_Renders()
        {
            using var harness = new RenderHarness();

            var video = await harness.RenderAsync(c => c.comments = [RenderHarness.MakeComment(0, "alice", "Z\u0337\u0322a\u0337l\u0337g\u0337o\u0337 e\u0301")], Ct);

            Assert.True(video.FirstNonBackgroundRow(29, RenderHarness.Background) >= 0);
        }

        /// <summary>
        /// EmoteScale and BadgeScale must scale those images (2x → twice as wide) independently of the font size. Widths are compared
        /// because images taller than the 20px section are vertically cropped. Guards the scale options in GetScaledEmotes/GetScaledBadges.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_EmoteAndBadgeScale_ScaleImages()
        {
            var comments = () => new List<Comment> { RenderHarness.EmoteComment(0, badges: [new UserBadge { _id = "subscriber", version = "0" }]) };
            using var normal = new RenderHarness();
            var small = await normal.RenderAsync(c => c.comments = comments(), Ct);

            using var scaled = new RenderHarness();
            scaled.Options.EmoteScale = 2;
            scaled.Options.BadgeScale = 2;
            var big = await scaled.RenderAsync(c => c.comments = comments(), Ct);

            Assert.Equal(28, small.ColumnCount(29, FixtureColors.FirstPartyEmote));
            Assert.Equal(56, big.ColumnCount(29, FixtureColors.FirstPartyEmote));
            Assert.Equal(18, small.ColumnCount(29, FixtureColors.SubscriberBadge));
            Assert.Equal(36, big.ColumnCount(29, FixtureColors.SubscriberBadge));
        }

        /// <summary>
        /// A font family name other than "Inter Embedded" is resolved through the system font manager (falling back to the default
        /// typeface when missing) and still renders text. Guards the non-embedded font branch.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_SystemFontFamily_RendersText()
        {
            using var harness = new RenderHarness();
            harness.Options.Font = "DejaVu Sans";

            var video = await harness.RenderAsync(c => c.comments = [RenderHarness.MakeComment(0, "alice", "hello")], Ct);

            Assert.True(video.FirstNonBackgroundRow(29, RenderHarness.Background) >= 0);
        }

        /// <summary>
        /// Starting a render deep into a long chat must only lay out the last 100 comments before the start (not all of them) and still
        /// show the newest comment at the bottom. Guards the "skip to 100 before" optimisation and the disposal of rows scrolled off-screen.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_StartDeepIntoLongChat_ShowsNewestComments()
        {
            using var harness = new RenderHarness();
            harness.Options.StartOverride = 149;
            harness.Options.EndOverride = 150;

            var video = await harness.RenderAsync(c => c.comments = Enumerable.Range(0, 150).Select(i => RenderHarness.EmoteComment(i)).ToList(), Ct);

            Assert.Equal(10, video.FrameCount);
            Assert.True(video.Count(0, FixtureColors.FirstPartyEmote) > 0);
            Assert.True(video.Count(0, p => p != RenderHarness.Background, 0, 20) > 0, "frame should be full of comments");
        }

        /// <summary>
        /// An already-cancelled token must abort before any ffmpeg process starts and leave no (empty) output file behind.
        /// Guards the cleanup in RenderVideoAsync's catch block that removes the claimed placeholder file.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_CancelledBeforeStart_ThrowsAndRemovesOutputFile()
        {
            using var harness = new RenderHarness();
            using var renderer = new ChatRenderer(harness.Options, harness.Progress);
            await renderer.ParseJsonAsync(Ct);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renderer.RenderVideoAsync(new CancellationToken(true)));
            Assert.False(File.Exists(harness.Options.OutputFile));
        }

        /// <summary>
        /// Cancelling while frames are being piped to ffmpeg must stop the render with an OperationCanceledException (the GUI's cancel
        /// button) rather than hang or finish. Guards the per-tick cancellation check and the ffmpeg process disposal on failure.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_CancelledMidRender_Throws()
        {
            using var harness = new RenderHarness();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            using var renderer = new ChatRenderer(harness.Options, new CancelOnProgress(cts));
            await renderer.ParseJsonAsync(Ct);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renderer.RenderVideoAsync(cts.Token));
        }

        /// <summary>
        /// If ffmpeg rejects the output arguments and exits, the render must fail with an IOException (broken pipe) instead of
        /// reporting success. Guards against silently producing no video when the user supplies bad ffmpeg arguments.
        /// </summary>
        [Fact]
        public async Task RenderVideoAsync_FfmpegRejectsArguments_ThrowsIOException()
        {
            using var harness = new RenderHarness();
            harness.Options.OutputArgs = "-not_a_real_option 1 \"{save_path}\""; // rejected while parsing, before ffmpeg reads any input

            await Assert.ThrowsAnyAsync<IOException>(() => harness.RenderOnlyAsync(cancellationToken: Ct));
            Assert.False(File.Exists(harness.Options.OutputFile));
        }

        /// <summary>An <see cref="ITaskProgress"/> that cancels the render the first time progress is reported.</summary>
        private sealed class CancelOnProgress(CancellationTokenSource cts) : ITaskProgress
        {
            public void ReportProgress(int percent) => cts.Cancel();
            public void ReportProgress(int percent, TimeSpan time1, TimeSpan time2) => cts.Cancel();
            public void SetStatus(string status) { }
            public void SetTemplateStatus(string statusTemplate, int initialPercent) { }
            public void SetTemplateStatus(string statusTemplate, int initialPercent, TimeSpan initialTime1, TimeSpan initialTime2) { }
            public void LogVerbose(string logMessage) { }
            public void LogVerbose(DefaultInterpolatedStringHandler logMessage) { }
            public void LogInfo(string logMessage) { }
            public void LogInfo(DefaultInterpolatedStringHandler logMessage) { }
            public void LogWarning(string logMessage) { }
            public void LogWarning(DefaultInterpolatedStringHandler logMessage) { }
            public void LogError(string logMessage) { }
            public void LogError(DefaultInterpolatedStringHandler logMessage) { }
            public void LogFfmpeg(string logMessage) { }
        }
    }
}
