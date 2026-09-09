namespace TwitchDownloaderAvalonia.Services
{
    public static class Loc
    {
        public static string Get(string key)
        {
            return LocalizationService.Current.Get(key);
        }

        public static string Get(string key, params object[] args)
        {
            return LocalizationService.Current.Get(key, args);
        }

        public static string Error(string message)
        {
            return Get("common.log_error_prefix") + message;
        }
    }
}
