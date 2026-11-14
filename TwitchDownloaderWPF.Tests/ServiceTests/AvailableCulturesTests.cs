using System.Globalization;
using System.Reflection;
using System.Resources;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Tests.TestHelpers;
using TwitchDownloaderWPF.Translations;

namespace TwitchDownloaderWPF.Tests.ServiceTests
{
    // Tests AvailableCultures, the compiled list behind the Settings window's language dropdown. WindowSettings indexes straight into
    // AvailableCultures.All and CultureService feeds the code to CultureInfo/WPFLocalizeExtension, so every entry must be a real culture
    // that resolves to its own translation. (TwitchDownloaderCLI.Tests/WpfResourceTests checks the same table from source on every OS.)
    public class AvailableCulturesTests
    {
        private static Culture[] NamedFields() => typeof(AvailableCultures)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Culture))
            .Select(f => (Culture)f.GetValue(null)!)
            .ToArray();

        // Verifies the named fields (English, Polish, Brazilian, ...) are all distinct and are exactly the entries of All.
        // Why: regression test for `Polish = new Culture("pt-BR", ...)`, which compiled (static readonly fields may be reassigned in the static
        // constructor) and left AvailableCultures.Polish pointing at Brazilian Portuguese with no field for pl-PL.
        [Fact]
        public void NamedFields_AreDistinctAndMatchAll()
        {
            var fields = NamedFields();

            Assert.Equal(fields.Length, fields.Distinct().Count());
            Assert.Equal(AvailableCultures.All.OrderBy(c => c.Code), fields.OrderBy(c => c.Code));
        }

        // Verifies the named fields hold the culture their name promises, for the two that were mixed up.
        // Why: pins the exact regression (Polish == pt-BR) rather than only the structural symptom.
        [Fact]
        public void PolishAndBrazilian_HaveTheirOwnCodes()
        {
            Assert.Equal("pl-PL", AvailableCultures.Polish.Code);
            Assert.Equal("pt-BR", AvailableCultures.Brazilian.Code);
        }

        // Verifies every advertised code is a culture .NET recognises.
        // Why: CultureService.SetApplicationCulture swallows CultureNotFoundException, so a typo in a code would make picking that language
        // silently do nothing.
        [Fact]
        public void EveryCode_IsAKnownCulture()
        {
            Assert.All(AvailableCultures.All, culture => CultureInfo.GetCultureInfo(culture.Code, predefinedOnly: true));
        }

        // Verifies every non-English culture resolves, through the real ResourceManager fallback, to a satellite resource set of its own
        // rather than to the neutral English strings.
        // Why: this is the end-to-end check that the culture code, the Strings.<culture>.resx file name and the satellite assembly the build
        // produces all line up; if any of them drift, the language appears in the dropdown but the GUI stays English.
        [Fact]
        public void EveryNonEnglishCulture_LoadsItsOwnTranslation()
        {
            var neutral = Strings.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false);

            var untranslated = AvailableCultures.All
                .Where(c => c != AvailableCultures.English)
                .Where(c => ReferenceEquals(Strings.ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo(c.Code), createIfNotExists: true, tryParents: true), neutral))
                .Select(c => c.Code)
                .ToArray();

            Assert.NotNull(neutral);
            Assert.True(untranslated.Length == 0, $"These cultures fall back to the neutral resources: {string.Join(", ", untranslated)}");
        }

        // Verifies English is the first entry and matches the GuiCulture setting's default.
        // Why: WindowSettings selects the dropdown entry with `culture.Code == Settings.Default.GuiCulture`; if the default no longer matched an
        // entry exactly, a fresh install would show an empty language dropdown.
        [Fact]
        public void English_IsFirstAndIsTheDefaultSetting()
        {
            var defaultCulture = SettingDefaults.Of("GuiCulture");

            Assert.Equal(AvailableCultures.English, AvailableCultures.All[0]);
            Assert.Equal(AvailableCultures.English.Code, defaultCulture);
        }
    }
}
