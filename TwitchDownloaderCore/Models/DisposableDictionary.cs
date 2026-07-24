namespace TwitchDownloaderCore.Models
{
    public class DisposableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IDisposable where TValue : IDisposable
    {
        public void Dispose()
        {
            foreach (var disposable in Values)
            {
                disposable?.Dispose();
            }
        }
    }
}