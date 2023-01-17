﻿namespace TwitchDownloaderWPF.Services
{
    public static class AvailableCultures
    {
        // I really hate doing this but I can't figure out manually reading the resource files when they are merged to
        // the main assembly, and they can't be satellites because of: <PublishSingleFile>True</PublishSingleFile>

        // Notes for translators:
        //
        // Please place your culture in the enum in English aplhabetical order by the 'name'
        //
        // If you do not know the 'name' for your culture, you can probably find it here:
        // http://www.codedigest.com/CodeDigest/207-Get-All-Language-Country-Code-List-for-all-Culture-in-C---ASP-Net.aspx
        // Or by combining the ISO 639-3 language name with the ISO 3166-1 alpha-2 (ISO 3166-3 if none) country name
        // ISO 639-3: https://en.wikipedia.org/wiki/IETF_language_tag#List_of_common_primary_language_subtags
        // ISO 3166-1 alpha-2: https://en.wikipedia.org/wiki/ISO_3166-1_alpha-2#Officially_assigned_code_elements
        public enum Culture
        {
            English,
            French
        }

        private const string ENGLISH_NAME = "en-US";
        private const string ENGLISH_NATIVE_NAME = "English";
        private const string FRENCH_NAME = "fr-FR";
        private const string FRENCH_NATIVE_NAME = "Français";
        private const string INVARIANT_NAME = "";
        private const string INVARIANT_NATIVE_NAME = "Invariant";

        public static string ToName(this Culture translation)
        {
            return translation switch
            {
                Culture.English => ENGLISH_NAME,
                Culture.French => FRENCH_NAME,
                _ => INVARIANT_NAME
            };
        }

        public static string ToNativeName(this Culture translation)
        {
            return translation switch
            {
                Culture.English => ENGLISH_NATIVE_NAME,
                Culture.French => FRENCH_NATIVE_NAME,
                _ => INVARIANT_NATIVE_NAME
            };
        }
    }
}
