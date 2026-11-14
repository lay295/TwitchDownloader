using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TwitchDownloaderCLI.Tests.WpfResourceTests
{
    // Tests TwitchDownloaderWPF/Services/AvailableCultures.cs, the list behind the Settings window's language dropdown, against the
    // translation files that actually exist. The WPF assembly cannot be loaded off Windows, so the culture table is read from its source;
    // TwitchDownloaderWPF.Tests checks the compiled type on Windows. A mistake here either hides a finished translation or advertises a
    // language that silently shows English.
    public partial class AvailableCulturesSourceTests
    {
        private record CultureEntry(string Field, string Code, string NativeName);

        // Matches `public static readonly Culture English;`
        [GeneratedRegex(@"public\s+static\s+readonly\s+Culture\s+(\w+)\s*;")]
        private static partial Regex FieldDeclarationRegex();

        // Matches `English = new Culture("en-US", "English")`
        [GeneratedRegex("""(\w+)\s*=\s*new\s+Culture\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\)""")]
        private static partial Regex CultureAssignmentRegex();

        private static readonly string Source = File.ReadAllText(WpfRepoFiles.PathOf("Services", "AvailableCultures.cs"));

        private static List<CultureEntry> Entries() =>
            CultureAssignmentRegex().Matches(Source).Select(m => new CultureEntry(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value)).ToList();

        // Verifies each named Culture field is assigned exactly once and every declared field is assigned.
        // Why: regression test for the bug where the pt-BR entry was written as `Polish = new Culture("pt-BR", ...)`. C# allows a static
        // readonly field to be assigned twice in a static constructor, so it compiled, and AvailableCultures.Polish silently became Portuguese.
        [Fact]
        public void EachCultureField_IsAssignedExactlyOnce()
        {
            var entries = Entries();
            var declared = FieldDeclarationRegex().Matches(Source).Select(m => m.Groups[1].Value).ToArray();

            Assert.NotEmpty(entries);
            var reassigned = entries.GroupBy(e => e.Field).Where(g => g.Count() > 1).Select(g => $"{g.Key} ({string.Join(", ", g.Select(e => e.Code))})").ToArray();
            Assert.True(reassigned.Length == 0, $"Culture fields assigned more than once: {string.Join("; ", reassigned)}");
            Assert.Equal(declared.Order(), entries.Select(e => e.Field).Order());
        }

        // Verifies no two advertised cultures share a code (case-insensitively) or a display name.
        // Why: WindowSettings maps the dropdown index straight back into AvailableCultures.All, so two entries with the same name are
        // indistinguishable to the user, and two with the same code make one of them unreachable when the setting is restored.
        [Fact]
        public void AdvertisedCultures_HaveUniqueCodesAndNames()
        {
            var entries = Entries();

            Assert.Equal(entries.Count, entries.Select(e => e.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(entries.Count, entries.Select(e => e.NativeName).Distinct().Count());
        }

        // Verifies every advertised culture other than English has a Strings.<culture>.resx that .NET resource fallback will pick
        // (an exact match such as pt-br, or its parent language such as de for de-DE).
        // Why: selecting a culture without a translation is silently ignored by the resource manager and the GUI stays in English.
        [Fact]
        public void EveryNonEnglishCulture_HasATranslationFile()
        {
            var translations = WpfRepoFiles.TranslationResxPaths.Select(WpfRepoFiles.ResxCulture).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var untranslated = Entries()
                .Where(e => !e.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                .Where(e => !translations.Contains(e.Code) && !translations.Contains(e.Code.Split('-')[0]))
                .Select(e => e.Code)
                .ToArray();

            Assert.True(untranslated.Length == 0, $"Advertised cultures with no Strings.<culture>.resx: {string.Join(", ", untranslated)}");
        }

        // Verifies every translation file can be selected through at least one advertised culture.
        // Why: a translator who adds Strings.xx.resx but forgets the AvailableCultures entry ships a satellite assembly that can never be
        // chosen in the GUI.
        [Fact]
        public void EveryTranslationFile_IsReachableFromAnAdvertisedCulture()
        {
            var codes = Entries().Select(e => e.Code).ToArray();

            var unreachable = WpfRepoFiles.TranslationResxPaths
                .Select(WpfRepoFiles.ResxCulture)
                .Where(resxCulture => !codes.Any(code => code.Equals(resxCulture, StringComparison.OrdinalIgnoreCase)
                                                         || code.Split('-')[0].Equals(resxCulture, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            Assert.True(unreachable.Length == 0, $"Translations not selectable from AvailableCultures: {string.Join(", ", unreachable)}");
        }

        // Verifies the default GuiCulture in Settings.settings is exactly (ordinal) one of the advertised codes.
        // Why: WindowSettings pre-selects the dropdown entry with `culture.Code == Settings.Default.GuiCulture`; a default such as "en-us"
        // would leave the language dropdown blank on a fresh install.
        [Fact]
        public void DefaultGuiCulture_IsAnAdvertisedCode()
        {
            XNamespace ns = "http://schemas.microsoft.com/VisualStudio/2004/01/settings";
            var defaultCulture = XDocument.Load(WpfRepoFiles.PathOf("Properties", "Settings.settings"))
                .Descendants(ns + "Setting")
                .Single(s => (string?)s.Attribute("Name") == "GuiCulture")
                .Element(ns + "Value")!.Value;

            Assert.Contains(defaultCulture, Entries().Select(e => e.Code));
        }
    }
}
