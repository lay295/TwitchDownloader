namespace TwitchDownloaderAvalonia.Converters
{
    internal static class Boxed
    {
        public static readonly object True = true;
        public static readonly object False = false;

        public static object From(bool value) => value ? True : False;
    }
}
