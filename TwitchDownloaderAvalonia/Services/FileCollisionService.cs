using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderCore.Services;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class FileCollisionService(SettingsService settings, DialogService dialogs)
    {
        private CollisionBehavior? _sessionBehavior;

        public void ResetSessionBehavior() => _sessionBehavior = null;

        public FileInfo? HandleCollision(FileInfo fileInfo)
        {
            var behavior = _sessionBehavior ?? settings.Current.FileCollisionBehavior;
            if (behavior is not CollisionBehavior.Prompt)
                return Apply(fileInfo, behavior);

            var result = dialogs.PromptCollision(fileInfo.Name, fileInfo.FullName);
            if (result.Remember)
            {
                _sessionBehavior = result.Choice switch
                {
                    CollisionChoice.Overwrite => CollisionBehavior.Overwrite,
                    CollisionChoice.Rename => CollisionBehavior.Rename,
                    _ => CollisionBehavior.Cancel,
                };
            }

            var chosen = result.Choice switch
            {
                CollisionChoice.Overwrite => CollisionBehavior.Overwrite,
                CollisionChoice.Rename => CollisionBehavior.Rename,
                _ => CollisionBehavior.Cancel,
            };

            return Apply(fileInfo, chosen);
        }

        private static FileInfo? Apply(FileInfo fileInfo, CollisionBehavior behavior)
        {
            return behavior switch
            {
                CollisionBehavior.Overwrite => fileInfo,
                CollisionBehavior.Rename => FilenameService.GetNonCollidingName(fileInfo),
                CollisionBehavior.Cancel => null,
                _ => fileInfo,
            };
        }
    }
}
