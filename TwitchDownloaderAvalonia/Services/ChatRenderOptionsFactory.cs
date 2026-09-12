using System.Text.Json;
using SkiaSharp;
using TwitchDownloaderCore.Chat;
using TwitchDownloaderCore.TwitchObjects;
using Color = Avalonia.Media.Color;

namespace TwitchDownloaderAvalonia.Services
{
    internal sealed class ChatRenderBuildArgs
    {
        public required string InputFile { get; init; }
        public required string OutputFile { get; init; }
        public required string BackgroundColorHex { get; init; }
        public required string AlternateBackgroundColorHex { get; init; }
        public required string FontColorHex { get; init; }
        public required string HighlightUsersColorHex { get; init; }
        public required bool AlternateMessageBackgrounds { get; init; }
        public required int ChatHeight { get; init; }
        public required int ChatWidth { get; init; }
        public required bool BttvEmotes { get; init; }
        public required bool FfzEmotes { get; init; }
        public required bool StvEmotes { get; init; }
        public required bool Outline { get; init; }
        public required string Font { get; init; }
        public required double FontSize { get; init; }
        public required double UpdateRate { get; init; }
        public required double EmoteScale { get; init; }
        public required double BadgeScale { get; init; }
        public required double EmojiScale { get; init; }
        public required double AvatarScale { get; init; }
        public required double SidePaddingScale { get; init; }
        public required double SectionHeightScale { get; init; }
        public required double WordSpacingScale { get; init; }
        public required double EmoteSpacingScale { get; init; }
        public required double AccentIndentScale { get; init; }
        public required double AccentStrokeScale { get; init; }
        public required double VerticalSpacingScale { get; init; }
        public required double UsernameFontScale { get; init; }
        public required double OutlineScale { get; init; }
        public required string HighlightUsersList { get; init; }
        public required string IgnoreUsersList { get; init; }
        public required string BannedWordsList { get; init; }
        public required bool Timestamp { get; init; }
        public required int Framerate { get; init; }
        public required string FfmpegInputArgs { get; init; }
        public required string FfmpegOutputArgs { get; init; }
        public required bool Sharpening { get; init; }
        public required bool GenerateMask { get; init; }
        public required string TempFolder { get; init; }
        public required bool SubMessages { get; init; }
        public required bool ChatBadges { get; init; }
        public required bool Offline { get; init; }
        public required bool RenderUserAvatars { get; init; }
        public required bool DisperseCommentOffsets { get; init; }
        public required bool AdjustUsernameVisibility { get; init; }
        public required EmojiVendor EmojiVendor { get; init; }
        public required ChatBadgeType ChatBadgeMask { get; init; }
        public required int StartOverride { get; init; }
        public required int EndOverride { get; init; }
    }

    internal static class ChatRenderOptionsFactory
    {
        public static string ContainerExtension(AppSettings settings)
        {
            var containers = RenderEncodingPresets.CreateContainers();
            var container = containers.FirstOrDefault(item => item.Name == settings.RenderVideoContainer)
                            ?? containers[0];
            return container.Name.ToLowerInvariant();
        }

        public static ChatRenderOptions FromSettings(
            AppSettings settings,
            string inputFile,
            string outputFile,
            string ffmpegPath,
            Func<FileInfo, FileInfo> collisionCallback)
        {
            var (inputArgs, outputArgs) = ResolveFfmpegArgs(settings);
            var emojiVendor = Enum.IsDefined(typeof(EmojiVendor), settings.RenderEmojiVendor)
                ? (EmojiVendor)settings.RenderEmojiVendor
                : EmojiVendor.GoogleNotoColor;

            return Create(new ChatRenderBuildArgs
            {
                InputFile = inputFile,
                OutputFile = outputFile,
                BackgroundColorHex = settings.RenderBackgroundColor,
                AlternateBackgroundColorHex = settings.RenderAlternateBackgroundColor,
                FontColorHex = settings.RenderFontColor,
                HighlightUsersColorHex = settings.RenderHighlightUsersColor,
                AlternateMessageBackgrounds = settings.RenderAlternateMessageBackgrounds,
                ChatHeight = settings.RenderHeight,
                ChatWidth = settings.RenderWidth,
                BttvEmotes = settings.BttvEmotes,
                FfzEmotes = settings.FfzEmotes,
                StvEmotes = settings.StvEmotes,
                Outline = settings.RenderOutline,
                Font = string.IsNullOrWhiteSpace(settings.RenderFont) ? "Inter Embedded" : settings.RenderFont,
                FontSize = settings.RenderFontSize,
                UpdateRate = settings.RenderUpdateTime,
                EmoteScale = settings.RenderEmoteScale,
                BadgeScale = settings.RenderBadgeScale,
                EmojiScale = settings.RenderEmojiScale,
                AvatarScale = settings.RenderAvatarScale,
                SidePaddingScale = settings.RenderSidePaddingScale,
                SectionHeightScale = settings.RenderSectionHeightScale,
                WordSpacingScale = settings.RenderWordSpacingScale,
                EmoteSpacingScale = settings.RenderEmoteSpacingScale,
                AccentIndentScale = settings.RenderAccentIndentScale,
                AccentStrokeScale = settings.RenderAccentStrokeScale,
                VerticalSpacingScale = settings.RenderVerticalSpacingScale,
                UsernameFontScale = settings.RenderUsernameFontScale,
                OutlineScale = settings.RenderOutlineScale,
                HighlightUsersList = settings.RenderHighlightUsersList,
                IgnoreUsersList = settings.RenderIgnoreUsersList,
                BannedWordsList = settings.RenderBannedWordsList,
                Timestamp = settings.RenderTimestamp,
                Framerate = settings.RenderFramerate,
                FfmpegInputArgs = inputArgs,
                FfmpegOutputArgs = outputArgs,
                Sharpening = settings.RenderSharpening,
                GenerateMask = settings.RenderGenerateMask,
                TempFolder = settings.TempPath,
                SubMessages = settings.RenderSubMessages,
                ChatBadges = settings.RenderChatBadges,
                Offline = settings.RenderOffline,
                RenderUserAvatars = settings.RenderUserAvatars,
                DisperseCommentOffsets = settings.RenderDisperseCommentOffsets,
                AdjustUsernameVisibility = settings.RenderAdjustUsernameVisibility,
                EmojiVendor = emojiVendor,
                ChatBadgeMask = (ChatBadgeType)settings.RenderChatBadgeMask,
                StartOverride = -1,
                EndOverride = -1,
            }, ffmpegPath, collisionCallback);
        }

