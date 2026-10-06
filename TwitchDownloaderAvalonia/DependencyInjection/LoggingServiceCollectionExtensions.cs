using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace TwitchDownloaderAvalonia.DependencyInjection
{
    internal static class LoggingServiceCollectionExtensions
    {
        public static IServiceCollection AddTwitchDownloaderLogging(this IServiceCollection services, string? logDirectory = null)
        {
            var directory = logDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TwitchDownloader", "logs");
            Directory.CreateDirectory(directory);

            var fileLogger = new RollingFileLoggerProvider(Path.Combine(directory, "avalonia.log"));
            services.AddSingleton(fileLogger);
            services.AddSingleton<ILoggerProvider>(fileLogger);

            services.AddLogging(builder =>
            {
#if DEBUG
                builder.SetMinimumLevel(MsLogLevel.Debug);
#else
                builder.SetMinimumLevel(MsLogLevel.Information);
#endif
                builder.AddFilter("Microsoft.Extensions.Http", MsLogLevel.Warning);
                builder.AddFilter("System.Net.Http.HttpClient", MsLogLevel.Warning);
                builder.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
                builder.AddDebug();
            });
            return services;
        }
    }
}
