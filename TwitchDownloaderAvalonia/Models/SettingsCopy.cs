using System.Reflection;

namespace TwitchDownloaderAvalonia.Models
{
    internal static class SettingsCopy
    {
        public static void Copy<T>(T source, T dest) where T : class
        {
            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                var value = prop.GetValue(source);
                if (value is List<string> list)
                    value = new List<string>(list);
                else if (value is List<NamedRenderPreset> presets)
                    value = presets.Select(preset => preset.Clone()).ToList();

                prop.SetValue(dest, value);
            }
        }

        public static T Clone<T>(T source) where T : class, new()
        {
            var dest = new T();
            Copy(source, dest);
            return dest;
        }
    }
}
