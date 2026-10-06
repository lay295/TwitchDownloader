using Avalonia.Input.Platform;
using Microsoft.Extensions.Logging;
using TwitchDownloaderAvalonia.ViewModels;
using TwitchDownloaderAvalonia.Views;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class DialogService(
        LocalizationService loc,
        SettingsService settings,
        IFileDialogService files,
        ILoggerFactory loggerFactory) : IDialogService
    {
        private Window? _owner;

        public void SetOwner(Window owner) => _owner = owner;

        public Task ShowErrorAsync(string title, string message) => ShowMessageAsync(title, message);

        public async Task CopyTextAsync(string text)
        {
            if (_owner?.Clipboard is not { } clipboard)
                return;

            if (Dispatcher.UIThread.CheckAccess())
            {
                await clipboard.SetTextAsync(text);
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => clipboard.SetTextAsync(text));
        }

        public async Task<string?> GetClipboardTextAsync()
        {
            if (_owner?.Clipboard is not { } clipboard)
                return null;

            if (Dispatcher.UIThread.CheckAccess())
                return await clipboard.TryGetTextAsync();

            return await Dispatcher.UIThread.InvokeAsync(() => clipboard.TryGetTextAsync());
        }

        public async Task ShowMessageAsync(string title, string message)
        {
            if (_owner is null)
                return;

            if (Dispatcher.UIThread.CheckAccess())
            {
                await ShowMessageCoreAsync(title, message);
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => ShowMessageCoreAsync(title, message));
        }

        /// <summary>
        /// Shows a modal file-collision prompt and returns the user's choice.
        /// </summary>
        /// <remarks>
        /// Core download APIs expose a synchronous collision callback, while Avalonia dialogs are async.
        /// Call this from a background thread (the VOD download path uses <c>Task.Run</c>).
        /// Invoking it on the UI thread would deadlock on <c>GetResult</c>.
        /// </remarks>
        public CollisionPromptResult PromptCollision(string fileName, string fullPath)
        {
            if (_owner is null)
                return new CollisionPromptResult(CollisionChoice.Cancel, false);

            EnsureBackgroundThread("File collision prompts");

            return Dispatcher.UIThread.InvokeAsync(() => ShowCollisionCoreAsync(fileName, fullPath))
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Shows a modal picker for abandoned VOD cache folders and returns the directories the user chose to delete.
        /// </summary>
        /// <remarks>
        /// Core download APIs expose a synchronous cache-cleaner callback, while Avalonia dialogs are async.
        /// Call this from a background thread (the VOD download path uses <c>Task.Run</c>).
        /// Invoking it on the UI thread would deadlock on <c>GetResult</c>.
        /// </remarks>
        public DirectoryInfo[] PromptAbandonedVideoCaches(DirectoryInfo[] directories)
        {
            if (_owner is null || directories.Length == 0)
                return [];

            EnsureBackgroundThread("Abandoned video cache prompts");

            return Dispatcher.UIThread.InvokeAsync(() => ShowAbandonedVideoCachesCoreAsync(directories))
                .GetAwaiter()
                .GetResult();
        }

        internal static void EnsureBackgroundThread(string operation)
        {
            if (Dispatcher.UIThread.CheckAccess())
                throw new InvalidOperationException($"{operation} cannot block the UI thread. Call from a background thread.");
        }

        private async Task ShowMessageCoreAsync(string title, string message)
        {
            var dialog = new MessageDialog();
            dialog.DataContext = new MessageDialogViewModel(loc, title, message, result => dialog.Close(result));
            await dialog.ShowDialog(_owner!);
        }

        public async Task<bool> ShowConfirmAsync(string title, string message)
        {
            if (_owner is null)
                return false;

            if (Dispatcher.UIThread.CheckAccess())
                return await ShowConfirmCoreAsync(title, message);

            return await Dispatcher.UIThread.InvokeAsync(() => ShowConfirmCoreAsync(title, message));
        }

        private async Task<bool> ShowConfirmCoreAsync(string title, string message)
        {
            var dialog = new MessageDialog();
            dialog.DataContext = new MessageDialogViewModel(loc, title, message, result => dialog.Close(result), showCancel: true);
            return await dialog.ShowDialog<bool>(_owner!);
        }

        private async Task<CollisionPromptResult> ShowCollisionCoreAsync(string fileName, string fullPath)
        {
            var dialog = new CollisionDialog();
            dialog.DataContext = new CollisionDialogViewModel(loc, fileName, fullPath, promptResult => dialog.Close(promptResult));
            return await dialog.ShowDialog<CollisionPromptResult>(_owner!);
        }

        public async Task<EnqueueOptions?> ShowEnqueueOptionsAsync(bool hasVods, bool hasRecordingVods)
        {
            if (_owner is null)
                return null;

            if (Dispatcher.UIThread.CheckAccess())
                return await ShowEnqueueOptionsCoreAsync(hasVods, hasRecordingVods);

            return await Dispatcher.UIThread.InvokeAsync(() => ShowEnqueueOptionsCoreAsync(hasVods, hasRecordingVods));
        }

        private async Task<EnqueueOptions?> ShowEnqueueOptionsCoreAsync(bool hasVods, bool hasRecordingVods)
        {
            var dialog = new EnqueueOptionsDialog();
            dialog.DataContext = new EnqueueOptionsViewModel(loc, settings, files, hasVods, hasRecordingVods, dialog.Close);
            return await dialog.ShowDialog<EnqueueOptions?>(_owner!);
        }

        public async Task ShowUrlListAsync(ThumbnailService thumbnails, QueueEnqueueService enqueue)
        {
            if (_owner is null)
                return;

            if (Dispatcher.UIThread.CheckAccess())
            {
                await ShowUrlListCoreAsync(thumbnails, enqueue);
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => ShowUrlListCoreAsync(thumbnails, enqueue));
        }

        private async Task ShowUrlListCoreAsync(ThumbnailService thumbnails, QueueEnqueueService enqueue)
        {
            var dialog = new UrlListDialog();
            var viewModel = new UrlListViewModel(loc, settings, this, thumbnails, enqueue, queued => dialog.Close(queued));
            dialog.DataContext = viewModel;
            dialog.Closing += OnDialogClosing;
            await dialog.ShowDialog(_owner!);
            return;

            void OnDialogClosing(object? sender, WindowClosingEventArgs e) => viewModel.NotifyClosed();
        }

        private async Task<DirectoryInfo[]> ShowAbandonedVideoCachesCoreAsync(DirectoryInfo[] directories)
        {
            var dialog = new AbandonedVideoCacheDialog();
            var viewModel = new AbandonedVideoCacheViewModel(
                loc,
                directories,
                dialog.Close,
                this,
                loggerFactory.CreateLogger<AbandonedVideoCacheViewModel>());

            dialog.DataContext = viewModel;
            viewModel.StartSizeCalculation();

            try
            {
                var result = await dialog.ShowDialog<DirectoryInfo[]?>(_owner!);
                return result ?? [];
            }
            finally
            {
                viewModel.Dispose();
            }
        }
    }

    public enum CollisionChoice
    {
        Cancel = 0,
        Overwrite,
        Rename,
    }

    public readonly record struct CollisionPromptResult(CollisionChoice Choice, bool Remember);
}
