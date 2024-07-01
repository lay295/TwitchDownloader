using System.Collections.Generic;
using System.Text.Json;

namespace TwitchDownloaderCore.Extensions
{
    public static class JsonElementExtensions
    {
        public static List<T> DeserializeFirstAndLastFromList<T>(this JsonElement arrayElement, JsonSerializerOptions options = null)
        {
            // It's not the prettiest, but for arrays with thousands of objects it can save whole seconds and prevent tons of fragmented memory
            var list = new List<T>(2);
            JsonElement lastElement = default;
            foreach (var element in arrayElement.EnumerateArray())
            {
                if (list.Count == 0)
                {
                    list.Add(element.Deserialize<T>(options: options));
                    continue;
                }

                lastElement = element;
            }

            if (lastElement.ValueKind != JsonValueKind.Undefined)
                list.Add(lastElement.Deserialize<T>(options: options));

            return list;
        }
    }
}