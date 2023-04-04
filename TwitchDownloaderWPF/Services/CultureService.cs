using System;
using System.Globalization;
using System.Threading;
using WPFLocalizeExtension.Engine;

namespace TwitchDownloaderWPF.Services
{
    public class CultureService
    {
        public event EventHandler<CultureInfo> CultureChanged;

        public void SetApplicationCulture(string culture)
        {
            try
            {
                var newCulture = CultureInfo.GetCultureInfo(culture);
                LocalizeDictionary.Instance.SetCurrentThreadCulture = true;
                LocalizeDictionary.Instance.Culture = newCulture;
                CultureInfo.DefaultThreadCurrentCulture = newCulture;
                CultureInfo.DefaultThreadCurrentUICulture = newCulture;
                Thread.CurrentThread.CurrentCulture = newCulture;
                Thread.CurrentThread.CurrentUICulture = newCulture;

                CultureChanged?.Invoke(this, newCulture);
            }
            catch (CultureNotFoundException) { }
        }
    }
}
