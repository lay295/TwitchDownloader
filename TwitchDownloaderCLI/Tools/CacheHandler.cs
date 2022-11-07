using System;
using System.IO;
using TwitchDownloaderCLI.Modes.Arguments;

namespace TwitchDownloaderCLI.Tools
{
    public static class CacheHandler
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
        }

        internal static void PromptClearCache()
        {
            Console.WriteLine("Are you sure you want to clear the cache? This should really only be done if the program isn't working correctly.");
            PromptUser();
        }

        private static void PromptUser()
        {
            Console.Write("[Y]es / [N]o: ");
            string userInput = Console.ReadLine().Trim().ToLower();
            if (userInput.Equals("y") || userInput.Equals("yes"))
            {
                ClearTempCache();
                return;
            }
            else if (userInput.Equals("n") || userInput.Equals("no"))
            {
                return;
            }

            Console.Write("Invalid input. ");
            PromptUser();
        }

        internal static void ClearTempCache()
        {
            string defaultCacheDirectory = Path.Combine(Path.GetTempPath(), "TwitchDownloader");
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
            }
            else
            {
                Console.WriteLine("No cache to clear.");
            }
        }
    }
}
