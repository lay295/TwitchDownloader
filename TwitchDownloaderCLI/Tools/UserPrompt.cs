using System;
using TwitchDownloaderCLI.Models;

namespace TwitchDownloaderCLI.Tools
{
    public static class UserPrompt
    {
        public static UserPromptResult ShowYesNo(string message)
        {
            Console.WriteLine(message);

            while (true)
            {
                Console.Write("[Y] Yes / [N] No: ");

                var userInput = Console.ReadLine();
                if (userInput is null)
                {
                    Console.WriteLine();
                    Console.WriteLine("[ERROR] - Could not read user input.");
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
    }
}