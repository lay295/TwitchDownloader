namespace TwitchDownloaderCore.Tests.TestHelpers
{
    /// <summary>
    /// A unique, empty directory under the system temp folder that is deleted (with its contents) on dispose,
    /// so file-system tests never collide with each other or leave junk behind.
    /// </summary>
    public sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public string Combine(params string[] parts) => System.IO.Path.Combine([Path, ..parts]);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
