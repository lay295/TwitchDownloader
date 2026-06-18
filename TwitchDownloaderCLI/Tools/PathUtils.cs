using System;
using System.IO;

namespace TwitchDownloaderCLI.Tools
{
    internal static class PathUtils
    {
        // https://stackoverflow.com/a/3856090/12204538
        public static bool ExistsOnPATH(string fileName)
        {
            return GetFileOnPATH(fileName) != null;
        }

        /// <returns>The path to <paramref name="fileName"/> as specified in the PATH environment variable, or <see langword="null"/> if was not found</returns>
        public static string GetFileOnPATH(string fileName)
        {
            if (File.Exists(fileName))
            {
                return Path.GetFullPath(fileName);
            }

            var environmentPath = Environment.GetEnvironmentVariable("PATH")!; // environment variable is case sensitive on Linux
            foreach (var path in environmentPath.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName!);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }
    }
}