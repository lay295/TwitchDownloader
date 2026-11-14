using System.Xml.Linq;

namespace TwitchDownloaderCLI.Tests.WpfResourceTests
{
    // The WPF project only builds and runs on Windows, but its translation, theme and culture files are plain text.
    // Reading them straight from the repository lets these consistency checks run on every CI platform (and on Linux dev machines).
    internal static class WpfRepoFiles
    {
        private static readonly Lazy<string> WpfDirectoryLazy = new(FindWpfDirectory);

        public static string WpfDirectory => WpfDirectoryLazy.Value;

        public static string PathOf(params string[] parts) => Path.Combine([WpfDirectory, ..parts]);

        public static string NeutralResxPath => PathOf("Translations", "Strings.resx");

        /// <summary>All Strings.&lt;culture&gt;.resx files, excluding the neutral (English) Strings.resx.</summary>
        public static IEnumerable<string> TranslationResxPaths =>
            Directory.GetFiles(PathOf("Translations"), "Strings.*.resx").Order(StringComparer.Ordinal);

        /// <summary>"Strings.pt-br.resx" -> "pt-br"</summary>
        public static string ResxCulture(string resxPath) => Path.GetFileNameWithoutExtension(resxPath)["Strings.".Length..];

        /// <summary>Every &lt;data name="..."&gt;&lt;value&gt;...&lt;/value&gt;&lt;/data&gt; entry, in file order (duplicates kept).</summary>
        public static List<KeyValuePair<string, string>> ReadResxEntries(string resxPath) =>
            XDocument.Load(resxPath).Root!
                .Elements("data")
                .Select(e => new KeyValuePair<string, string>((string)e.Attribute("name")!, (string?)e.Element("value") ?? ""))
                .ToList();

        public static Dictionary<string, string> ReadResx(string resxPath) =>
            ReadResxEntries(resxPath).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        private static string FindWpfDirectory()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "TwitchDownloaderWPF.sln")))
                {
                    return Path.Combine(dir.FullName, "TwitchDownloaderWPF");
                }
            }

            throw new DirectoryNotFoundException($"Could not find TwitchDownloaderWPF.sln in any parent of {AppContext.BaseDirectory}");
        }
    }
}
