using TwitchDownloaderCLI.Models;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Services;

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

        private FileInfo PromptUser(FileInfo fileInfo)
        {
            var behavior = UserPrompt.ShowOverwriteRenameExit($"The file '{fileInfo.FullName}' already exists.");

            switch (behavior)
            {
                case OverwriteBehavior.Overwrite:
                    return fileInfo;
                case OverwriteBehavior.Rename:
                    return FilenameService.GetNonCollidingName(fileInfo);
                case OverwriteBehavior.Exit:
                    Environment.Exit(1);
                    return null;
                case null:
                    _logger.LogError("Could not read user input. Please specify the desired collision behavior with the CLI argument and try again.");
                    Environment.Exit(1);
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(behavior), behavior, null);
            }
        }
    }
}