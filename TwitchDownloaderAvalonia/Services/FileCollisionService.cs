namespace TwitchDownloaderAvalonia.Services
{
    public sealed class FileCollisionService
    {
        private readonly SettingsService _settings;
        private readonly Func<string, string, CollisionPromptResult> _prompt;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private CollisionBehavior? _sessionBehavior;

        public FileCollisionService(SettingsService settings, DialogService dialogs) : this(settings, dialogs.PromptCollision) { }

        internal FileCollisionService(SettingsService settings, Func<string, string, CollisionPromptResult> prompt)
        {
            _settings = settings;
            _prompt = prompt;
        }

        public void ResetSessionBehavior()
        {
            _gate.Wait();
            try
            {
                _sessionBehavior = null;
            }
            finally
            {
                _gate.Release();
            }
        }

        public FileInfo? HandleCollision(FileInfo fileInfo)
        {
            _gate.Wait();
            try
            {
                var behavior = _sessionBehavior ?? _settings.Current.FileCollisionBehavior;
                if (behavior is not CollisionBehavior.Prompt)
                    return Apply(fileInfo, behavior);

                var result = _prompt(fileInfo.Name, fileInfo.FullName);
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
            finally
            {
                _gate.Release();
            }
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
