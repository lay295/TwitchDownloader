using CommandLine;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tests.CliTestHelpers;

namespace TwitchDownloaderCLI.Tests.ArgumentTests
{
    // Tests the option declarations of the `chatrender` verb (ChatRenderArgs). It has by far the most options of any verb, and its defaults
    // (colors, sizes, ffmpeg arguments) define what a render looks like for users who pass nothing but -i/-o.
    public class ChatRenderArgsTests
    {
        // With only the required options, every other option must take its documented default. The trim times must stay at -1s
        // (meaning "no override" in ChatRenderer); a 0 default would force every render to start at 0:00.
        [Fact]
        public void UsesDocumentedDefaults()
        {
            var args = CliArgs.Parse<ChatRenderArgs>("chatrender", "-i", "chat.json", "-o", "chat.mp4");

            Assert.Equal("chat.json", args.InputFile);
            Assert.Equal("chat.mp4", args.OutputFile);
            Assert.Equal("#111111", args.BackgroundColor);
            Assert.Equal("#191919", args.AlternateBackgroundColor);
            Assert.Equal("#C8FF0059", args.HighlightUserColor);
            Assert.Equal("#ffffff", args.MessageColor);
            Assert.Equal(350, args.ChatWidth);
            Assert.Equal(600, args.ChatHeight);
            Assert.Equal(TimeDuration.MinusOneSeconds, args.TrimBeginningTime);
            Assert.Equal(TimeDuration.MinusOneSeconds, args.TrimEndingTime);
            Assert.True(args.BttvEmotes);
            Assert.True(args.FfzEmotes);
            Assert.True(args.StvEmotes);
            Assert.True(args.AllowUnlistedEmotes);
            Assert.True(args.SubMessages);
            Assert.True(args.ChatBadges);
            Assert.False(args.Outline);
            Assert.Equal(4, args.OutlineSize);
            Assert.Equal("Inter Embedded", args.Font);
            Assert.Equal(12, args.FontSize);
            Assert.Equal("normal", args.MessageFontStyle);
            Assert.Equal("bold", args.UsernameFontStyle);
            Assert.False(args.Timestamp);
            Assert.False(args.GenerateMask);
            Assert.False(args.Sharpening);
            Assert.Equal(30, args.Framerate);
            Assert.Equal(0.2, args.UpdateRate);
            Assert.Equal("-framerate {fps} -f rawvideo -analyzeduration {max_int} -probesize {max_int} -pix_fmt {pix_fmt} -video_size {width}x{height} -i -", args.InputArgs);
            Assert.Equal("-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p \"{save_path}\"", args.OutputArgs);
            Assert.Equal("", args.IgnoreUsersString);
            Assert.Equal("", args.BannedWordsString);
            Assert.Equal("", args.HighlightUsersString);
            Assert.Equal(0, args.BadgeFilterMask);
            Assert.False(args.DisperseCommentOffsets);
            Assert.False(args.AlternateMessageBackgrounds);
            Assert.True(args.AdjustUsernameVisibility);
            Assert.False(args.RenderUserAvatars);
            Assert.False(args.Offline);
            Assert.Equal("notocolor", args.EmojiVendor);
            Assert.Null(args.FfmpegPath);
            Assert.Equal("", args.TempFolder);
            Assert.False(args.SkipDriveWaiting);
            Assert.All(
                new[]
                {
                    args.ScaleEmote, args.ScaleBadge, args.ScaleEmoji, args.ScaleAvatar, args.ScaleUsername, args.ScaleVertical, args.ScaleLeft,
                    args.ScaleSectionHeight, args.ScaleWordSpace, args.ScaleEmoteSpace, args.ScaleAccentStroke, args.ScaleAccentIndent
                },
                scale => Assert.Equal(1.0, scale));
            Assert.Equal(OverwriteBehavior.Prompt, args.OverwriteBehavior);
            Assert.True(args.ShowBanner);
            Assert.Equal(LogLevel.Status | LogLevel.Info | LogLevel.Warning | LogLevel.Error, args.LogLevel);
        }

