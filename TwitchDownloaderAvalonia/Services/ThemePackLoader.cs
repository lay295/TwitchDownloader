using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media;
using Avalonia.Styling;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed record ThemePack(
        string Name,
        string? Author,
        bool IsDark,
        IReadOnlyDictionary<string, Color> Colors)
    {
        public ThemeVariant ToThemeVariant() => new(Name, IsDark ? ThemeVariant.Dark : ThemeVariant.Light);

        public ResourceDictionary ToResourceDictionary()
        {
            var dictionary = new ResourceDictionary();
            foreach (var key in ThemePackKeys.BrushKeys)
            {
                var color = Colors[key];
                dictionary[key] = new SolidColorBrush(color);
                if (ThemePackKeys.ColorKeyForBrush.TryGetValue(key, out var colorKey))
                    dictionary[colorKey] = color;
            }

            return dictionary;
        }
    }

    public static class ThemePackLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static readonly Dictionary<string, string> CanonicalBrushKeys =
            ThemePackKeys.BrushKeys.ToDictionary(key => key, StringComparer.OrdinalIgnoreCase);

        public static bool TryParse(string json, string name, out ThemePack? pack)
        {
            pack = null;
            if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(name))
                return false;

            if (!TryReadJson(json, out var model) || model.IsDark is not { } isDark)
                return false;

            var parsed = new Dictionary<string, Color>(StringComparer.Ordinal);
            if (model.Colors is not null)
            {
                foreach (var (key, value) in model.Colors)
                {
                    if (!CanonicalBrushKeys.TryGetValue(key, out var canonical))
                        continue;
                    if (!TryParseHex(value, out var color))
                        continue;

                    parsed[canonical] = color;
                }
            }

            var fallback = isDark ? ThemePackPalettes.Dark : ThemePackPalettes.Light;
            var colors = new Dictionary<string, Color>(ThemePackKeys.BrushKeys.Length, StringComparer.Ordinal);
            foreach (var key in ThemePackKeys.BrushKeys)
            {
                if (parsed.TryGetValue(key, out var color))
                    colors[key] = color;
                else
                    colors[key] = ParseRequiredHex(fallback[key]);
            }

            pack = new ThemePack(name, string.IsNullOrWhiteSpace(model.Author) ? null : model.Author.Trim(), isDark, colors);
            return true;
        }

        public static bool TryLoadFile(string path, string name, out ThemePack? pack)
        {
            pack = null;
            try
            {
                var json = File.ReadAllText(path);
                return TryParse(json, name, out pack);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                return false;
            }
        }

        public static bool TryPeekIsDark(string json, out bool isDark)
        {
            isDark = false;
            if (!TryReadJson(json, out var model) || model.IsDark is not { } value)
                return false;

            isDark = value;
            return true;
        }

        internal static string SerializeSample(ThemePackSample sample)
        {
            var ordered = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in ThemePackKeys.BrushKeys)
                ordered[key] = sample.Colors[key];

            return JsonSerializer.Serialize(new ThemePackJson
            {
                Author = ThemePackPalettes.SAMPLE_AUTHOR,
                IsDark = sample.IsDark,
                Colors = ordered,
            }, JsonOptions);
        }

        private static bool TryParseHex(string? value, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var text = value.Trim();
            if (text[0] != '#' || text.Length is not (7 or 9))
                return false;

            return Color.TryParse(text, out color);
        }

        private static bool TryReadJson(string json, [NotNullWhen(true)] out ThemePackJson? model)
        {
            model = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                model = JsonSerializer.Deserialize<ThemePackJson>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return false;
            }

            return model is not null;
        }

        private static Color ParseRequiredHex(string hex)
        {
            if (!TryParseHex(hex, out var color))
                throw new InvalidOperationException($"Built-in theme color '{hex}' is invalid.");

            return color;
        }

        private sealed class ThemePackJson
        {
            public string? Author { get; init; }
            public bool? IsDark { get; init; }
            public Dictionary<string, string>? Colors { get; init; }
        }
    }
}
