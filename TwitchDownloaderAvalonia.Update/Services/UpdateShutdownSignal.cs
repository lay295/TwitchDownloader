namespace TwitchDownloaderAvalonia.Update.Services
{
    public static class UpdateShutdownSignal
    {
        public static string GetPath(int processId)
        {
            return Path.Combine(Path.GetTempPath(), $"TwitchDownloaderAvalonia.update-shutdown-{processId}");
        }

        public static void Clear(int processId)
        {
            TryDelete(GetPath(processId));
        }

        public static bool Signal(int processId)
        {
            try
            {
                File.WriteAllText(GetPath(processId), "1");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task WaitAsync(int processId, CancellationToken cancellationToken = default)
        {
            var path = GetPath(processId);
            while (!File.Exists(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignored
            }
        }
    }
}
