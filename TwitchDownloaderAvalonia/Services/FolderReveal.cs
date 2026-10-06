namespace TwitchDownloaderAvalonia.Services
{
    internal static class FolderReveal
    {
        /// <summary>
        /// explorer.exe /select fails on very long paths even when the file itself can be stored.
        /// </summary>
        internal const int WINDOWS_SELECT_PATH_LIMIT = 259;

        public static bool CanSelectFile(string path, bool fileExists, bool isWindows)
        {
            if (!fileExists)
                return false;

            return !isWindows || path.Length <= WINDOWS_SELECT_PATH_LIMIT;
        }
    }
}
