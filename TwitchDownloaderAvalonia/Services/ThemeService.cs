using System.Diagnostics.CodeAnalysis;
using Avalonia.Styling;

namespace TwitchDownloaderAvalonia.Services
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class ThemeService
    {
        public const string System = "System";
        public const string Light = "Light";
        public const string Dark = "Dark";

        public static IReadOnlyList<string> Options { get; } = [System, Light, Dark];

        public static void Apply(string? theme)
        {
            if (Application.Current is null)
                return;

            Application.Current.RequestedThemeVariant = theme switch
            {
                Light => ThemeVariant.Light,
                Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }
}