        // Every option must land in its own property. With this many similarly named options (12 scale-* flags) a copy-paste
        // mistake mapping two flags to the same property is easy to make, so each gets a distinct value here.
        [Fact]
        public void ParsesEveryOption()
        {
            var args = CliArgs.Parse<ChatRenderArgs>(
                "chatrender", "--input", "chat.json", "--output", "chat.webm",
                "--background-color", "#00000000", "--alt-background-color", "#01010101", "--highlight-user-color", "#02020202", "--message-color", "#030303",
                "-w", "500", "-h", "800", "-b", "5s", "-e", "1:00",
                "--bttv", "false", "--ffz", "false", "--stv", "false", "--allow-unlisted-emotes", "false", "--sub-messages", "false", "--badges", "false",
                "--outline", "--outline-size", "2.5", "-f", "Arial", "--font-size", "24.5", "--message-fontstyle", "italic", "--username-fontstyle", "normal",
                "--timestamp", "--generate-mask", "--sharpening", "--framerate", "60", "--update-rate", "0.5",
                "--input-args", "in-args", "--output-args", "out-args",
                "--ignore-users", "a,b", "--ban-words", "c,d", "--highlight-users", "e,f", "--badge-filter", "6",
                "--dispersion", "--alternate-backgrounds", "--readable-colors", "false", "--avatars", "--offline", "--emoji-vendor", "twemoji",
                "--ffmpeg-path", "ff", "--temp-path", "tmp", "--skip-drive-waiting",
                "--scale-emote", "1.1", "--scale-badge", "1.2", "--scale-emoji", "1.3", "--scale-avatar", "1.4", "--scale-username", "1.5",
                "--scale-vertical", "1.6", "--scale-side-padding", "1.7", "--scale-section-height", "1.8", "--scale-word-space", "1.9",
                "--scale-emote-space", "2.1", "--scale-highlight-stroke", "2.2", "--scale-highlight-indent", "2.3",
                "--collision", "Overwrite", "--banner", "false", "--log-level", "Status,Ffmpeg");

            Assert.Equal("chat.json", args.InputFile);
            Assert.Equal("chat.webm", args.OutputFile);
            Assert.Equal("#00000000", args.BackgroundColor);
            Assert.Equal("#01010101", args.AlternateBackgroundColor);
            Assert.Equal("#02020202", args.HighlightUserColor);
            Assert.Equal("#030303", args.MessageColor);
            Assert.Equal(500, args.ChatWidth);
            Assert.Equal(800, args.ChatHeight);
            Assert.Equal(TimeSpan.FromSeconds(5), (TimeSpan)args.TrimBeginningTime);
            Assert.Equal(TimeSpan.FromMinutes(1), (TimeSpan)args.TrimEndingTime);
            Assert.False(args.BttvEmotes);
            Assert.False(args.FfzEmotes);
            Assert.False(args.StvEmotes);
            Assert.False(args.AllowUnlistedEmotes);
            Assert.False(args.SubMessages);
            Assert.False(args.ChatBadges);
            Assert.True(args.Outline);
            Assert.Equal(2.5, args.OutlineSize);
            Assert.Equal("Arial", args.Font);
            Assert.Equal(24.5, args.FontSize);
            Assert.Equal("italic", args.MessageFontStyle);
            Assert.Equal("normal", args.UsernameFontStyle);
            Assert.True(args.Timestamp);
            Assert.True(args.GenerateMask);
            Assert.True(args.Sharpening);
            Assert.Equal(60, args.Framerate);
            Assert.Equal(0.5, args.UpdateRate);
            Assert.Equal("in-args", args.InputArgs);
            Assert.Equal("out-args", args.OutputArgs);
            Assert.Equal("a,b", args.IgnoreUsersString);
            Assert.Equal("c,d", args.BannedWordsString);
            Assert.Equal("e,f", args.HighlightUsersString);
            Assert.Equal(6, args.BadgeFilterMask);
            Assert.True(args.DisperseCommentOffsets);
            Assert.True(args.AlternateMessageBackgrounds);
            Assert.False(args.AdjustUsernameVisibility);
            Assert.True(args.RenderUserAvatars);
            Assert.True(args.Offline);
            Assert.Equal("twemoji", args.EmojiVendor);
            Assert.Equal("ff", args.FfmpegPath);
            Assert.Equal("tmp", args.TempFolder);
            Assert.True(args.SkipDriveWaiting);
            Assert.Equal(1.1, args.ScaleEmote);
            Assert.Equal(1.2, args.ScaleBadge);
            Assert.Equal(1.3, args.ScaleEmoji);
            Assert.Equal(1.4, args.ScaleAvatar);
            Assert.Equal(1.5, args.ScaleUsername);
            Assert.Equal(1.6, args.ScaleVertical);
            Assert.Equal(1.7, args.ScaleLeft);
            Assert.Equal(1.8, args.ScaleSectionHeight);
            Assert.Equal(1.9, args.ScaleWordSpace);
            Assert.Equal(2.1, args.ScaleEmoteSpace);
            Assert.Equal(2.2, args.ScaleAccentStroke);
            Assert.Equal(2.3, args.ScaleAccentIndent);
            Assert.Equal(OverwriteBehavior.Overwrite, args.OverwriteBehavior);
            Assert.False(args.ShowBanner);
            Assert.Equal(LogLevel.Status | LogLevel.Ffmpeg, args.LogLevel);
        }

        // --input and --output are required.
        [Theory]
        [InlineData("chatrender", "-o", "chat.mp4")]
        [InlineData("chatrender", "-i", "chat.json")]
        public void MissingRequiredOption_IsAnError(params string[] argv)
        {
            Assert.Contains(ErrorType.MissingRequiredOptionError, CliArgs.Errors(argv));
        }
    }
}
