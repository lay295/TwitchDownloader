namespace TwitchDownloaderCore.Extensions
{
    public static class DictionaryExtensions
    {
        public static void AddRange<TKey, TValue, T>(this IDictionary<TKey, TValue> dictionary, IEnumerable<T> items, Func<T, TKey> keySelector, Func<T, TValue> valueSelector)
        {
            foreach (var item in items)
            {
                dictionary.Add(keySelector(item), valueSelector(item));
            }
        }

        public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, IReadOnlyDictionary<TKey, TValue> items)
        {
            foreach (var (key, value) in items)
            {
                dictionary.Add(key, value);
            }
        }
    }
}