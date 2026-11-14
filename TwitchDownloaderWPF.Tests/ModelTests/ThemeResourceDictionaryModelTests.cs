using System.Windows.Media;
using System.Xml.Serialization;
using TwitchDownloaderWPF.Models;

namespace TwitchDownloaderWPF.Tests.ModelTests
{
    // Tests ThemeResourceDictionaryModel (with SolidColorBrushModel and BooleanModel), the XmlSerializer contract ThemeService uses to read
    // theme .xaml files. The XML namespaces on these attributes must match the XAML namespaces in the theme files exactly; if they drift,
    // deserialization silently yields empty lists and every theme stops changing colours.
    public class ThemeResourceDictionaryModelTests
    {
        private static ThemeResourceDictionaryModel LoadBundledTheme(string fileName)
        {
            using var stream = typeof(ThemeResourceDictionaryModel).Assembly.GetManifestResourceStream($"TwitchDownloaderWPF.Themes.{fileName}")!;
            using var reader = new StreamReader(stream);
            return (ThemeResourceDictionaryModel)new XmlSerializer(typeof(ThemeResourceDictionaryModel)).Deserialize(reader)!;
        }

        // Verifies each bundled theme deserializes with all brushes populated and every colour parseable by WPF.
        // Why: this is exactly what ThemeService.ChangeThemePath does on every theme change; a namespace mismatch yields zero brushes,
        // and an unparseable colour is skipped there without any error.
        [Theory]
        [InlineData("Dark.xaml")]
        [InlineData("Light.xaml")]
        public void BundledTheme_DeserializesWithParseableBrushes(string fileName)
        {
            var theme = LoadBundledTheme(fileName);

            Assert.NotEmpty(theme.SolidColorBrush);
            Assert.All(theme.SolidColorBrush, brush =>
            {
                Assert.False(string.IsNullOrEmpty(brush.Key));
                Assert.IsType<Color>(ColorConverter.ConvertFromString(brush.Color));
            });
        }

        // Verifies the boolean flags come through with the right values: Dark turns on the dark title bar and HandyControl skin, Light turns them off.
        // Why: these booleans are the only way a theme switches the window chrome and HandyControl widgets; if BooleanModel stopped binding,
        // both would stay false and the dark theme would have a white title bar and light controls.
        [Theory]
        [InlineData("Dark.xaml", true)]
        [InlineData("Light.xaml", false)]
        public void BundledTheme_HasExpectedDarkFlags(string fileName, bool expected)
        {
            var flags = LoadBundledTheme(fileName).Boolean.ToDictionary(b => b.Key, b => b.Value);

            Assert.Equal(expected, flags["DarkTitleBar"]);
            Assert.Equal(expected, flags["DarkHandyControl"]);
        }
    }
}
