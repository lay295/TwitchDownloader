using System.Globalization;
using TwitchDownloaderWPF.Services;
using TwitchDownloaderWPF.Tests.TestHelpers;
using WPFLocalizeExtension.Engine;

namespace TwitchDownloaderWPF.Tests.ServiceTests
{
    // Tests CultureService, which applies the user's GuiCulture setting at startup and whenever the language dropdown changes. It must switch
    // every culture that matters (XAML via WPFLocalizeExtension, code via CurrentUICulture, and new threads via DefaultThreadCurrentUICulture)
    // and must never crash on a bad persisted setting. It mutates process-wide cultures, so it runs in the non-parallel collection and
    // restores everything afterwards.
    [Collection(ProcessStateCollection.NAME)]
    public sealed class CultureServiceTests : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;
        private readonly CultureInfo? _originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        private readonly CultureInfo? _originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        private readonly CultureInfo _originalLocalizeCulture = LocalizeDictionary.Instance.Culture;

        public void Dispose()
        {
            LocalizeDictionary.Instance.Culture = _originalLocalizeCulture;
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = _originalDefaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultUiCulture;
        }

        // Verifies a valid code updates the XAML localizer, the current thread and the default for new threads, then raises CultureChanged once.
        // Why: download tasks run on other threads and format their status/log text with the default UI culture, and pages such as
        // PageChatRender rebuild culture-dependent lists in their CultureChanged handler; missing any of these leaves part of the GUI in the old language.
        [Fact]
        public void SetApplicationCulture_AppliesCultureEverywhere_AndRaisesCultureChanged()
        {
            var service = new CultureService();
            var raised = new List<CultureInfo>();
            service.CultureChanged += (_, culture) => raised.Add(culture);

            service.SetApplicationCulture("de-DE");

            var expected = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(expected, Assert.Single(raised));
            Assert.Equal(expected, LocalizeDictionary.Instance.Culture);
            Assert.Equal(expected, CultureInfo.CurrentUICulture);
            Assert.Equal(expected, CultureInfo.CurrentCulture);
            Assert.Equal(expected, CultureInfo.DefaultThreadCurrentUICulture);
            Assert.Equal(expected, CultureInfo.DefaultThreadCurrentCulture);
        }

        // Verifies an invalid culture name is ignored: no exception, no CultureChanged event, and the current UI culture is unchanged.
        // Why: GuiCulture comes from user.config, which users can hand-edit or which an older version may have written; App.OnStartup calls
        // this before any window exists, so throwing here would crash the app on launch.
        [Fact]
        public void SetApplicationCulture_IgnoresInvalidCultureName()
        {
            var service = new CultureService();
            var raised = false;
            service.CultureChanged += (_, _) => raised = true;
            var before = CultureInfo.CurrentUICulture;

            service.SetApplicationCulture("not a culture!");

            Assert.False(raised);
            Assert.Equal(before, CultureInfo.CurrentUICulture);
        }
    }
}
