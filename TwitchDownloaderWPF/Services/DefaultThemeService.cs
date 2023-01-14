using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TwitchDownloaderWPF.Services
{
    public class DefaultThemeService
    {
        public static void WriteIncludedThemes()
        {
            var resourceNames = GetResourceNames();
            var themePaths = resourceNames.Where((i) => i.StartsWith($"{nameof(TwitchDownloaderWPF)}.Themes."));

            foreach (var themePath in themePaths)
            {
                var themeData = ReadResource(themePath);
                var themePathSplit = themePath.Split(".");

                var themeName = themePathSplit[^2];
                var themeExtension = themePathSplit[^1];
                var themeFullName = $"{themeName}.{themeExtension}";

                try
                {
                    File.WriteAllText(Path.Combine("Themes", themeFullName), themeData);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (System.Security.SecurityException) { }
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
