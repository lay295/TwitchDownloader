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
                var userInput = Console.ReadLine().AsSpan().Trim();

                if (userInput.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                    userInput.Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    return UserPromptResult.Yes;
                }

                if (userInput.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                    userInput.Equals("no", StringComparison.OrdinalIgnoreCase))
                {
                    return UserPromptResult.No;
                }
            }
        }
    }
}