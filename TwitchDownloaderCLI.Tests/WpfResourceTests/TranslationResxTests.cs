using System.Text;
using System.Text.RegularExpressions;

namespace TwitchDownloaderCLI.Tests.WpfResourceTests
{
    // Tests TwitchDownloaderWPF/Translations/Strings*.resx, the GUI's only source of user-visible text.
    // A broken translation is never caught by the compiler: a missing key silently falls back to English (or shows nothing),
    // and a translated format string with the wrong placeholders makes string.Format throw at runtime, only for that language.
    public partial class TranslationResxTests
    {
        public static TheoryData<string> AllResxFiles => new(Directory.GetFiles(WpfRepoFiles.PathOf("Translations"), "*.resx").Select(f => Path.GetFileName(f)).Order());

        public static TheoryData<string> TranslationResxFiles => new(WpfRepoFiles.TranslationResxPaths.Select(f => Path.GetFileName(f)));

        // Matches the argument index of a composite format item such as {0}, {1:N0} or {2,-5}.
        [GeneratedRegex(@"\{(\d+)\s*[,:}]")]
        private static partial Regex FormatItemIndexRegex();

        // Matches {lex:Loc SomeKey}, the WPFLocalizeExtension markup used for every localized string in the XAML.
        [GeneratedRegex(@"\{lex:Loc\s+([A-Za-z0-9_]+)\s*\}")]
        private static partial Regex LexLocRegex();

        // Matches ResourceManager.GetString("SomeKey", ...) in the generated Strings.Designer.cs.
        [GeneratedRegex(@"ResourceManager\.GetString\(""([^""]+)""")]
        private static partial Regex DesignerGetStringRegex();

