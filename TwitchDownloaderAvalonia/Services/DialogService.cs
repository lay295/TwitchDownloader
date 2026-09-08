using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.ViewModels;
using TwitchDownloaderAvalonia.Views;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class DialogService(SettingsService settings, FileDialogService files)
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

            if (Dispatcher.UIThread.CheckAccess())
                throw new InvalidOperationException("File collision prompts cannot block the UI thread. Call from a background thread.");

            return Dispatcher.UIThread.InvokeAsync(() => ShowCollisionCoreAsync(fileName, fullPath))
                .GetAwaiter()
                .GetResult();
        }

        private async Task ShowMessageCoreAsync(string title, string message)
        {
            var dialog = new MessageDialog();
            dialog.DataContext = new MessageDialogViewModel(title, message, result => dialog.Close(result));
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
            dialog.DataContext = new MessageDialogViewModel(title, message, result => dialog.Close(result), showCancel: true);
            return await dialog.ShowDialog<bool>(_owner!);
        }

        private async Task<CollisionPromptResult> ShowCollisionCoreAsync(string fileName, string fullPath)
        {
            var dialog = new CollisionDialog();
            dialog.DataContext = new CollisionDialogViewModel(fileName, fullPath, promptResult => dialog.Close(promptResult));
            return await dialog.ShowDialog<CollisionPromptResult>(_owner!);
        }

        public async Task<EnqueueOptions?> ShowEnqueueOptionsAsync(bool includeAudioOnly)
        {
            if (_owner is null)
                return null;

            if (Dispatcher.UIThread.CheckAccess())
                return await ShowEnqueueOptionsCoreAsync(includeAudioOnly);

            return await Dispatcher.UIThread.InvokeAsync(() => ShowEnqueueOptionsCoreAsync(includeAudioOnly));
        }

        private async Task<EnqueueOptions?> ShowEnqueueOptionsCoreAsync(bool includeAudioOnly)
        {
            var dialog = new EnqueueOptionsDialog();
            dialog.DataContext = new EnqueueOptionsViewModel(settings, files, includeAudioOnly, dialog.Close);
            return await dialog.ShowDialog<EnqueueOptions?>(_owner!);
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
