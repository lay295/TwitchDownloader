namespace TwitchDownloaderAvalonia.Abstractions
{
    /// <summary>
    /// Native file and folder pickers for save/open flows.
    /// </summary>
    /// <remarks>
    /// Implementations require an owner window. Without one, methods return <see langword="null"/>.
    /// </remarks>
    public interface IFileDialogService
    {
        /// <summary>
        /// Shows a save-file picker.
        /// </summary>
        /// <param name="suggestedFileName">Initial file name in the dialog.</param>
        /// <param name="filterName">Display the name of the file type filter.</param>
        /// <param name="extension">Default extension without or with a leading dot (e.g. <c>mp4</c>).</param>
        /// <returns>Local path, or <see langword="null"/> if canceled or unavailable.</returns>
        Task<string?> SaveFileAsync(string suggestedFileName, string filterName, string extension);

        /// <summary>
        /// Shows an open-file picker for a single file.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="filterName">Display the name of the file type filter.</param>
        /// <param name="patterns">Glob patterns such as <c>*.json</c>.</param>
        /// <returns>Local path, or <see langword="null"/> if cancelled or unavailable.</returns>
        Task<string?> OpenFileAsync(string title, string filterName, IReadOnlyList<string> patterns);

        /// <summary>
        /// Shows a folder picker.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="startPath">Optional starting folder when it exists on disk.</param>
        /// <returns>Local folder path, or <see langword="null"/> if canceled or unavailable.</returns>
        Task<string?> PickFolderAsync(string title, string? startPath = null);
    }
}
