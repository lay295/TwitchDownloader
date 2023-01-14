namespace TwitchDownloaderWPF.Services
{
    public static class AvailableCultures
    {
        // I really hate doing this but I can't figure out manually reading the resource files
        // when they are merged to the main assembly, and they can't be satellites because of
        // <PublishSingleFile>True</PublishSingleFile>
        public enum Culture
        {
            English
        }

        private const string ENGLISH_NAME = "en";
        private const string ENGLISH_NATIVE_NAME = "English";

        private const string INVARIANT_NAME = "";
        private const string INVARIANT_NATIVE_NAME = "Invariant";

        public static string ToName(this Culture translation)
        {
            return translation switch
            {
                Culture.English => ENGLISH_NAME,
                _ => INVARIANT_NAME
            };
        }

        public static string ToNativeName(this Culture translation)
        {
            return translation switch
            {
                Culture.English => ENGLISH_NATIVE_NAME,
                _ => INVARIANT_NATIVE_NAME
            };
        }
    }
}
