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
            var container = containers.FirstOrDefault(item => item.Name == settings.Render.VideoContainer) ?? containers[0];
            return container.Name.ToLowerInvariant();
        }

        public static ChatRenderOptions FromSettings(
            LocalizationService loc,
            AppSettings settings,
            string inputFile,
            string outputFile,
            string ffmpegPath,
            Func<FileInfo, FileInfo> collisionCallback)
        {
            var render = settings.Render;
            var chat = settings.Chat;
            var (inputArgs, outputArgs) = ResolveFfmpegArgs(settings);
            var emojiVendor = Enum.IsDefined(typeof(EmojiVendor), render.EmojiVendor)
                ? (EmojiVendor)render.EmojiVendor
                : EmojiVendor.GoogleNotoColor;

            return Create(loc, new ChatRenderBuildArgs
            {
                InputFile = inputFile,
                OutputFile = outputFile,
                BackgroundColorHex = render.BackgroundColor,
                AlternateBackgroundColorHex = render.AlternateBackgroundColor,
                FontColorHex = render.FontColor,
                HighlightUsersColorHex = render.HighlightUsersColor,
                AlternateMessageBackgrounds = render.AlternateMessageBackgrounds,
                ChatHeight = render.Height,
                ChatWidth = render.Width,
                BttvEmotes = chat.BttvEmotes,
                FfzEmotes = chat.FfzEmotes,
                StvEmotes = chat.StvEmotes,
                Outline = render.Outline,
                Font = string.IsNullOrWhiteSpace(render.Font) ? "Inter Embedded" : render.Font,
                FontSize = render.FontSize,
                UpdateRate = render.UpdateTime,
                EmoteScale = render.EmoteScale,
                BadgeScale = render.BadgeScale,
                EmojiScale = render.EmojiScale,
                AvatarScale = render.AvatarScale,
                SidePaddingScale = render.SidePaddingScale,
                SectionHeightScale = render.SectionHeightScale,
                WordSpacingScale = render.WordSpacingScale,
                EmoteSpacingScale = render.EmoteSpacingScale,
                AccentIndentScale = render.AccentIndentScale,
                AccentStrokeScale = render.AccentStrokeScale,
                VerticalSpacingScale = render.VerticalSpacingScale,
                UsernameFontScale = render.UsernameFontScale,
                OutlineScale = render.OutlineScale,
                HighlightUsersList = render.HighlightUsersList,
                IgnoreUsersList = render.IgnoreUsersList,
                BannedWordsList = render.BannedWordsList,
                Timestamp = render.Timestamp,
                Framerate = render.Framerate,
                FfmpegInputArgs = inputArgs,
                FfmpegOutputArgs = outputArgs,
                Sharpening = render.Sharpening,
                GenerateMask = render.GenerateMask,
                TempFolder = settings.General.TempPath,
                SubMessages = render.SubMessages,
                ChatBadges = render.ChatBadges,
                Offline = render.Offline,
                RenderUserAvatars = render.UserAvatars,
                DisperseCommentOffsets = render.DisperseCommentOffsets,
                AdjustUsernameVisibility = render.AdjustUsernameVisibility,
                EmojiVendor = emojiVendor,
                ChatBadgeMask = (ChatBadgeType)render.ChatBadgeMask,
                StartOverride = -1,
                EndOverride = -1,
            }, ffmpegPath, collisionCallback);
        }

        public static ChatRenderOptions Create(
            LocalizationService loc,
            ChatRenderBuildArgs args,
            string ffmpegPath,
            Func<FileInfo, FileInfo> collisionCallback)
        {
            var background = RequireColor(loc, args.BackgroundColorHex);
            var alternate = RequireColor(loc, args.AlternateBackgroundColorHex);
            var fontColor = RequireColor(loc, args.FontColorHex);
            var highlight = RequireColor(loc, args.HighlightUsersColorHex);
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

        private static SKColor RequireColor(LocalizationService loc, string hex)
        {
            if (!TryParseColor(hex, out var color))
                throw new InvalidOperationException(loc.Get("render.invalid_colors"));

            return color;
        }

        private static (string InputArgs, string OutputArgs) ResolveFfmpegArgs(AppSettings settings)
        {
            var render = settings.Render;
            var containers = RenderEncodingPresets.CreateContainers();
            var container = containers.FirstOrDefault(item => item.Name == render.VideoContainer) ?? containers[0];
            var codec = container.Codecs.FirstOrDefault(item => item.Name == render.VideoCodec) ?? container.Codecs[0];
            var match = DeserializeFfmpegArgs(render.FfmpegArguments)
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
