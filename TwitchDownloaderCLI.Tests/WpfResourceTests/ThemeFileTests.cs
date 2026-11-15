using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TwitchDownloaderCLI.Tests.WpfResourceTests
{
    // Tests the bundled theme files TwitchDownloaderWPF/Themes/*.xaml. ThemeService deserializes them with XmlSerializer and copies each
    // SolidColorBrush into Application.Resources by key; they are also the templates users copy to make custom themes. A bad value
    // crashes or silently skips at theme load, and a missing key leaves the previous theme's colour on screen.
    public partial class ThemeFileTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        private static readonly XNamespace SystemNs = "clr-namespace:System;assembly=mscorlib";

        private static string[] ThemeFileNames() => Directory.GetFiles(WpfRepoFiles.PathOf("Themes"), "*.xaml").Select(f => Path.GetFileName(f)).Order().ToArray();

        public static TheoryData<string> ThemeFiles => new(ThemeFileNames());

        [GeneratedRegex(@"\{DynamicResource\s+([A-Za-z0-9_]+)\s*\}")]
        private static partial Regex DynamicResourceRegex();

        [GeneratedRegex("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")]
        private static partial Regex HexColorRegex();

        private static XElement LoadTheme(string fileName) => XDocument.Load(WpfRepoFiles.PathOf("Themes", fileName)).Root!;

        private static string[] BrushKeys(string fileName) =>
            LoadTheme(fileName).Elements(Presentation + "SolidColorBrush").Select(e => (string)e.Attribute(X + "Key")!).ToArray();

        // Verifies every bundled theme defines exactly the same brush and boolean keys.
        // Why: ThemeService only overwrites the keys present in the selected file, so a key missing from Light.xaml would keep showing the
        // Dark colour after switching themes; users also copy these files as templates, propagating any gap into custom themes.
        [Fact]
        public void BundledThemes_DefineTheSameKeys()
        {
            var themes = ThemeFileNames();
            Assert.True(themes.Length >= 2, "Expected at least the Dark and Light themes");

            static string[] AllKeys(string fileName) => LoadTheme(fileName).Elements().Select(e => (string)e.Attribute(X + "Key")!).Order().ToArray();

            var reference = AllKeys(themes[0]);
            foreach (var theme in themes.Skip(1))
            {
                Assert.Equal(reference, AllKeys(theme));
            }
        }

        // Verifies each brush has a key and a #RRGGBB/#AARRGGBB colour, and that both booleans ThemeService reads are present and lower-case.
        // Why: XmlSerializer only accepts lower-case xs:boolean text ("True" throws during deserialization, which ThemeService does not catch),
        // and a malformed colour is silently skipped by BrushConverter, leaving that element un-themed.
        [Theory]
        [MemberData(nameof(ThemeFiles))]
        public void Theme_HasValidBrushesAndBooleans(string fileName)
        {
            var root = LoadTheme(fileName);

            foreach (var brush in root.Elements(Presentation + "SolidColorBrush"))
            {
                var key = (string?)brush.Attribute(X + "Key");
                var color = (string?)brush.Attribute("Color");
                Assert.False(string.IsNullOrEmpty(key), $"{fileName} has a SolidColorBrush without x:Key");
                Assert.True(color is not null && HexColorRegex().IsMatch(color), $"{fileName}: {key} has invalid Color '{color}'");
            }

            var booleans = root.Elements(SystemNs + "Boolean").ToDictionary(e => (string)e.Attribute(X + "Key")!, e => e.Value);
            Assert.Equal(["DarkHandyControl", "DarkTitleBar"], booleans.Keys.Order());
            Assert.All(booleans.Values, value => Assert.Contains(value, new[] { "true", "false" }));
        }

        // Verifies every {DynamicResource Key} used by the GUI's windows and pages is defined by every bundled theme.
        // Why: DynamicResource lookups fail silently; a brush the selected theme does not define keeps App.xaml's light-palette fallback
        // (or no brush at all), giving e.g. light panels or unreadable text in the dark theme.
        [Fact]
        public void DynamicResourcesUsedByTheGui_AreDefinedByEveryTheme()
        {
            var used = Directory.GetFiles(WpfRepoFiles.WpfDirectory, "*.xaml")
                .Where(f => !Path.GetFileName(f).Equals("App.xaml", StringComparison.OrdinalIgnoreCase))
                .SelectMany(f => DynamicResourceRegex().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
                .ToHashSet();

            Assert.NotEmpty(used);
            foreach (var theme in ThemeFileNames())
            {
                var missing = used.Except(BrushKeys(theme)).Order().ToArray();
                Assert.True(missing.Length == 0, $"{theme} does not define: {string.Join(", ", missing)}");
            }
        }
    }
}
