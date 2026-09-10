using Avalonia.Platform;
using Tomlyn;
using Tomlyn.Model;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class LocalizationService : INotifyPropertyChanged
    {
        public const string DEFAULT_CULTURE = "en-US";

        public static LocalizationService Current { get; } = new();

        private readonly Dictionary<string, string> _fallback;
        private Dictionary<string, string> _current;

        private LocalizationService()
        {
            _fallback = Load(DEFAULT_CULTURE) ?? new Dictionary<string, string>(StringComparer.Ordinal);
            _current = _fallback;
            Culture = DEFAULT_CULTURE;
        }

        public string Culture { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? CultureChanged;

        public string this[string key] => Get(key);

        public string Get(string key)
        {
            EnsureFallback();
            if (_current.TryGetValue(key, out var value) && value.Length > 0)
                return value;

            if (!ReferenceEquals(_current, _fallback)
                && _fallback.TryGetValue(key, out value)
                && value.Length > 0)
            {
#if DEBUG
                Debug.WriteLine($"[i18n] missing {Culture}: {key}");
#endif
                return value;
            }

#if DEBUG
            Debug.WriteLine($"[i18n] missing key: {key}");
#endif
            return key;
        }

        public string Get(string key, params object[] args)
        {
            return Format(Get(key), key, args);
        }

        internal static string Format(string template, string key, params object[] args)
        {
            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, args);
            }
            catch (FormatException ex)
            {
                Debug.WriteLine($"[i18n] format {key}: {ex.Message}");
                return template;
            }
        }

        public void SetCulture(string? culture)
        {
            EnsureFallback();
            var resolved = AvailableCultures.Resolve(culture);
            var loaded = resolved != DEFAULT_CULTURE
                ? Load(resolved) ?? _fallback
                : _fallback;

            _current = loaded;
            Culture = resolved;

            try
            {
                var info = CultureInfo.GetCultureInfo(resolved);
                CultureInfo.DefaultThreadCurrentCulture = info;
                CultureInfo.DefaultThreadCurrentUICulture = info;
                CultureInfo.CurrentCulture = info;
                CultureInfo.CurrentUICulture = info;
                Thread.CurrentThread.CurrentCulture = info;
                Thread.CurrentThread.CurrentUICulture = info;
            }
            catch (CultureNotFoundException)
            {
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EnsureFallback()
        {
            if (_fallback.Count > 0)
                return;

            var loaded = Load(DEFAULT_CULTURE);
            if (loaded is null)
                return;

            foreach (var pair in loaded)
                _fallback[pair.Key] = pair.Value;
        }

        private static Dictionary<string, string>? Load(string culture)
        {
            try
            {
                var uri = new Uri($"avares://TwitchDownloaderAvalonia/Lang/{culture}.toml");
                if (!AssetLoader.Exists(uri))
                    return null;

                using var stream = AssetLoader.Open(uri);
                using var reader = new StreamReader(stream);
                var table = TomlSerializer.Deserialize<TomlTable>(reader.ReadToEnd());
                if (table is null)
                    return null;

                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                Flatten(table, string.Empty, dict);
                dict.Remove("meta.code");
                dict.Remove("meta.name");
                return dict;
            }
            catch
            {
                return null;
            }
        }

        private static void Flatten(TomlTable table, string prefix, Dictionary<string, string> dest)
        {
            foreach (var pair in table)
            {
                var key = string.IsNullOrEmpty(prefix) ? pair.Key : prefix + "." + pair.Key;
                switch (pair.Value)
                {
                    case TomlTable nested:
                        Flatten(nested, key, dest);
                        break;
                    case string text:
                        dest[key] = text;
                        break;
                    default:
                        dest[key] = pair.Value?.ToString() ?? string.Empty;
                        break;
                }
            }
        }
    }
}
