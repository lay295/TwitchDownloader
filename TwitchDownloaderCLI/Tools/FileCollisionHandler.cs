using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderCLI.Tools;

internal class FileCollisionHandler(IFileCollisionArgs collisionArgs) {

    [return: MaybeNull]
    public FileInfo HandleCollisionCallback(FileInfo fileInfo) {
        return collisionArgs.OverwriteBehavior switch {
            OverwriteBehavior.Overwrite => fileInfo,
            OverwriteBehavior.Exit => null,
            OverwriteBehavior.Rename => FilenameService.GetNonCollidingName(fileInfo),
            OverwriteBehavior.Prompt => PromptUser(fileInfo),
            _ => throw new ArgumentOutOfRangeException(
                nameof(collisionArgs.OverwriteBehavior),
                collisionArgs.OverwriteBehavior,
                null
            )
        };
    }

    [return: MaybeNull]
    private static FileInfo PromptUser(FileInfo fileInfo) {
        Console.WriteLine($"The file '{fileInfo.FullName}' already exists.");

        while (true) {
            Console.Write("[O] Overwrite / [R] Rename / [E] Exit: ");
            var userInput = Console.ReadLine()!.Trim().ToLower();
            switch (userInput) {
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
