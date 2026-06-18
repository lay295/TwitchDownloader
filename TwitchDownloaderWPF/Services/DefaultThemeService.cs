using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TwitchDownloaderCore.Extensions;

namespace TwitchDownloaderWPF.Services
{
    public static class DefaultThemeService
    {
        public static bool WriteIncludedThemes()
        {
            var success = true;
            var resourceNames = GetResourceNames();
            var themeResourcePaths = resourceNames.Where(i => i.StartsWith($"{nameof(TwitchDownloaderWPF)}.Themes."));

            foreach (var themeResourcePath in themeResourcePaths)
            {
                using var themeStream = GetResourceStream(themeResourcePath);
                if (themeStream is null) continue;

                var themeName = themeResourcePath.GetNthOccurrence('.', ^2);
                var themeExtension = Path.GetExtension(themeResourcePath.AsSpan());
                var themeFullPath = Path.Combine("Themes", $"{themeName}{themeExtension}");

                try
                {
                    using var fs = new FileStream(themeFullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    themeStream.CopyTo(fs);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
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
