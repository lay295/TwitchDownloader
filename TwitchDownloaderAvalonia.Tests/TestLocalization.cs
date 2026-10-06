using Microsoft.Extensions.DependencyInjection;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests
{
    internal static class TestLocalization
    {
        private static readonly Lock Gate = new();

        public static LocalizationService Instance
        {
            get
            {
                if (field is not null)
                    return field;

                lock (Gate)
                {
                    return field ??= new ServiceCollection()
                        .AddLogging()
                        .AddSingleton<LocalizationService>()
                        .BuildServiceProvider()
                        .GetRequiredService<LocalizationService>();
                }
            }
        }
    }
}
