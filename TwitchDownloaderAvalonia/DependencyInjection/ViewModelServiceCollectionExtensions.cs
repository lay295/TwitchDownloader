using Microsoft.Extensions.DependencyInjection;
using TwitchDownloaderAvalonia.ViewModels;

namespace TwitchDownloaderAvalonia.DependencyInjection
{
    internal static class ViewModelServiceCollectionExtensions
    {
        public static IServiceCollection AddTwitchDownloaderViewModels(this IServiceCollection services)
        {
            services.AddSingleton<MainWindowViewModel>();
            return services;
        }
    }
}
