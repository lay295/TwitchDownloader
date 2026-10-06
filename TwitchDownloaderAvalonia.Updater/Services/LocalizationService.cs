using System.ComponentModel;
using Avalonia.Platform;
using Tomlyn;
using Tomlyn.Model;

namespace TwitchDownloaderAvalonia.Updater.Services
{
    public sealed class LocalizationService : INotifyPropertyChanged
    {
        public const string DEFAULT_CULTURE = "en-US";
        private const string ASSET_ASSEMBLY = "TwitchDownloaderAvalonia.Updater";

        private readonly Dictionary<string, string> _fallback;
        private Dictionary<string, string> _current;

        public LocalizationService()
        {
            _fallback = Load(DEFAULT_CULTURE) ?? new Dictionary<string, string>(StringComparer.Ordinal);
            _current = _fallback;
            Culture = DEFAULT_CULTURE;
        }

        public string Culture { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? CultureChanged;

        public string Get(string key)
        {
            if (_current.TryGetValue(key, out var value) && value.Length > 0)
                return value;
            if (!ReferenceEquals(_current, _fallback) && _fallback.TryGetValue(key, out value) && value.Length > 0)
                return value;
            return key;
        }

        public string Get(string key, params object[] args)
        {
            var template = Get(key);
            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, args);
            }
            catch (FormatException)
            {
                return template;
            }
        }

        public void SetCulture(string? culture)
        {
            var resolved = ResolveCulture(culture);
            _current = resolved != DEFAULT_CULTURE ? Load(resolved) ?? _fallback : _fallback;
            Culture = resolved;
            try
            {
                var info = CultureInfo.GetCultureInfo(resolved);
                CultureInfo.DefaultThreadCurrentCulture = info;
                CultureInfo.DefaultThreadCurrentUICulture = info;
            }
            catch (CultureNotFoundException)
            {
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }

        private static string ResolveCulture(string? culture)
        {
            if (string.IsNullOrWhiteSpace(culture))
                return DEFAULT_CULTURE;

            string[] codes =
            [
                "en-US", "de-DE", "es-ES", "fr-FR", "it-IT", "ja-JP", "pl-PL",
                "pt-BR", "ru-RU", "tr-TR", "uk-UA", "zh-CN", "zh-TW",
            ];

            foreach (var code in codes)
            {
                if (code.Equals(culture, StringComparison.OrdinalIgnoreCase))
                    return code;
            }

            var dash = culture.IndexOf('-');
            var language = dash > 0 ? culture[..dash] : culture;
            if (language.Equals("zh", StringComparison.OrdinalIgnoreCase))
            {
                var lower = culture.ToLowerInvariant();
                if (lower.Contains("hant") || lower.StartsWith("zh-tw") || lower.StartsWith("zh-hk") || lower.StartsWith("zh-mo"))
                    return "zh-TW";
                return "zh-CN";
            }

            foreach (var code in codes)
            {
                if (code.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase))
                    return code;
            }

            return DEFAULT_CULTURE;
        }

        private static Dictionary<string, string>? Load(string culture)
        {
            try
            {
                var uri = new Uri($"avares://{ASSET_ASSEMBLY}/Lang/{culture}.toml");
                if (!AssetLoader.Exists(uri))
                    return null;

                using var stream = AssetLoader.Open(uri);
                using var reader = new StreamReader(stream);
                var table = TomlSerializer.Deserialize<TomlTable>(reader.ReadToEnd());
                if (table is null)
                    return null;

                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                Flatten(table, string.Empty, dict);
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
