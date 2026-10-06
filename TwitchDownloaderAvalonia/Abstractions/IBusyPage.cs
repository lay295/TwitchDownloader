namespace TwitchDownloaderAvalonia.Abstractions
{
    /// <summary>
    /// A content page that can show a blocking loading overlay while work is in progress.
    /// </summary>
    /// <remarks>
    /// Bound by <c>LoadingOverlay</c>. Keep <see cref="IsBusy"/> true for the whole async operation
    /// so the overlay stays up until the page is interactive again.
    /// </remarks>
    public interface IBusyPage
    {
        /// <summary>
        /// Whether the page is currently running Get Info or another blocking operation.
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Shared status used to decide whether the overlay shows the animated mascot.
        /// </summary>
        AppStatus AppStatus { get; }
    }
}
