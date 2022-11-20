using System;
using System.IO;

namespace TwitchDownloaderCLI.Tools
{
    public static class PathExtensions
    {
        // https://stackoverflow.com/a/3856090/12204538
        public static bool ExistsOnPath(string fileName)
        {
            return GetFileOnPath(fileName) != null;
        }

        public static string GetFileOnPath(string fileName)
        {
            if (File.Exists(fileName))
            {
                return Path.GetFullPath(fileName);
            }

            var environmentPath = Environment.GetEnvironmentVariable("PATH"); // environment variable is case sensitive on Linux
            foreach (var path in environmentPath.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            return null;
        }
    }
}
