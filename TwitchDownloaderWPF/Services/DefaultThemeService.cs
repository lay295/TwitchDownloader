using System.IO;
using System.Linq;
using System.Reflection;

namespace TwitchDownloader.Tools
{
    public class DefaultThemeService
    {
        public static void WriteIncludedThemes()
        {
            var resourceNames = GetResourceNames();
            var themePaths = resourceNames.Where((i) => i.StartsWith($"{nameof(TwitchDownloader)}.Themes."));

            foreach (var themePath in themePaths)
            {
                var themeData = ReadResource(themePath);
                var themePathSplit = themePath.Split(".");

                var themeName = themePathSplit[^2];
                var themeExtension = themePathSplit[^1];
                var themeFullName = $"{themeName}.{themeExtension}";

                File.WriteAllText(Path.Combine("Themes", themeFullName), themeData);
            }
        }

        private static string[] GetResourceNames()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceNames();
        }

        private static string ReadResource(string resourcePath)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using var manifestStream = assembly.GetManifestResourceStream(resourcePath);
            using var streamReader = new StreamReader(manifestStream);

            return streamReader.ReadToEnd();
        }
    }
}