        // Verifies every resx parses as XML and declares each key once.
        // Why: a malformed resx fails the whole WPF build, and a duplicated key fails resgen with an unhelpful error; catching it here
        // points at the exact file and key, and on platforms where the WPF project is not built at all.
        [Theory]
        [MemberData(nameof(AllResxFiles))]
        public void ResxFile_IsWellFormedXmlWithUniqueKeys(string fileName)
        {
            var entries = WpfRepoFiles.ReadResxEntries(WpfRepoFiles.PathOf("Translations", fileName));

            Assert.NotEmpty(entries);
            var duplicates = entries.GroupBy(e => e.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            Assert.True(duplicates.Length == 0, $"{fileName} declares these keys more than once: {string.Join(", ", duplicates)}");
        }

        // Verifies each translation has exactly the neutral (English) key set, and that no translated value is blank.
        // Why: a key missing from a translation silently shows English in that language, a key that only exists in a translation is dead
        // (usually a rename that was not propagated), and an empty value renders as a blank label or button.
        [Theory]
        [MemberData(nameof(TranslationResxFiles))]
        public void Translation_HasExactlyTheNeutralKeys_WithNonEmptyValues(string fileName)
        {
            var neutral = WpfRepoFiles.ReadResx(WpfRepoFiles.NeutralResxPath);
            var translation = WpfRepoFiles.ReadResx(WpfRepoFiles.PathOf("Translations", fileName));

            var missing = neutral.Keys.Except(translation.Keys).Order().ToArray();
            var orphaned = translation.Keys.Except(neutral.Keys).Order().ToArray();
            var blank = translation.Where(kvp => string.IsNullOrWhiteSpace(kvp.Value)).Select(kvp => kvp.Key).Order().ToArray();

            Assert.True(missing.Length == 0, $"{fileName} is missing keys from Strings.resx: {string.Join(", ", missing)}");
            Assert.True(orphaned.Length == 0, $"{fileName} has keys that are not in Strings.resx: {string.Join(", ", orphaned)}");
            Assert.True(blank.Length == 0, $"{fileName} has blank values for: {string.Join(", ", blank)}");
        }

        // Verifies every translated string uses the same {n} format items as the English string, and is itself a valid composite format.
        // Why: the GUI passes these strings to string.Format with the English argument count. A translation that references {1} when only one
        // argument is passed, or that contains a stray brace, throws FormatException only when that language is selected; one that drops {0}
        // silently loses information (e.g. which file already exists).
        [Theory]
        [MemberData(nameof(TranslationResxFiles))]
        public void Translation_KeepsTheNeutralFormatItems(string fileName)
        {
            var neutral = WpfRepoFiles.ReadResx(WpfRepoFiles.NeutralResxPath);
            var translation = WpfRepoFiles.ReadResx(WpfRepoFiles.PathOf("Translations", fileName));

            var problems = new List<string>();
            foreach (var (key, neutralValue) in neutral)
            {
                if (!translation.TryGetValue(key, out var translatedValue))
                    continue; // Reported by Translation_HasExactlyTheNeutralKeys_WithNonEmptyValues

                var expected = FormatItemIndices(neutralValue);
                var actual = FormatItemIndices(translatedValue);
                if (!expected.SetEquals(actual))
                {
                    problems.Add($"{key}: expected {{{string.Join("},{", expected.Order())}}} but found {{{string.Join("},{", actual.Order())}}}");
                    continue;
                }

                if (expected.Count > 0 && !IsValidCompositeFormat(translatedValue))
                {
                    problems.Add($"{key}: '{translatedValue}' is not a valid composite format string");
                }
            }

            Assert.True(problems.Count == 0, $"{fileName} has format item mismatches:\n{string.Join("\n", problems)}");
        }

        // Verifies the neutral strings that contain format items are themselves valid composite formats.
        // Why: these are the strings every user sees by default; a typo such as "{0" would throw in string.Format for everyone.
        [Fact]
        public void NeutralFormatStrings_AreValidCompositeFormats()
        {
            var invalid = WpfRepoFiles.ReadResx(WpfRepoFiles.NeutralResxPath)
                .Where(kvp => FormatItemIndices(kvp.Value).Count > 0 && !IsValidCompositeFormat(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToArray();

            Assert.True(invalid.Length == 0, $"Strings.resx has invalid composite format strings: {string.Join(", ", invalid)}");
        }

        // Verifies every {lex:Loc Key} used in the WPF XAML refers to a key that exists in Strings.resx.
        // Why: XAML localization keys are resolved by WPFLocalizeExtension at runtime, so a typo or a key deleted from the resx compiles fine
        // and the control shows a "Key: X" placeholder (or nothing) instead of text.
        // The comparison ignores case because WPFLocalizeExtension's ResxLocalizationProvider defaults to IgnoreCase = true
        // (e.g. PageVodDownload.xaml's "OauthTooltip" resolves to the "OAuthTooltip" resource).
        [Fact]
        public void XamlLocalizationKeys_ExistInNeutralResx()
        {
            var neutralKeys = WpfRepoFiles.ReadResx(WpfRepoFiles.NeutralResxPath).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var unknown = Directory.GetFiles(WpfRepoFiles.WpfDirectory, "*.xaml")
                .SelectMany(file => LexLocRegex().Matches(File.ReadAllText(file)).Select(m => (File: Path.GetFileName(file), Key: m.Groups[1].Value)))
                .Where(usage => !neutralKeys.Contains(usage.Key))
                .Select(usage => $"{usage.File}: {usage.Key}")
                .Distinct()
                .ToArray();

            Assert.True(unknown.Length == 0, $"XAML references localization keys missing from Strings.resx:\n{string.Join("\n", unknown)}");
        }

        // Verifies every property in the generated Strings.Designer.cs still has a matching key in Strings.resx.
        // Why: the designer file is only regenerated by Visual Studio/Rider, not by `dotnet build`. If a key is removed or renamed in the resx
        // without regenerating, code keeps compiling against the stale property, which then returns null at runtime (blank text or a
        // NullReferenceException in string.Format callers).
        [Fact]
        public void DesignerProperties_AllHaveAResxKey()
        {
            var neutralKeys = WpfRepoFiles.ReadResx(WpfRepoFiles.NeutralResxPath).Keys.ToHashSet();
            var designer = File.ReadAllText(WpfRepoFiles.PathOf("Translations", "Strings.Designer.cs"), Encoding.UTF8);

            var designerKeys = DesignerGetStringRegex().Matches(designer).Select(m => m.Groups[1].Value).ToArray();
            var stale = designerKeys.Where(k => !neutralKeys.Contains(k)).ToArray();

            Assert.NotEmpty(designerKeys);
            Assert.True(stale.Length == 0, $"Strings.Designer.cs has properties with no key in Strings.resx (regenerate the designer): {string.Join(", ", stale)}");
        }

        private static HashSet<int> FormatItemIndices(string value) =>
            FormatItemIndexRegex().Matches(value).Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();

        private static bool IsValidCompositeFormat(string value)
        {
            try
            {
                CompositeFormat.Parse(value);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
