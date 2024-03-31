using System;
using System.IO;
using TwitchDownloaderCLI.Modes.Arguments;

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
                Console.Write("[Y]es / [N]o: ");
                var userInput = Console.ReadLine()!.Trim().ToLower();
                switch (userInput)
                {
                    case "y":
                    case "ye":
                    case "yes":
                        ClearTempCache();
                        return;
                    case "n":
                    case "no":
                        return;
                    default:
                        Console.Write("Invalid input. ");
                        continue;
                }
            }
        }

        private static void ClearTempCache()
        {
            var defaultCacheDirectory = Path.Combine(Path.GetTempPath(), "TwitchDownloader");
            if (Directory.Exists(defaultCacheDirectory))
            {
                Console.WriteLine("Clearing cache...");
                try
                {
                    Directory.Delete(defaultCacheDirectory, true);
                    Console.WriteLine("Cache cleared successfully.");
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("Insufficient access to clear cache folder.");
                }
                return;
            }

            Console.WriteLine("No cache to clear.");
        }
    }
}