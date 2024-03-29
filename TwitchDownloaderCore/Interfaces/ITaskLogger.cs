namespace TwitchDownloaderCore.Interfaces
{
    public interface ITaskLogger
    {
        // TODO: Add DefaultInterpolatedStringHandler overloads once log levels are implemented for zero-alloc logging
        void LogVerbose(string logMessage);
        void LogInfo(string logMessage);
        void LogWarning(string logMessage);
        void LogError(string logMessage);
        void LogFfmpeg(string logMessage);
    }
}