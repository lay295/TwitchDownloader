using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Extensions.Logging;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class ThemePackLoaderTests
    {
        [Fact]
        public void ParseValidPackReadsBrushesAndIsDark()
        {
            const string JSON =
                """
                {
                  "author": "Tester",
                  "isDark": true,
                  "colors": {
                    "AccentBrush": "#00FF00",
                    "ThemeAccentBrush2": "#CC00FF00"
                  }
                }
                """;

            Assert.True(ThemePackLoader.TryParse(JSON, "Neon", out var pack));
            Assert.NotNull(pack);
            Assert.Equal("Neon", pack.Name);
            Assert.Equal("Tester", pack.Author);
            Assert.True(pack.IsDark);
            Assert.Equal(Color.FromRgb(0, 255, 0), pack.Colors["AccentBrush"]);
            Assert.Equal(Color.FromArgb(0xCC, 0, 255, 0), pack.Colors["ThemeAccentBrush2"]);
        }

        [Fact]
        public void MissingKeysFillFromDarkFallback()
        {
            const string JSON =
                """
                {
                  "isDark": true,
                  "colors": {
                    "AccentBrush": "#00FF00",
                    "NotARealBrush": "#FFFFFF"
                  }
                }
                """;

            Assert.True(ThemePackLoader.TryParse(JSON, "Partial", out var pack));
            Assert.NotNull(pack);
            Assert.Equal(Color.FromRgb(0, 255, 0), pack.Colors["AccentBrush"]);
            Assert.Equal(Color.Parse("#1C1C21"), pack.Colors["WindowBackgroundBrush"]);
            Assert.Equal(Color.Parse("#EFEFF1"), pack.Colors["WindowForegroundBrush"]);
            Assert.False(pack.Colors.ContainsKey("NotARealBrush"));
        }

        [Fact]
        public void MissingKeysFillFromLightFallback()
        {
            const string JSON = """{ "isDark": false, "colors": { } }""";

            Assert.True(ThemePackLoader.TryParse(JSON, "Empty", out var pack));
            Assert.NotNull(pack);
            Assert.False(pack.IsDark);
            Assert.Equal(Color.Parse("#F4F2F7"), pack.Colors["WindowBackgroundBrush"]);
            Assert.Equal(Color.Parse("#9146FF"), pack.Colors["AccentBrush"]);
        }

        [Fact]
        public void InvalidHexUsesFallback()
        {
            const string JSON =
                """
                {
                  "isDark": false,
                  "colors": {
                    "AccentBrush": "nope",
                    "WindowBackgroundBrush": "#FFFFFF"
                  }
                }
                """;

            Assert.True(ThemePackLoader.TryParse(JSON, "BadHex", out var pack));
            Assert.NotNull(pack);
            Assert.Equal(Color.Parse("#FFFFFF"), pack.Colors["WindowBackgroundBrush"]);
            Assert.Equal(Color.Parse("#9146FF"), pack.Colors["AccentBrush"]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("{ \"colors\": {} }")]
        [InlineData("{ \"isDark\": \"yes\" }")]
        public void InvalidJsonFailsGracefully(string json)
        {
            Assert.False(ThemePackLoader.TryParse(json, "Broken", out var pack));
            Assert.Null(pack);
        }

        [Fact]
        public void ApplySelectionMapsBuiltinAndPackPreferences()
        {
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var themes = new ThemeService(loggerFactory.CreateLogger<ThemeService>());
            ThemePackInfo[] packs =
            [
                new("Light Pink", false),
                new("Dark Pink", true),
            ];

            var system = themes.Normalize("System", "Light Pink", "Dark Pink", packs);
            Assert.Equal(ThemeService.SYSTEM, system.Mode);
            Assert.Equal("Light Pink", system.LightTheme);
            Assert.Equal("Dark Pink", system.DarkTheme);
            Assert.Empty(system.MissingPackFiles);
            Assert.Same(ThemeVariant.Default, ThemeService.GetRequestedVariant(system.Mode));

            var light = themes.Normalize("light", "Light Pink", "Dark", packs);
            Assert.Equal(ThemeService.LIGHT, light.Mode);
            Assert.Same(ThemeVariant.Light, ThemeService.GetRequestedVariant(light.Mode));

            var dark = themes.Normalize("DARK", "Light", "dark pink", packs);
            Assert.Equal(ThemeService.DARK, dark.Mode);
            Assert.Equal("Dark Pink", dark.DarkTheme);
            Assert.Same(ThemeVariant.Dark, ThemeService.GetRequestedVariant(dark.Mode));

            var migrated = themes.Normalize("dark pink", "Light", "Dark", packs);
            Assert.True(migrated.MigratedFromPack);
            Assert.Equal(ThemeService.DARK, migrated.Mode);
            Assert.Equal("Dark Pink", migrated.DarkTheme);
            Assert.Equal(ThemeService.LIGHT, migrated.LightTheme);

            var missing = themes.Normalize("Gone", "Missing Light", "Missing Dark", packs);
            Assert.Equal(ThemeService.SYSTEM, missing.Mode);
            Assert.Equal(ThemeService.LIGHT, missing.LightTheme);
            Assert.Equal(ThemeService.DARK, missing.DarkTheme);
            Assert.Equal(3, missing.MissingPackFiles.Count);
            Assert.Same(ThemeVariant.Default, ThemeService.GetRequestedVariant(missing.Mode));
        }
    }

    public class ThemePackCatalogTests
    {
        [Fact]
        public void ScanPacksFiltersReservedNamesAndSorts()
        {
            var directory = CreateTempThemesDir();
            File.WriteAllText(Path.Combine(directory, "System.json"), "{ \"isDark\": true }");
            File.WriteAllText(Path.Combine(directory, "Light.json"), "{ \"isDark\": false }");
            File.WriteAllText(Path.Combine(directory, "Dark.json"), "{ \"isDark\": true }");
            File.WriteAllText(Path.Combine(directory, "Example Light.json"), "{ \"isDark\": false }");
            File.WriteAllText(Path.Combine(directory, "Example Dark.json"), "{ \"isDark\": true }");
            File.WriteAllText(Path.Combine(directory, "Zed.json"), "{ \"isDark\": true }");
            File.WriteAllText(Path.Combine(directory, "Alpha.json"), "{ \"isDark\": false }");

            var catalog = new ThemePackCatalog(directory);
            var names = catalog.ScanPacks();

            Assert.Equal(["Alpha", "Zed"], names);
        }

        [Fact]
        public void EnsureIncludedPacksWritesSamplesOnce()
        {
            var directory = CreateTempThemesDir();
            var catalog = new ThemePackCatalog(directory);

            Assert.True(catalog.EnsureIncludedPacks());
            var pinkPath = Path.Combine(directory, "Light Pink.json");
            var original = File.ReadAllText(pinkPath);
            File.WriteAllText(pinkPath, "{ \"isDark\": false, \"colors\": { \"AccentBrush\": \"#111111\" } }");

            Assert.True(catalog.EnsureIncludedPacks());
            Assert.Equal("{ \"isDark\": false, \"colors\": { \"AccentBrush\": \"#111111\" } }", File.ReadAllText(pinkPath));
            Assert.True(File.Exists(Path.Combine(directory, "README.txt")));
            var readme = File.ReadAllText(Path.Combine(directory, "README.txt"));
            Assert.Contains("Custom theme packs", readme, StringComparison.Ordinal);
            Assert.Contains("isDark", readme, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(directory, "Dark Pink.json")));
            Assert.False(File.Exists(Path.Combine(directory, "Example Light.json")));
            Assert.DoesNotContain("Example Light", catalog.ScanPacks());
            Assert.Contains("Light Pink", catalog.ScanPacks());
            Assert.Contains("Dark Pink", catalog.ScanPacks());
            Assert.NotEqual(original.Trim(), File.ReadAllText(pinkPath).Trim());

            var infos = catalog.ScanPackInfos();
            Assert.Contains(infos, info => info is { Name: "Light Pink", IsDark: false });
            Assert.Contains(infos, info => info is { Name: "Dark Pink", IsDark: true });
            Assert.DoesNotContain(infos, info => info.Name == "Example Light");
            Assert.DoesNotContain(infos, info => info.Name == "Example Dark");
        }

        [Fact]
        public void ApplyDoesNothingWithoutApplication()
        {
            using var loggerFactory = LoggerFactory.Create(_ => { });
            var themes = new ThemeService(loggerFactory.CreateLogger<ThemeService>());
            themes.Apply("Dark");
            themes.Apply("Missing Pack");
        }

        private static string CreateTempThemesDir()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"), "Themes");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
