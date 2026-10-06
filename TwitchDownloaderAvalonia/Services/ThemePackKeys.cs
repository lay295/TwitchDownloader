namespace TwitchDownloaderAvalonia.Services
{
    internal static class ThemePackKeys
    {
        public static readonly string[] BrushKeys =
        [
            "WindowBackgroundBrush",
            "WindowForegroundBrush",
            "SidebarBackgroundBrush",
            "StatusBarBackgroundBrush",
            "CardBackgroundBrush",
            "CardMutedBrush",
            "InputBackgroundBrush",
            "BorderBrush",
            "AccentBrush",
            "AccentHoverBrush",
            "AccentForegroundBrush",
            "NavSelectedBrush",
            "NavHoverBrush",
            "CardSelectedBrush",
            "CardSelectedHoverBrush",
            "MutedForegroundBrush",
            "LiveBadgeBackgroundBrush",
            "LiveBadgeForegroundBrush",
            "HighlightBrush",
            "HighlightForegroundBrush",
            "ThemeBackgroundBrush",
            "ThemeForegroundBrush",
            "ThemeAccentBrush",
            "ThemeAccentBrush2",
            "ThemeAccentBrush3",
            "ThemeAccentBrush4",
            "ThemeControlHighlightMidBrush",
            "ThemeBorderLowBrush",
            "ThemeBorderMidBrush",
            "ThemeBorderHighBrush",
            "ThemeControlMidBrush",
            "ThemeControlHighBrush",
        ];

        public static readonly IReadOnlyDictionary<string, string> ColorKeyForBrush =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ThemeBackgroundBrush"] = "ThemeBackgroundColor",
                ["ThemeAccentBrush"] = "ThemeAccentColor",
                ["ThemeForegroundBrush"] = "ThemeForegroundColor",
                ["ThemeBorderLowBrush"] = "ThemeBorderLowColor",
                ["ThemeControlMidBrush"] = "ThemeControlMidColor",
                ["ThemeControlHighBrush"] = "ThemeControlHighColor",
                ["LiveBadgeBackgroundBrush"] = "LiveBadgeColor",
            };

        public static bool IsReserved(string name)
        {
            return name.Equals(ThemeService.SYSTEM, StringComparison.OrdinalIgnoreCase)
                   || name.Equals(ThemeService.LIGHT, StringComparison.OrdinalIgnoreCase)
                   || name.Equals(ThemeService.DARK, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsHiddenSample(string name)
        {
            return name.Equals("Example Light", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("Example Dark", StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldListPack(string name) => !IsReserved(name) && !IsHiddenSample(name);
    }
}
