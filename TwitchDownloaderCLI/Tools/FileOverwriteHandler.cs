using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCLI.Tools
{
    internal class FileOverwriteHandler
    {
        private readonly IFileOverwriteArgs _overwriteArgs;

        public FileOverwriteHandler(IFileOverwriteArgs overwriteArgs)
        {
            _overwriteArgs = overwriteArgs;
        }

        [return: MaybeNull]
        public FileInfo HandleOverwriteCallback(FileInfo fileInfo)
        {
            return _overwriteArgs.OverwriteBehavior switch
            {
                OverwriteBehavior.Overwrite => fileInfo,
                OverwriteBehavior.Exit => null,
                OverwriteBehavior.Rename => FilenameService.GetNonCollidingName(fileInfo),
                OverwriteBehavior.Prompt => PromptUser(fileInfo),
                _ => throw new ArgumentOutOfRangeException(nameof(_overwriteArgs.OverwriteBehavior), _overwriteArgs.OverwriteBehavior, null)
            };
        }

        [return: MaybeNull]
        private static FileInfo PromptUser(FileInfo fileInfo)
        {
            Console.WriteLine($"{fileInfo.FullName} already exists.");

            while (true)
            {
                Console.Write("[O] Overwrite / [R] Rename / [E] Exit: ");
                var userInput = Console.ReadLine()!.Trim().ToLower();
                switch (userInput)
                {
                    case "o" or "overwrite":
                        return fileInfo;
                    case "e" or "exit":
                        return null;
                    case "r" or "rename":
                        return FilenameService.GetNonCollidingName(fileInfo);
                }
            }
        }
    }
}