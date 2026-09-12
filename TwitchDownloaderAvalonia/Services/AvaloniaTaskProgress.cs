using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class AvaloniaTaskProgress(
        LogLevel logLevel,
        Action<int> handlePercent,
        Action<string> handleStatus,
        Action<string>? handleLog = null)
        : ITaskProgress
    {
        private string _status = string.Empty;
        private bool _statusIsTemplate;
        private int _lastPercent = -1;
        private TimeSpan _lastTime1 = new(-1);
        private TimeSpan _lastTime2 = new(-1);
        private readonly Lock _writeLock = new();

        public void SetStatus(string status)
        {
            lock (_writeLock)
            {
                _status = status;
                _statusIsTemplate = false;
                Post(() => handleStatus(status));
            }
        }

        public void SetTemplateStatus([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string statusTemplate, int initialPercent)
        {
            lock (_writeLock)
            {
                _status = statusTemplate;
                _statusIsTemplate = true;
                _lastPercent = -1;
                ReportProgress(initialPercent);
            }
        }

        public void SetTemplateStatus([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string statusTemplate, int initialPercent, TimeSpan initialTime1, TimeSpan initialTime2)
        {
            lock (_writeLock)
            {
                _status = statusTemplate;
                _statusIsTemplate = true;
                _lastPercent = -1;
                ReportProgress(initialPercent, initialTime1, initialTime2);
            }
        }

        public void ReportProgress(int percent)
        {
            lock (_writeLock)
            {
                if (_lastPercent == percent)
                {
                    return;
                }

                _lastPercent = percent;
                Post(() => handlePercent(percent));

                if (_statusIsTemplate)
                {
                    var status = string.Format(_status, percent);
                    Post(() => handleStatus(status));
                }
            }
        }

        public void ReportProgress(int percent, TimeSpan time1, TimeSpan time2)
        {
            lock (_writeLock)
            {
                if (_lastPercent == percent && _lastTime1 == time1 && _lastTime2 == time2)
                {
                    return;
                }

                _lastPercent = percent;
                _lastTime1 = time1;
                _lastTime2 = time2;
                Post(() => handlePercent(percent));

                if (_statusIsTemplate)
                {
                    var status = string.Format(_status, percent, time1, time2);
                    Post(() => handleStatus(status));
                }
            }
        }

        public void LogVerbose(string logMessage)
        {
            if ((logLevel & LogLevel.Verbose) == 0) return;
            PostLog(logMessage);
        }

        public void LogVerbose(DefaultInterpolatedStringHandler logMessage)
        {
            if ((logLevel & LogLevel.Verbose) == 0) return;
            PostLog(logMessage.ToStringAndClear());
        }

        public void LogInfo(string logMessage)
        {
            if ((logLevel & LogLevel.Info) == 0) return;
            PostLog(logMessage);
        }

        public void LogInfo(DefaultInterpolatedStringHandler logMessage)
        {
            if ((logLevel & LogLevel.Info) == 0) return;
            PostLog(logMessage.ToStringAndClear());
        }

        public void LogWarning(string logMessage)
        {
            if ((logLevel & LogLevel.Warning) == 0) return;
            PostLog(logMessage);
        }

        public void LogWarning(DefaultInterpolatedStringHandler logMessage)
        {
            if ((logLevel & LogLevel.Warning) == 0) return;
            PostLog(logMessage.ToStringAndClear());
        }

        public void LogError(string logMessage)
        {
            if ((logLevel & LogLevel.Error) == 0) return;
            PostLog(Loc.Error(logMessage));
        }

        public void LogError(DefaultInterpolatedStringHandler logMessage)
        {
            if ((logLevel & LogLevel.Error) == 0) return;
            PostLog(Loc.Error(logMessage.ToStringAndClear()));
        }

        public void LogFfmpeg(string logMessage)
        {
            if ((logLevel & LogLevel.Ffmpeg) == 0) return;
            PostLog(logMessage);
        }

        private void PostLog(string message)
        {
            if (handleLog is null)
            {
                return;
            }

            Post(() => handleLog(message));
        }

        private static void Post(Action action)
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
