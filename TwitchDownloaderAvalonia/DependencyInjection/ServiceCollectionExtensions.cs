using Microsoft.Extensions.DependencyInjection;

namespace TwitchDownloaderAvalonia.DependencyInjection
{
    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTwitchDownloader(this IServiceCollection services, string? logDirectory = null)
        {
            services.AddTwitchDownloaderHttpClients();
            services.AddTwitchDownloaderLogging(logDirectory);
            services.AddTwitchDownloaderServices();
            services.AddTwitchDownloaderViewModels();

            return services;
        }
    }
}
