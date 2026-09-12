namespace TwitchDownloaderAvalonia.Services
{
    public readonly record struct CultureOption(string Code, string NativeName)
    {
        public override string ToString() => NativeName;
    }

    public static class AvailableCultures
    {
        public static readonly CultureOption English = new("en-US", "English");
        public static readonly CultureOption German = new("de-DE", "Deutsch");
        public static readonly CultureOption Spanish = new("es-ES", "Español");
        public static readonly CultureOption French = new("fr-FR", "Français");
        public static readonly CultureOption Italian = new("it-IT", "Italiano");
        public static readonly CultureOption Japanese = new("ja-JP", "日本語");
        public static readonly CultureOption Polish = new("pl-PL", "Polski");
        public static readonly CultureOption PortugueseBrazil = new("pt-BR", "Português (Brasil)");
        public static readonly CultureOption Russian = new("ru-RU", "Русский");
        public static readonly CultureOption Turkish = new("tr-TR", "Türkçe");
        public static readonly CultureOption Ukrainian = new("uk-UA", "Українська");
        public static readonly CultureOption SimplifiedChinese = new("zh-CN", "简体中文");
        public static readonly CultureOption TraditionalChinese = new("zh-TW", "繁體中文");

        public static IReadOnlyList<CultureOption> All { get; } =
        [
            English,
            German,
            Spanish,
            French,
            Italian,
            Japanese,
            Polish,
            PortugueseBrazil,
            Russian,
            Turkish,
            Ukrainian,
            SimplifiedChinese,
            TraditionalChinese,
        ];

        public static string Resolve(string? culture)
        {
            if (string.IsNullOrWhiteSpace(culture))
                return LocalizationService.DEFAULT_CULTURE;

            foreach (var option in All)
            {
                if (option.Code.Equals(culture, StringComparison.OrdinalIgnoreCase))
                    return option.Code;
            }

            if (TryResolveChinese(culture, out var chinese))
                return chinese;

            var dash = culture.IndexOf('-');
            var language = dash > 0 ? culture[..dash] : culture;
            if (language.Equals("zh", StringComparison.OrdinalIgnoreCase))
                return SimplifiedChinese.Code;

            foreach (var option in All)
            {
                if (option.Code.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase))
                    return option.Code;
            }

            return LocalizationService.DEFAULT_CULTURE;
        }

        public static CultureOption FromCode(string? culture)
        {
            var resolved = Resolve(culture);
            foreach (var option in All)
            {
                if (option.Code == resolved)
                    return option;
            }

            return English;
        }

        private static bool TryResolveChinese(string culture, out string code)
        {
            code = SimplifiedChinese.Code;
            CultureInfo? info = null;
            try
            {
                info = CultureInfo.GetCultureInfo(culture);
            }
            catch (CultureNotFoundException)
            {
            }

            var name = (info?.Name ?? culture).ToLowerInvariant();
            if (!name.StartsWith("zh", StringComparison.Ordinal))
                return false;

            for (var current = info; current is { Name.Length: > 0 }; current = current.Parent)
            {
                if (current.Name.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase))
                {
                    code = TraditionalChinese.Code;
                    return true;
                }

                if (current.Name.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase))
                {
                    code = SimplifiedChinese.Code;
                    return true;
                }
            }

            if (name.Contains("hant", StringComparison.Ordinal)
                || name.StartsWith("zh-tw", StringComparison.Ordinal)
                || name.StartsWith("zh-hk", StringComparison.Ordinal)
                || name.StartsWith("zh-mo", StringComparison.Ordinal))
            {
                code = TraditionalChinese.Code;
                return true;
            }

            code = SimplifiedChinese.Code;
            return true;
        }
    }
}
