using SkiaSharp;
using TwitchDownloaderCLI.Modes;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCLI.Tests.ModeTests
{
    // Tests RenderChat.GetRenderOptions, which converts the ~60 `chatrender` arguments into Core's ChatRenderOptions: color parsing, font
    // style and emoji vendor names, odd-dimension rounding, comma-separated user/word lists and sanity warnings. Render() is only tested
    // for a corrupt input (a real render needs ffmpeg and is covered elsewhere); the missing-ffmpeg branch calls Environment.Exit.
    public class RenderChatTests
    {
        private static ChatRenderOptions Map(RecordingTaskProgress logger, params string[] extraArgs)
        {
            var args = CliArgs.Parse<ChatRenderArgs>(["chatrender", "-i", "chat.json", "-o", "chat.mp4", ..extraArgs]);
            return RenderChat.GetRenderOptions(args, new FileCollisionHandler(args, logger), logger);
        }

        private static ChatRenderOptions Map(params string[] extraArgs) => Map(new RecordingTaskProgress(), extraArgs);

        // With default arguments the documented look must come out: dark background, 350x600, no trim override, bold usernames,
        // Noto Color emojis, the stock ffmpeg arguments and no warnings.
        [Fact]
        public void MapsDefaults()
        {
            var logger = new RecordingTaskProgress();

            var options = Map(logger);

            Assert.Equal("chat.json", options.InputFile);
            Assert.Equal("chat.mp4", options.OutputFile);
            Assert.Equal(new SKColor(0x11, 0x11, 0x11), options.BackgroundColor);
            Assert.Equal(new SKColor(0x19, 0x19, 0x19), options.AlternateBackgroundColor);
            Assert.Equal(new SKColor(0xFF, 0x00, 0x59, 0xC8), options.HighlightUserColor);
            Assert.Equal(SKColors.White, options.MessageColor);
            Assert.Equal(350, options.ChatWidth);
            Assert.Equal(600, options.ChatHeight);
            Assert.Equal(-1, options.StartOverride);
            Assert.Equal(-1, options.EndOverride);
            Assert.Equal(SKFontStyle.Normal.Weight, options.MessageFontStyle.Weight);
            Assert.Equal(SKFontStyle.Bold.Weight, options.UsernameFontStyle.Weight);
            Assert.Equal(EmojiVendor.GoogleNotoColor, options.EmojiVendor);
            Assert.Equal(FfmpegHandler.FfmpegExecutableName, options.FfmpegPath);
            Assert.Equal("-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", options.InputArgs);
            Assert.Empty(options.IgnoreUsersArray);
            Assert.Empty(options.BannedWordsArray);
            Assert.Empty(options.HighlightUsersArray);
            Assert.Equal((ChatBadgeType)0, options.ChatBadgeMask);
            Assert.True(options.AdjustUsernameVisibility);
            Assert.Empty(logger.Warnings);
        }

        // Every remaining argument must reach its Core option. Many are same-typed doubles/bools, so each gets a distinct value
        // to catch two options being wired to the same property.
        [Fact]
        public void MapsAllOptions()
        {
            var options = Map(
                "--background-color", "#80102030", "--alt-background-color", "#405060", "--highlight-user-color", "#708090", "--message-color", "#A0B0C0",
                "-w", "400", "-h", "700", "-b", "90s", "-e", "1:00:00",
                "--bttv", "false", "--ffz", "false", "--stv", "false", "--allow-unlisted-emotes", "false", "--sub-messages", "false", "--badges", "false",
                "--outline", "--outline-size", "3", "-f", "Arial", "--font-size", "20", "--timestamp", "--framerate", "50", "--update-rate", "0.1",
                "--output-args", "out-args", "--badge-filter", "6", "--dispersion", "--alternate-backgrounds", "--readable-colors", "false",
                "--avatars", "--offline", "--temp-path", "cache", "--skip-drive-waiting", "--ffmpeg-path", "ff",
                "--scale-emote", "1.1", "--scale-badge", "1.2", "--scale-emoji", "1.3", "--scale-avatar", "1.4", "--scale-username", "1.5",
                "--scale-vertical", "1.6", "--scale-side-padding", "1.7", "--scale-section-height", "1.8", "--scale-word-space", "1.9",
                "--scale-emote-space", "2.1", "--scale-highlight-stroke", "2.2", "--scale-highlight-indent", "2.3");

            Assert.Equal(new SKColor(0x10, 0x20, 0x30, 0x80), options.BackgroundColor);
            Assert.Equal(new SKColor(0x40, 0x50, 0x60), options.AlternateBackgroundColor);
            Assert.Equal(new SKColor(0x70, 0x80, 0x90), options.HighlightUserColor);
            Assert.Equal(new SKColor(0xA0, 0xB0, 0xC0), options.MessageColor);
            Assert.Equal(400, options.ChatWidth);
            Assert.Equal(700, options.ChatHeight);
            Assert.Equal(90, options.StartOverride);
            Assert.Equal(3600, options.EndOverride);
            Assert.False(options.BttvEmotes);
            Assert.False(options.FfzEmotes);
            Assert.False(options.StvEmotes);
            Assert.False(options.AllowUnlistedEmotes);
            Assert.False(options.SubMessages);
            Assert.False(options.ChatBadges);
            Assert.True(options.Outline);
            Assert.Equal(3, options.OutlineSize);
            Assert.Equal("Arial", options.Font);
            Assert.Equal(20, options.FontSize);
            Assert.True(options.Timestamp);
            Assert.Equal(50, options.Framerate);
            Assert.Equal(0.1, options.UpdateRate);
            Assert.Equal("out-args", options.OutputArgs);
            Assert.Equal(ChatBadgeType.Broadcaster | ChatBadgeType.Moderator, options.ChatBadgeMask);
            Assert.True(options.DisperseCommentOffsets);
            Assert.True(options.AlternateMessageBackgrounds);
            Assert.False(options.AdjustUsernameVisibility);
            Assert.True(options.RenderUserAvatars);
            Assert.True(options.Offline);
            Assert.Equal("cache", options.TempFolder);
            Assert.True(options.SkipDriveWaiting);
            Assert.Equal(Path.GetFullPath("ff"), options.FfmpegPath);
            Assert.Equal(1.1, options.EmoteScale);
            Assert.Equal(1.2, options.BadgeScale);
            Assert.Equal(1.3, options.EmojiScale);
            Assert.Equal(1.4, options.AvatarScale);
            Assert.Equal(1.5, options.UsernameFontScale);
            Assert.Equal(1.6, options.VerticalSpacingScale);
            Assert.Equal(1.7, options.SidePaddingScale);
            Assert.Equal(1.8, options.SectionHeightScale);
            Assert.Equal(1.9, options.WordSpacingScale);
            Assert.Equal(2.1, options.EmoteSpacingScale);
            Assert.Equal(2.2, options.AccentStrokeScale);
            Assert.Equal(2.3, options.AccentIndentScale);
        }

        // Font style names (and their "italics" spellings) are matched case-insensitively for both message and username fonts.
        [Theory]
        [InlineData("normal", false, false)]
        [InlineData("BOLD", true, false)]
        [InlineData("italic", false, true)]
        [InlineData("italics", false, true)]
        [InlineData("BoldItalic", true, true)]
        [InlineData("bolditalics", true, true)]
        public void ParsesFontStyles(string style, bool bold, bool italic)
        {
            var options = Map("--message-fontstyle", style, "--username-fontstyle", style);

            Assert.All(new[] { options.MessageFontStyle, options.UsernameFontStyle }, fontStyle =>
            {
                Assert.Equal(bold ? SKFontStyle.Bold.Weight : SKFontStyle.Normal.Weight, fontStyle.Weight);
                Assert.Equal(italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright, fontStyle.Slant);
            });
        }

        // An unknown font style must fail with the list of valid values instead of rendering with an arbitrary style.
        [Theory]
        [InlineData("--message-fontstyle")]
        [InlineData("--username-fontstyle")]
        public void InvalidFontStyle_Throws(string option)
        {
            var ex = Assert.Throws<NotSupportedException>(() => Map(option, "oblique"));
            Assert.Contains("Valid values are", ex.Message);
        }

        // Each documented emoji vendor alias maps to its vendor; anything else is rejected.
        [Theory]
        [InlineData("twitter", EmojiVendor.TwitterTwemoji)]
        [InlineData("TWEMOJI", EmojiVendor.TwitterTwemoji)]
        [InlineData("google", EmojiVendor.GoogleNotoColor)]
        [InlineData("notocolor", EmojiVendor.GoogleNotoColor)]
        [InlineData("system", EmojiVendor.None)]
        [InlineData("none", EmojiVendor.None)]
        public void ParsesEmojiVendor(string vendor, EmojiVendor expected)
        {
            Assert.Equal(expected, Map("--emoji-vendor", vendor).EmojiVendor);
        }

        // An unknown emoji vendor must be rejected with a helpful message.
        [Fact]
        public void InvalidEmojiVendor_Throws()
        {
            Assert.Throws<NotSupportedException>(() => Map("--emoji-vendor", "apple"));
        }

        // An invalid color string must fail while building the options, not half-way through a render.
        [Fact]
        public void InvalidColor_Throws()
        {
            Assert.ThrowsAny<ArgumentException>(() => Map("--background-color", "not-a-color"));
        }

        // --sharpening appends ffmpeg's smartblur filter to the (possibly custom) input arguments.
        [Fact]
        public void Sharpening_AppendsFilterToInputArgs()
        {
            Assert.Equal("custom -filter_complex \"smartblur=lr=1:ls=-1.0\"", Map("--sharpening", "--input-args", "custom").InputArgs);
        }

        // libx264 rejects odd frame sizes, so odd widths/heights are rounded up to the next even number with a warning.
        [Theory]
        [InlineData("351", "600", 352, 600)]
        [InlineData("350", "601", 350, 602)]
        [InlineData("351", "601", 352, 602)]
        public void OddDimensions_AreRoundedUpWithWarning(string width, string height, int expectedWidth, int expectedHeight)
        {
            var logger = new RecordingTaskProgress();

            var options = Map(logger, "-w", width, "-h", height);

            Assert.Equal(expectedWidth, options.ChatWidth);
            Assert.Equal(expectedHeight, options.ChatHeight);
            Assert.Contains(logger.Warnings, w => w.Contains("MUST be even"));
        }

        // A mask is only useful with a transparent background, so --generate-mask with an opaque background warns; it must not warn
        // when the background is transparent, or when alternating backgrounds use a transparent alternate color.
        [Theory]
        [InlineData(true, "--background-color", "#111111")]
        [InlineData(false, "--background-color", "#00000000")]
        [InlineData(false, "--alternate-backgrounds", "--alt-background-color", "#00000000")]
        public void GenerateMask_WarnsOnlyForOpaqueBackground(bool expectWarning, params string[] extraArgs)
        {
            var logger = new RecordingTaskProgress();

            Map(logger, ["--generate-mask", ..extraArgs]);

            Assert.Equal(expectWarning, logger.Warnings.Any(w => w.Contains("Generate mask")));
        }

        // Comma-separated user and word lists are split, trimmed and stripped of empty entries.
        [Fact]
        public void SplitsCommaSeparatedLists()
        {
            var options = Map("--ignore-users", "Nightbot, StreamElements,,", "--ban-words", "spoiler , bad phrase", "--highlight-users", " vip ");

            Assert.Equal(["Nightbot", "StreamElements"], options.IgnoreUsersArray);
            Assert.Equal(["spoiler", "bad phrase"], options.BannedWordsArray);
            Assert.Equal(["vip"], options.HighlightUsersArray);
        }

        // The verb entry point surfaces a corrupt chat JSON as an error before ffmpeg is started or an output file is created.
        [Fact]
        public void Render_CorruptInput_ThrowsWithoutOutput()
        {
            using var temp = new TempDirectory();
            var input = temp.Combine("chat.json");
            File.WriteAllText(input, "this is not json");
            var ffmpeg = temp.Combine(FfmpegHandler.FfmpegExecutableName);
            File.WriteAllText(ffmpeg, "");
            var output = temp.Combine("chat.mp4");

            var ex = Assert.Throws<AggregateException>(() => RenderChat.Render(CliArgs.Parse<ChatRenderArgs>(
                "chatrender", "-i", input, "-o", output, "--ffmpeg-path", ffmpeg, "--temp-path", temp.Path, "--collision", "Overwrite", "--log-level", "None")));

            Assert.Contains("is not a valid chat format", Assert.IsType<NotSupportedException>(ex.InnerException).Message);
            Assert.False(File.Exists(output));
        }
    }
}
