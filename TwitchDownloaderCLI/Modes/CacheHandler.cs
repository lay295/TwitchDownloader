using System.IO;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Services;

namespace TwitchDownloaderCLI.Modes
{
    internal static class CacheHandler
    {
        public static void ParseArgs(CacheArgs args)
        {
            using var progress = new CliTaskProgress(args.LogLevel);

            if (args.ForceClearCache)
            {
                ClearTempCache(progress);
            }
            else if (args.ClearCache)
            {
                PromptClearCache(progress);
            }

            // TODO: Add option to print out cache information (i.e. individual sub-directory size, maybe in table form?)
            // TODO: Add interactive cache delete mode (i.e. loop over each sub-directory with Yes/No delete prompts)
            // TODO: Allow the user to specify a cache folder so it can be managed with the aforementioned tools
        }

        private static void PromptClearCache(ITaskProgress progress)
        {
            var promptResult = UserPrompt.ShowYesNo("Are you sure you want to clear the cache? This should really only be done if the program isn't working correctly.");
            if (promptResult is UserPromptResult.Yes)
            {
                ClearTempCache(progress);
            }
        }

        private static void ClearTempCache(ITaskProgress progress)
        {
            var baseDirectory = Path.GetTempPath();
            var defaultCacheDirectory = CacheDirectoryService.GetCacheDirectory(baseDirectory);

            if (!Directory.Exists(defaultCacheDirectory))
            {
                progress.LogInfo("No cache to clear.");
                return;
            }

            progress.LogInfo("Clearing cache...");

            if (!CacheDirectoryService.ClearCacheDirectory(baseDirectory, out var exception))
            {
                progress.LogError($"Failed to clear cache: {exception.Message}");
                return;
            }

            progress.LogInfo("Cache cleared successfully.");
        }
    }
}