using System.Security;

namespace TwitchDownloaderAvalonia.Services
{
    internal sealed class ThemePackCatalog(string themeDirectory)
    {
        private const string README_RESOURCE = "TwitchDownloaderAvalonia.Themes.README.txt";

        public string ThemeDirectory { get; } = themeDirectory;

        public IReadOnlyList<string> ScanPacks()
        {
            if (!Directory.Exists(ThemeDirectory))
                return [];

            var names = new List<string>();
            string[] files;
            try
            {
                files = Directory.GetFiles(ThemeDirectory);
            }
            catch (Exception ex) when (IsIo(ex))
            {
                return [];
            }

            foreach (var file in files)
            {
                if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name) || !ThemePackKeys.ShouldListPack(name))
                    continue;

                names.Add(name);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public IReadOnlyList<ThemePackInfo> ScanPackInfos()
        {
            var infos = new List<ThemePackInfo>();
            foreach (var name in ScanPacks())
            {
                var path = FindPackPath(name);
                if (path is null)
                    continue;

                try
                {
                    var json = File.ReadAllText(path);
                    if (ThemePackLoader.TryPeekIsDark(json, out var isDark))
                        infos.Add(new ThemePackInfo(name, isDark));
                }
                catch (Exception ex) when (IsIo(ex))
                {
                }
            }

            return infos;
        }

        public string? FindPackPath(string name)
        {
            if (!Directory.Exists(ThemeDirectory))
                return null;

            try
            {
                foreach (var file in Directory.GetFiles(ThemeDirectory))
                {
                    if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.Equals(name, StringComparison.OrdinalIgnoreCase) && ThemePackKeys.ShouldListPack(fileName))
                        return file;
                }
            }
            catch (Exception ex) when (IsIo(ex))
            {
                return null;
            }

            return null;
        }

        public bool EnsureIncludedPacks()
        {
            try
            {
                Directory.CreateDirectory(ThemeDirectory);
            }
            catch (Exception ex) when (IsIo(ex))
            {
                return false;
            }

            var readme = ReadIncludedReadme();
            var success = readme is not null && WriteIfMissing("README.txt", readme);

            foreach (var sample in ThemePackPalettes.Samples)
                success &= WriteIfMissing(sample.FileName, ThemePackLoader.SerializeSample(sample));

            return success;
        }

        private static string? ReadIncludedReadme()
        {
            using var stream = typeof(ThemePackCatalog).Assembly.GetManifestResourceStream(README_RESOURCE);
            if (stream is null)
                return null;

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private bool WriteIfMissing(string fileName, string contents)
        {
            var path = Path.Combine(ThemeDirectory, fileName);
            if (File.Exists(path))
                return true;

            try
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(contents);
            }
            catch (Exception ex) when (IsIo(ex) || ex is ArgumentException)
            {
            }

            return File.Exists(path);
        }

        private static bool IsIo(Exception ex)
        {
            return ex is IOException or UnauthorizedAccessException or SecurityException;
        }
    }
}
