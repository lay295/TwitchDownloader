using TwitchDownloaderCore.Tests.TestHelpers;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Tests.TestHelpers;

namespace TwitchDownloaderWPF.Tests.ServiceTests
{
    // Tests DefaultThemeService, which on every launch extracts the bundled themes (embedded resources) into the ./Themes folder that
    // ThemeService and the Settings theme dropdown read from. It works relative to the current directory, so each test switches to a temp
    // directory inside the non-parallel collection and switches back afterwards.
    [Collection(ProcessStateCollection.NAME)]
    public sealed class DefaultThemeServiceTests : IDisposable
    {
        private static readonly string[] BundledFiles = ["Dark.xaml", "Light.xaml", "README.txt"];

        private readonly string _originalDirectory = Environment.CurrentDirectory;
        private readonly TempDirectory _tempDir = new();

        public DefaultThemeServiceTests()
        {
            Environment.CurrentDirectory = _tempDir.Path;
        }

        public void Dispose()
        {
            Environment.CurrentDirectory = _originalDirectory;
            _tempDir.Dispose();
        }

        private static byte[] EmbeddedBytes(string fileName)
        {
            using var stream = typeof(DefaultThemeService).Assembly.GetManifestResourceStream($"TwitchDownloaderWPF.Themes.{fileName}")!;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        // Verifies every bundled theme file is written to Themes/ with exactly the embedded content, and the method reports success.
        // Why: the resource-name-to-file-name parsing (GetNthOccurrence on "TwitchDownloaderWPF.Themes.Dark.xaml") decides the file names;
        // if it broke, the theme dropdown would list wrong names and ThemeService would fall back to "System" with a "theme not found" dialog.
        [Fact]
        public void WriteIncludedThemes_WritesEveryBundledFile()
        {
            Directory.CreateDirectory("Themes");

            Assert.True(DefaultThemeService.WriteIncludedThemes());

            Assert.Equal(BundledFiles, Directory.GetFiles("Themes").Select(f => Path.GetFileName(f)).Order());
            Assert.All(BundledFiles, file => Assert.Equal(EmbeddedBytes(file), File.ReadAllBytes(Path.Combine("Themes", file))));
        }

        // Verifies an existing Dark.xaml is replaced with the bundled one.
        // Why: Themes/README.txt promises users that Dark.xaml and Light.xaml are overwritten on launch, which is how theme fixes reach
        // existing installs; a user editing them instead of a copy must not block updates.
        [Fact]
        public void WriteIncludedThemes_OverwritesStaleBundledTheme()
        {
            Directory.CreateDirectory("Themes");
            File.WriteAllText(Path.Combine("Themes", "Dark.xaml"), "stale user edit");

            Assert.True(DefaultThemeService.WriteIncludedThemes());

            Assert.Equal(EmbeddedBytes("Dark.xaml"), File.ReadAllBytes(Path.Combine("Themes", "Dark.xaml")));
        }

        // Verifies the method returns false (rather than throwing) when the files cannot be written, here because Themes/ does not exist.
        // Why: ThemeService shows a "themes failed to write" message based on this result; an exception would crash startup instead.
        [Fact]
        public void WriteIncludedThemes_ReturnsFalseWhenFilesCannotBeWritten()
        {
            Assert.False(DefaultThemeService.WriteIncludedThemes());
        }
    }
}
