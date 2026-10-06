using System.Windows.Input;

namespace TwitchDownloaderAvalonia.Abstractions
{
    /// <summary>
    /// Paste and clear commands for a URL or file-path text box.
    /// </summary>
    /// <remarks>
    /// Bound by <c>UrlBoxActions</c> as the box's inner-right content. Paste overwrites the
    /// current text from the clipboard; Clear sets it to empty. Implementations no-op when
    /// clipboard text is missing.
    /// </remarks>
    public interface IUrlBoxHost
    {
        /// <summary>
        /// Replaces the box text with clipboard contents when they are non-empty.
        /// </summary>
        ICommand PasteCommand { get; }

        /// <summary>
        /// Clears the box text.
        /// </summary>
        ICommand ClearCommand { get; }
    }
}
