using System;
using System.IO;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCore.Services;

namespace TwitchDownloaderCLI.Modes
{
    internal static class CacheHandler
    {
        public static void ParseArgs(CacheArgs args)
        {
            if (args.ForceClearCache)
            {
                ClearTempCache();
            }
            else if (args.ClearCache)
            {
                PromptClearCache();
            }

            // TODO: Add option to print out cache information (i.e. individual sub-directory size, maybe in table form?)
            // TODO: Add interactive cache delete mode (i.e. loop over each sub-directory with Yes/No delete prompts)
            // TODO: Allow the user to specify a cache folder so it can be managed with the aforementioned tools
        }

        private static void PromptClearCache()
        {
            Console.WriteLine("Are you sure you want to clear the cache? This should really only be done if the program isn't working correctly.");
            while (true)
            {
                Console.Write("[Y] Yes / [N] No: ");
                var userInput = Console.ReadLine()!.Trim().ToLower();
                switch (userInput)
                {
                    case "y" or "yes":
                        ClearTempCache();
                        return;
                    case "n" or "no":
                        return;
                }
            }
        }

        private static void ClearTempCache()
        {
            var defaultCacheDirectory = CacheDirectoryService.GetCacheDirectory(Path.GetTempPath());
            if (Directory.Exists(defaultCacheDirectory))
            {
                Console.WriteLine("Clearing cache...");
                if (CacheDirectoryService.ClearCacheDirectory(Path.GetTempPath(), out var exception))
                {
                    Console.WriteLine("Cache cleared successfully.");
                    return;
                }

                Console.WriteLine($"Failed to clear cache: {exception.Message}");
                return;
            }

            Console.WriteLine("No cache to clear.");
        }
    }
}