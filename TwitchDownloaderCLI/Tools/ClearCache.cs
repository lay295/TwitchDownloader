using System;
using System.IO;

namespace TwitchDownloaderCLI.Tools
{
    internal class ClearCache
    {
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
                Environment.Exit(0);
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
