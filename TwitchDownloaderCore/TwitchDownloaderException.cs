namespace TwitchDownloaderCore
{
    /// <summary>
    /// Represents an expected, user-facing failure such as a deleted or expired VOD, rather than a programming error.
    /// </summary>
    /// <remarks>
    /// Consumers are expected to present <see cref="Exception.Message"/> to the user as-is. It should not be necessary
    /// to display a stack trace for these failures.
    /// </remarks>
    public class TwitchDownloaderException : Exception
    {
        public TwitchDownloaderException() { }

        public TwitchDownloaderException(string message) : base(message) { }

        public TwitchDownloaderException(string message, Exception innerException) : base(message, innerException) { }
    }
}