        public static ChatRenderOptions Create(
            ChatRenderBuildArgs args,
            string ffmpegPath,
            Func<FileInfo, FileInfo> collisionCallback)
        {
            var background = RequireColor(args.BackgroundColorHex);
            var alternate = RequireColor(args.AlternateBackgroundColorHex);
            var fontColor = RequireColor(args.FontColorHex);
            var highlight = RequireColor(args.HighlightUsersColorHex);
            var inputArgs = args.Sharpening
                ? args.FfmpegInputArgs + " -filter_complex \"smartblur=lr=1:ls=-1.0\""
                : args.FfmpegInputArgs;

            return new ChatRenderOptions
            {
                OutputFile = args.OutputFile,
                InputFile = args.InputFile,
                BackgroundColor = background,
                AlternateBackgroundColor = alternate,
                AlternateMessageBackgrounds = args.AlternateMessageBackgrounds,
                ChatHeight = args.ChatHeight,
                ChatWidth = args.ChatWidth,
                BttvEmotes = args.BttvEmotes,
                FfzEmotes = args.FfzEmotes,
                StvEmotes = args.StvEmotes,
                Outline = args.Outline,
                Font = args.Font,
                FontSize = args.FontSize,
                UpdateRate = args.UpdateRate,
                EmoteScale = args.EmoteScale,
                BadgeScale = args.BadgeScale,
                EmojiScale = args.EmojiScale,
                AvatarScale = args.AvatarScale,
                SidePaddingScale = args.SidePaddingScale,
                SectionHeightScale = args.SectionHeightScale,
                WordSpacingScale = args.WordSpacingScale,
                EmoteSpacingScale = args.EmoteSpacingScale,
                AccentIndentScale = args.AccentIndentScale,
                AccentStrokeScale = args.AccentStrokeScale,
                VerticalSpacingScale = args.VerticalSpacingScale,
                UsernameFontScale = args.UsernameFontScale,
                HighlightUserColor = highlight,
                HighlightUsersArray = SplitCsv(args.HighlightUsersList),
                IgnoreUsersArray = SplitCsv(args.IgnoreUsersList),
                BannedWordsArray = SplitCsv(args.BannedWordsList),
                Timestamp = args.Timestamp,
                MessageColor = fontColor.WithAlpha(255),
                Framerate = args.Framerate,
                InputArgs = inputArgs,
                OutputArgs = args.FfmpegOutputArgs,
                MessageFontStyle = SKFontStyle.Normal,
                UsernameFontStyle = SKFontStyle.Bold,
                GenerateMask = args.GenerateMask,
                OutlineSize = 4 * args.OutlineScale,
                FfmpegPath = ffmpegPath,
                TempFolder = args.TempFolder,
                SubMessages = args.SubMessages,
                ChatBadges = args.ChatBadges,
                Offline = args.Offline,
                RenderUserAvatars = args.RenderUserAvatars,
                AllowUnlistedEmotes = true,
                DisperseCommentOffsets = args.DisperseCommentOffsets,
                AdjustUsernameVisibility = args.AdjustUsernameVisibility,
                EmojiVendor = args.EmojiVendor,
                ChatBadgeMask = args.ChatBadgeMask,
                StartOverride = args.StartOverride,
                EndOverride = args.EndOverride,
                FileCollisionCallback = collisionCallback,
            };
        }

        public static bool TryParseColor(string hex, out SKColor color)
        {
            color = SKColors.Transparent;
            if (string.IsNullOrWhiteSpace(hex) || !Color.TryParse(hex.Trim(), out var parsed))
                return false;

            color = new SKColor(parsed.R, parsed.G, parsed.B, parsed.A);
            return true;
        }

        private static SKColor RequireColor(string hex)
        {
            if (!TryParseColor(hex, out var color))
                throw new InvalidOperationException(Loc.Get("render.invalid_colors"));

            return color;
        }

        private static (string InputArgs, string OutputArgs) ResolveFfmpegArgs(AppSettings settings)
        {
            var containers = RenderEncodingPresets.CreateContainers();
            var container = containers.FirstOrDefault(item => item.Name == settings.RenderVideoContainer) ?? containers[0];
            var codec = container.Codecs.FirstOrDefault(item => item.Name == settings.RenderVideoCodec) ?? container.Codecs[0];
            var match = DeserializeFfmpegArgs(settings.RenderFfmpegArguments)
                .FirstOrDefault(item => item.CodecName == codec.Name && item.ContainerName == container.Name);

            var input = string.IsNullOrWhiteSpace(match?.InputArgs) ? codec.InputArgs : match.InputArgs;
            var output = string.IsNullOrWhiteSpace(match?.OutputArgs) ? codec.OutputArgs : match.OutputArgs;
            return (input, output);
        }

        private static List<CustomFfmpegArgs> DeserializeFfmpegArgs(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<CustomFfmpegArgs>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }

        private static string[] SplitCsv(string value)
        {
            return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
