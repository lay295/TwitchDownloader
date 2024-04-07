using System.Runtime.CompilerServices;

namespace TwitchDownloaderCore.Interfaces
{
    public interface ITaskLogger
    {
        void LogVerbose(string logMessage);
        void LogVerbose(DefaultInterpolatedStringHandler logMessage);
        void LogInfo(string logMessage);
        void LogInfo(DefaultInterpolatedStringHandler logMessage);
        void LogWarning(string logMessage);
        void LogWarning(DefaultInterpolatedStringHandler logMessage);
        void LogError(string logMessage);
        void LogError(DefaultInterpolatedStringHandler logMessage);
        void LogFfmpeg(string logMessage);
    }
}