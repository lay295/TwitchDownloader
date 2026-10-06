using Microsoft.Extensions.DependencyInjection;
using TwitchDownloaderAvalonia.Update.Services;

namespace TwitchDownloaderAvalonia.DependencyInjection
{
    internal static class HttpClientNames
    {
        public const string Thumbnails = "Thumbnails";
        public const string Updates = "Updates";
    }

    internal static class HttpClientServiceCollectionExtensions
    {
        public static IServiceCollection AddTwitchDownloaderHttpClients(this IServiceCollection services)
        {
            services.AddHttpClient(HttpClientNames.Thumbnails, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });

            services.AddHttpClient<UpdateCheckService>(HttpClientNames.Updates, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TwitchDownloader");
            });

            return services;
        }
    }
}
