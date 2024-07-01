using System;
using System.IO;
using System.Linq;

namespace TwitchDownloaderCLI.Tools;

internal static class PathUtils {
    // https://stackoverflow.com/a/3856090/12204538
    public static bool ExistsOnPATH(string fileName) => GetFileOnPATH(fileName) != null;

    /// <returns>
    ///     The path to <paramref name="fileName" /> as specified in the PATH environment variable, or
    ///     <see langword="null" /> if was not found
    /// </returns>
    public static string GetFileOnPATH(string fileName) {
        if (File.Exists(fileName))
            return Path.GetFullPath(fileName);

        var environmentPath
            = Environment.GetEnvironmentVariable("PATH")!; // environment variable is case sensitive on Linux
        return environmentPath
            .Split(Path.PathSeparator)
            .Select(path => Path.Combine(path, fileName!))
            .FirstOrDefault(fullPath => File.Exists(fullPath));

    }
}
