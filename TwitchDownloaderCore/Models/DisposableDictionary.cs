namespace TwitchDownloaderCore.Models
{
    public class DisposableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IDisposable where TValue : IDisposable
    {
        public void AddRange<T>(IEnumerable<T> items, Func<T, TKey> keySelector, Func<T, TValue> valueSelector)
        {
            foreach (var item in items)
            {
                Add(keySelector(item), valueSelector(item));
            }
        }

        public void AddRange(IReadOnlyDictionary<TKey, TValue> items)
        {
            foreach (var (key, value) in items)
            {
                Add(key, value);
            }
        }

        public void Dispose()
        {
            foreach (var disposable in Values)
            {
                disposable?.Dispose();
            }
        }
    }
}