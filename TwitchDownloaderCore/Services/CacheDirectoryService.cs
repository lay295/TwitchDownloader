using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace TwitchDownloaderCore.Services
{
    public static class CacheDirectoryService
    {
        private const string CACHE_DIRECTORY_SUFFIX = "TwitchDownloader";

        public static string GetCacheDirectory([AllowNull] string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                baseDirectory = Path.GetTempPath();

            baseDirectory = Path.GetFullPath(baseDirectory);

            if (new DirectoryInfo(baseDirectory).Name == CACHE_DIRECTORY_SUFFIX)
            {
                return baseDirectory;
            }

            return Path.Combine(baseDirectory, CACHE_DIRECTORY_SUFFIX);
        }

        public static bool ClearCacheDirectory([AllowNull] string baseDirectory, out Exception exception)
        {
            var cacheDirectory = GetCacheDirectory(baseDirectory);
            if (!Directory.Exists(cacheDirectory))
            {
                exception = null;
                return true;
            }

            try
            {
                Directory.Delete(cacheDirectory, true);
                exception = null;
                return true;
            }
            catch (Exception ex)
            {
                exception = ex;
                return false;
            }
        }
    }
}