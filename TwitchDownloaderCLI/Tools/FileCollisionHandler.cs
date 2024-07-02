using System;
using System.IO;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCLI.Tools
{
    internal class FileCollisionHandler
    {
        private readonly IFileCollisionArgs _collisionArgs;
        private readonly ITaskLogger _logger;

        public FileCollisionHandler(IFileCollisionArgs collisionArgs, ITaskLogger logger)
        {
            _collisionArgs = collisionArgs;
            _logger = logger;
        }

        public FileInfo HandleCollisionCallback(FileInfo fileInfo)
        {
            return _collisionArgs.OverwriteBehavior switch
            {
                OverwriteBehavior.Overwrite => fileInfo,
                OverwriteBehavior.Exit => Exit(fileInfo),
                OverwriteBehavior.Rename => FilenameService.GetNonCollidingName(fileInfo),
                OverwriteBehavior.Prompt => PromptUser(fileInfo),
                _ => throw new ArgumentOutOfRangeException(nameof(_collisionArgs.OverwriteBehavior), _collisionArgs.OverwriteBehavior, null)
            };
        }

        private FileInfo Exit(FileInfo fileInfo)
        {
            _logger.LogInfo($"The file '{fileInfo.FullName}' already exists, exiting.");
            Environment.Exit(1);
            return null;
        }

        private static FileInfo PromptUser(FileInfo fileInfo)
        {
            // Deliberate use of Console.WriteLine instead of logger. Do not change.
            Console.WriteLine($"The file '{fileInfo.FullName}' already exists.");

            while (true)
            {
                Console.Write("[O] Overwrite / [R] Rename / [E] Exit: ");
                var userInput = Console.ReadLine()!.Trim().ToLower();
                switch (userInput)
                {
                    case "o" or "overwrite":
                        return fileInfo;
                    case "e" or "exit":
                        Environment.Exit(1);
                        break;
                    case "r" or "rename":
                        return FilenameService.GetNonCollidingName(fileInfo);
                }
            }
        }
    }
}