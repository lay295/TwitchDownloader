namespace TwitchDownloaderAvalonia.Services
{
    public sealed class AbandonedVideoCacheService
    {
        private readonly Func<DirectoryInfo[], DirectoryInfo[]> _prompt;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public AbandonedVideoCacheService(IDialogService dialogs) : this(dialogs.PromptAbandonedVideoCaches) { }

        internal AbandonedVideoCacheService(Func<DirectoryInfo[], DirectoryInfo[]> prompt)
        {
            _prompt = prompt;
        }

        public DirectoryInfo[] Handle(DirectoryInfo[] directories)
        {
            if (directories.Length == 0)
                return [];

            _gate.Wait();
            try
            {
                return _prompt(directories);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
