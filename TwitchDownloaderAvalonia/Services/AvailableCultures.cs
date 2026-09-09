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

            var dash = culture.IndexOf('-');
            var language = dash > 0 ? culture[..dash] : culture;
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
    }
}
