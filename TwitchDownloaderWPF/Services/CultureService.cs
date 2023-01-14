using System.Globalization;
using System.Threading;
using WPFLocalizeExtension.Engine;

namespace TwitchDownloaderWPF.Services
{
    public static class CultureService
    {
        public static void SetApplicationCulture(string culture)
        {
            try
            {
                CultureInfo cultureInfo = CultureInfo.GetCultureInfo(culture);
                LocalizeDictionary.Instance.SetCurrentThreadCulture = true;
                LocalizeDictionary.Instance.Culture = cultureInfo;
                CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
                Thread.CurrentThread.CurrentCulture = cultureInfo;
                Thread.CurrentThread.CurrentUICulture = cultureInfo;
            }
            catch (CultureNotFoundException) { }
        }
    }
}
