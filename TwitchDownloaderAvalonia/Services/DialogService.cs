using Avalonia.Controls;
using Avalonia.Threading;
using TwitchDownloaderAvalonia.ViewModels;
using TwitchDownloaderAvalonia.Views;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class DialogService
    {
        private Window? _owner;

        public void SetOwner(Window owner) => _owner = owner;

        public Task ShowErrorAsync(string title, string message) => ShowMessageAsync(title, message);

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
            dialog.DataContext = new MessageDialogViewModel(title, message, dialog.Close);
            await dialog.ShowDialog(_owner!);
        }

        private async Task<CollisionPromptResult> ShowCollisionCoreAsync(string fileName, string fullPath)
        {
            var dialog = new CollisionDialog();
            dialog.DataContext = new CollisionDialogViewModel(fileName, fullPath, promptResult => dialog.Close(promptResult));
            return await dialog.ShowDialog<CollisionPromptResult>(_owner!);
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
