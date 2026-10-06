namespace TwitchDownloaderAvalonia.Services
{
    public sealed class QueueEnqueueService(
        LocalizationService loc,
        SettingsService settings,
        IDialogService dialogs,
        QueueService queue,
        FfmpegService ffmpeg,
        FileCollisionService collision,
        AbandonedVideoCacheService cacheCleaner)
    {
        public async Task<bool> EnqueueAsync(IReadOnlyList<QueueableMedia> items)
        {
            if (items.Count == 0)
                return false;

            var hasVods = items.Any(item => !item.IsClip);
            var hasRecordingVods = items.Any(item => item is { IsClip: false, IsRecording: true });
            var options = await dialogs.ShowEnqueueOptionsAsync(hasVods, hasRecordingVods);
            if (options is null)
                return false;

            try
            {
                if (!Directory.Exists(options.Folder))
                    TwitchHelper.CreateDirectory(options.Folder);
            }
            catch (Exception ex)
            {
                await dialogs.ShowErrorAsync(loc.Get("search.invalid_folder_title"), loc.Get("search.invalid_folder"));
                if (settings.Current.General.VerboseErrors)
                    await dialogs.ShowErrorAsync(loc.Get("dialogs.verbose_error"), ex.ToString());

                return false;
            }

            var tasks = MassEnqueuePlanner.Build(items, options, new MassEnqueueContext
            {
                Settings = settings.Current,
                Localization = loc,
                FfmpegPath = ffmpeg.ResolvedPath,
                CollisionCallback = file => collision.HandleCollision(file)!,
                CacheCleanerCallback = cacheCleaner.Handle,
                LogLevel = queue.LogLevel,
            });

            if (tasks.Count == 0)
                return false;

            queue.EnqueueRange(tasks);
            return true;
        }
    }
}
