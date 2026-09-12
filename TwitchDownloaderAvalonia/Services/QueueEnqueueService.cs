namespace TwitchDownloaderAvalonia.Services
{
    public sealed class QueueEnqueueService(
        SettingsService settings,
        DialogService dialogs,
        QueueService queue,
        FfmpegService ffmpeg,
        FileCollisionService collision)
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
                await dialogs.ShowErrorAsync(Loc.Get("search.invalid_folder_title"), Loc.Get("search.invalid_folder"));
                if (settings.Current.VerboseErrors)
                    await dialogs.ShowErrorAsync(Loc.Get("dialogs.verbose_error"), ex.ToString());

                return false;
            }

            var tasks = MassEnqueuePlanner.Build(items, options, new MassEnqueueContext
            {
                Settings = settings.Current,
                FfmpegPath = ffmpeg.ResolvedPath,
                CollisionCallback = file => collision.HandleCollision(file)!,
                LogLevel = queue.LogLevel,
            });

            if (tasks.Count == 0)
                return false;

            queue.EnqueueRange(tasks);
            return true;
        }
    }
}
