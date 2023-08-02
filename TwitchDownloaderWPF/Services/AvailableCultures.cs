namespace TwitchDownloaderWPF.Services
{
    public record struct Culture(string Code, string NativeName);

    public static class AvailableCultures
    {
        // Notes for translators:
        //
        // Please create a new record for your culture and place it in the 'All' array in English alphabetical order.
        // The order of the 'All' array is the order that they will appear in the language dropdown menu.
        //
        // If you do not know the code for your culture, you can find it using the Visual Studio ResX Resource Manager extension
        // https://marketplace.visualstudio.com/items?itemName=TomEnglert.ResXManager
        // For JetBrains Rider users, the localization manager's "Add Culture" dialogue contains culture codes
        // Or alternatively it can probably be found it here:
        // http://www.codedigest.com/CodeDigest/207-Get-All-Language-Country-Code-List-for-all-Culture-in-C---ASP-Net.aspx
        // Or it can be found by combining the ISO 639-3 language name with the ISO 3166-1 alpha-2 country name.
        // ISO 639-3: https://en.wikipedia.org/wiki/IETF_language_tag#List_of_common_primary_language_subtags
        // ISO 3166-1 alpha-2: https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2#Officially_assigned_code_elements

        public static readonly Culture English;
        public static readonly Culture Spanish;
        public static readonly Culture French;
        public static readonly Culture Polish;
        public static readonly Culture Russian;
        public static readonly Culture Turkish;
        public static readonly Culture SimplifiedChinese;

        public static readonly Culture[] All;

        static AvailableCultures()
        {
            All = new[]
            {
                English = new Culture("en-US", "English"),
                Spanish = new Culture("es-ES", "Spanish"),
                French = new Culture("fr-FR", "Français"),
                Polish = new Culture("pl-PL", "Polski"),
                Russian = new Culture("ru-RU", "Русский"),
                Turkish = new Culture("tr-TR", "Türkçe"),
                SimplifiedChinese = new Culture("zh-CN", "简体中文")
            };
        }
    }
}
