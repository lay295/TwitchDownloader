using TwitchDownloaderCLI.Models;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCLI.Tools
{
    public static class UserPrompt
    {
        public static UserPromptResult ShowYesNo(string message, ITaskLogger logger = null)
            => ShowYesNo(message, GetInteractiveInput(), logger);

        /// <param name="input">The reader to read the response from, or <see langword="null"/> if there is no interactive user to prompt.</param>
        public static UserPromptResult ShowYesNo(string message, TextReader input, ITaskLogger logger)
        {
            var response = Show(message, "[Y] Yes / [N] No: ", input, static userInput => userInput switch
            {
                "y" or "yes" => UserPromptResult.Yes,
                "n" or "no" => UserPromptResult.No,
                _ => (UserPromptResult?)null
            });

            if (response is null)
            {
                LogError("Could not read user input.", logger);
                return UserPromptResult.Unknown;
            }

            return response.Value;
        }

        public static OverwriteBehavior? ShowOverwriteRenameExit(string message)
            => ShowOverwriteRenameExit(message, GetInteractiveInput());

        /// <param name="input">The reader to read the response from, or <see langword="null"/> if there is no interactive user to prompt.</param>
        /// <returns>The chosen <see cref="OverwriteBehavior"/>, or <see langword="null"/> if no response could be read.</returns>
        public static OverwriteBehavior? ShowOverwriteRenameExit(string message, TextReader input)
            => Show(message, "[O] Overwrite / [R] Rename / [E] Exit: ", input, static userInput => userInput switch
            {
                "o" or "overwrite" => OverwriteBehavior.Overwrite,
                "r" or "rename" => OverwriteBehavior.Rename,
                "e" or "exit" => OverwriteBehavior.Exit,
                _ => (OverwriteBehavior?)null
            });

        /// <returns>The parsed response, or <see langword="null"/> if no response could be read.</returns>
        private static T? Show<T>(string message, string choices, TextReader input, Func<string, T?> parseResponse) where T : struct
        {
            // Deliberate use of Console.WriteLine instead of logger. Do not change.
            Console.WriteLine(message);

            // There is nobody to answer the prompt, and reading anyway may block until the process is killed.
            if (input is null)
            {
                return null;
            }

            while (true)
            {
                Console.Write(choices);

                var userInput = input.ReadLine();
                if (userInput is null)
                {
                    Console.WriteLine();
                    return null;
                }

                var response = parseResponse(userInput.Trim().ToLower());
                if (response is not null)
                {
                    return response;
                }
            }
        }

        /// <returns>The standard input reader, or <see langword="null"/> if standard input is redirected and therefore has no interactive user.</returns>
        private static TextReader GetInteractiveInput() => Console.IsInputRedirected ? null : Console.In;

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