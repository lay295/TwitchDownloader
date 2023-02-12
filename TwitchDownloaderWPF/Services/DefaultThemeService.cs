using System.IO;
using System.Linq;
using System.Reflection;

namespace TwitchDownloaderWPF.Services
{
    public class DefaultThemeService
    {
        public static bool WriteIncludedThemes()
        {
            var success = true;
            var resourceNames = GetResourceNames();
            var themeResourcePaths = resourceNames.Where(i => i.StartsWith($"{nameof(TwitchDownloaderWPF)}.Themes."));

            foreach (var themeResourcePath in themeResourcePaths)
            {
                using var themeStream = GetResourceStream(themeResourcePath);
                var themePathSplit = themeResourcePath.Split(".");

                var themeName = themePathSplit[^2];
                var themeExtension = themePathSplit[^1];
                var themeFullPath = Path.Combine("Themes", $"{themeName}.{themeExtension}");

                try
                {
                    using var fs = new FileStream(themeFullPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096);
                    themeStream.CopyTo(fs);
                }
                catch (IOException) { }
                catch (System.UnauthorizedAccessException) { }
                catch (System.Security.SecurityException) { }

                if (!File.Exists(themeFullPath))
                {
                    success = false;
                }
            }

            return success;
        }

        private static string[] GetResourceNames()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceNames();
        }

        private static Stream GetResourceStream(string resourcePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceStream(resourcePath);
        }
    }
}
