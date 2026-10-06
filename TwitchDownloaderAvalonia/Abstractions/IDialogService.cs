namespace TwitchDownloaderAvalonia.Abstractions
{
    /// <summary>
    /// Modal UI prompts used by view models and background download callbacks.
    /// </summary>
    /// <remarks>
    /// Implementations own the Avalonia window owner. Callers must not assume a window
    /// exists; the missing owner is typically no-ops or returns a cancel/empty result.
    /// Synchronous prompt methods must be invoked from a background thread — they block
    /// on the UI dispatcher and will deadlock if called on the UI thread.
    /// </remarks>
    public interface IDialogService
    {
        /// <summary>
        /// Shows an error dialog. Equivalent to <see cref="ShowMessageAsync"/> for the default UI.
        /// </summary>
        Task ShowErrorAsync(string title, string message);

        /// <summary>
        /// Shows a modal message dialog with an OK button.
        /// </summary>
        Task ShowMessageAsync(string title, string message);

        /// <summary>
        /// Shows a modal confirmation dialog with OK and Cancel.
        /// </summary>
        /// <returns><see langword="true"/> if the user confirmed; otherwise <see langword="false"/>.</returns>
        Task<bool> ShowConfirmAsync(string title, string message);

        /// <summary>
        /// Copies <paramref name="text"/> to the application clipboard when an owner window is available.
        /// </summary>
        Task CopyTextAsync(string text);

        /// <summary>
        /// Reads text from the application clipboard when an owner window is available.
        /// </summary>
        /// <returns>Clipboard text, or <see langword="null"/> if empty or unavailable.</returns>
        Task<string?> GetClipboardTextAsync();

        /// <summary>
        /// Shows the mass-enqueue options dialog (folder, qualities, chat/render toggles).
        /// </summary>
        /// <param name="hasVods">Whether the selection includes VOD items (enables VOD-only options).</param>
        /// <param name="hasRecordingVods">Whether any VOD is still live/recording.</param>
        /// <returns>Chosen options, or <see langword="null"/> if the user cancelled.</returns>
        Task<EnqueueOptions?> ShowEnqueueOptionsAsync(bool hasVods, bool hasRecordingVods);

        /// <summary>
        /// Shows the paste-a-list-of-URLs dialog and enqueues accepted items via <paramref name="enqueue"/>.
        /// </summary>
        Task ShowUrlListAsync(ThumbnailService thumbnails, QueueEnqueueService enqueue);

        /// <summary>
        /// Blocks until the user chooses how to resolve a file name collision.
        /// </summary>
        /// <remarks>
        /// Must not run on the UI thread. Used as a sync callback from Core download APIs.
        /// </remarks>
        CollisionPromptResult PromptCollision(string fileName, string fullPath);

        /// <summary>
        /// Blocks until the user picks which abandoned VOD cache folders to delete.
        /// </summary>
        /// <remarks>
        /// Must not run on the UI thread. Used as a sync callback from Core download APIs.
        /// </remarks>
        /// <returns>Directories the user chose to delete; empty if cancelled or none selected.</returns>
        DirectoryInfo[] PromptAbandonedVideoCaches(DirectoryInfo[] directories);
    }
}
