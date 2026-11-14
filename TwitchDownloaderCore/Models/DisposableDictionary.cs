namespace TwitchDownloaderCore.Models
{
    public class DisposableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IDisposable where TValue : IDisposable
    {
        public DisposableDictionary() { }

        public DisposableDictionary(IEqualityComparer<TKey> comparer) : base(comparer) { }

        public void Dispose()
        {
            foreach (var disposable in Values)
            {
                disposable?.Dispose();
            }
        }
    }
}