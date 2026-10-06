using Microsoft.Extensions.DependencyInjection;
using TwitchDownloaderAvalonia.Update.Services;

namespace TwitchDownloaderAvalonia.DependencyInjection
{
    internal static class ApplicationServiceCollectionExtensions
    {
        public static IServiceCollection AddTwitchDownloaderServices(this IServiceCollection services)
        {
            services.AddSingleton<SettingsService>();
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton<AppStatus>();
            services.AddSingleton<FfmpegService>();
            services.AddSingleton<FileDialogService>();
            services.AddSingleton<IFileDialogService>(sp => sp.GetRequiredService<FileDialogService>());
            services.AddSingleton<DialogService>();
            services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
            services.AddSingleton<FileCollisionService>();
            services.AddSingleton<AbandonedVideoCacheService>();
            services.AddSingleton<ThumbnailService>();
            services.AddSingleton<QueueService>();
            services.AddSingleton<UpdatePreferencesStore>();
            services.AddSingleton<UpdateLauncher>();
            services.AddSingleton<QueueEnqueueService>();

            return services;
        }
    }
}
