namespace TwitchDownloaderAvalonia.Services
{
    internal sealed class SearchRequestCoordinator : IDisposable
    {
        private CancellationTokenSource? _current;

        public CancellationToken StartNew()
        {
            var next = new CancellationTokenSource();
            var previous = Interlocked.Exchange(ref _current, next);
            if (previous is null)
                return next.Token;

            try
            {
                previous.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            previous.Dispose();

            return next.Token;
        }

        public bool IsCurrent(CancellationToken token)
        {
            var current = Volatile.Read(ref _current);
            return current is not null && current.Token == token;
        }

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _current, null);
            if (current is null)
                return;

            try
            {
                current.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            current.Dispose();
        }
    }
}
