using System;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCLI.Tools
{
    public static class UserPrompt
    {
        public static UserPromptResult ShowYesNo(string message, ITaskLogger logger = null)
        {
            Console.WriteLine(message);

            while (true)
            {
                Console.Write("[Y] Yes / [N] No: ");

                var userInput = Console.ReadLine();
                if (userInput is null)
                {
                    Console.WriteLine();
                    LogError("Could not read user input.", logger);
                    return UserPromptResult.Unknown;
                }

                switch (userInput.Trim().ToLower())
                {
                    case "y" or "yes":
                        return UserPromptResult.Yes;
                    case "n" or "no":
                        return UserPromptResult.No;
                }
            }
        }

        private static void LogError(string message, ITaskLogger logger = null)
        {
            if (logger is null)
            {
                Console.WriteLine($"[ERROR] - {message}");
                return;
            }

            logger.LogError(message);
        }
    }
}