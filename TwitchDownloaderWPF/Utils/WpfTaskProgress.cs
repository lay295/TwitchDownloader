using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderWPF.Models;

namespace TwitchDownloaderWPF.Utils
{
    internal class WpfTaskProgress : ITaskProgress
    {
        private string _status;
        private bool _statusIsTemplate;

        private int _lastPercent = -1;
        private TimeSpan _lastTime1 = new(-1);
        private TimeSpan _lastTime2 = new(-1);

        private readonly LogLevel _logLevel;

        private readonly Action<int> _handlePercent;
        private readonly Action<string> _handleStatus;
        private readonly Action<string> _handleLog;
        private readonly Action<string> _handleFfmpegLog;

        public WpfTaskProgress(Action<int> handlePercent)
        {
            _handlePercent = handlePercent;
            _handleStatus = null;
            _handleLog = null;
            _handleFfmpegLog = null;

            _logLevel = LogLevel.None;
        }

        public WpfTaskProgress(LogLevel logLevel, Action<int> handlePercent, Action<string> handleStatus, Action<string> handleLog, Action<string> handleFfmpegLog = null)
        {
            _handlePercent = handlePercent;
            _handleStatus = handleStatus;
            _handleLog = handleLog;
            _handleFfmpegLog = handleFfmpegLog;

            _logLevel = logLevel;
            if (handleFfmpegLog is not null)
            {
                // TODO: Make this user configurable
                _logLevel |= LogLevel.Ffmpeg;
            }
        }

        public void SetStatus(string status)
        {
            lock (this)
            {
                _status = status;
                _statusIsTemplate = false;

                _handleStatus?.Invoke(status);
            }
        }

        public void SetTemplateStatus([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string statusTemplate, int initialPercent)
        {
            lock (this)
            {
                _status = statusTemplate;
                _statusIsTemplate = true;

                _lastPercent = -1; // Ensure that the progress report runs
                ReportProgress(initialPercent);
            }
        }

        public void SetTemplateStatus([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string statusTemplate, int initialPercent, TimeSpan initialTime1, TimeSpan initialTime2)
        {
            lock (this)
            {
                _status = statusTemplate;
                _statusIsTemplate = true;

                _lastPercent = -1; // Ensure that the progress report runs
                ReportProgress(initialPercent, initialTime1, initialTime2);
            }
        }

        public void ReportProgress(int percent)
        {
            lock (this)
            {
                if (_lastPercent == percent)
                {
                    return;
                }

                _handlePercent(percent);
                _lastPercent = percent;

                if (!_statusIsTemplate)
                {
                    return;
                }

                var status = string.Format(_status, percent);
                _handleStatus?.Invoke(status);
            }
        }

        public void ReportProgress(int percent, TimeSpan time1, TimeSpan time2)
        {
            lock (this)
            {
                if (_lastPercent == percent && _lastTime1 == time1 && _lastTime2 == time2)
                {
                    return;
                }

                _handlePercent(percent);
                _lastPercent = percent;
                _lastTime1 = time1;
                _lastTime2 = time2;

                if (!_statusIsTemplate)
                {
                    return;
                }

                var status = string.Format(_status, percent, time1, time2);
                _handleStatus?.Invoke(status);
            }
        }

        public void LogVerbose(string logMessage)
        {
            if ((_logLevel & LogLevel.Verbose) == 0) return;

            _handleLog?.Invoke(logMessage);
        }

        public void LogVerbose(DefaultInterpolatedStringHandler logMessage)
        {
            if ((_logLevel & LogLevel.Verbose) == 0) return;

            _handleLog?.Invoke(logMessage.ToStringAndClear());
        }

        public void LogInfo(string logMessage)
        {
            if ((_logLevel & LogLevel.Info) == 0) return;

            _handleLog.Invoke(logMessage);
        }

        public void LogInfo(DefaultInterpolatedStringHandler logMessage)
        {
            if ((_logLevel & LogLevel.Info) == 0) return;

            _handleLog.Invoke(logMessage.ToStringAndClear());
        }

        public void LogWarning(string logMessage)
        {
            if ((_logLevel & LogLevel.Warning) == 0) return;

            _handleLog?.Invoke(logMessage);
        }

        public void LogWarning(DefaultInterpolatedStringHandler logMessage)
        {
            if ((_logLevel & LogLevel.Warning) == 0) return;

            _handleLog?.Invoke(logMessage.ToStringAndClear());
        }

        public void LogError(string logMessage)
        {
            if ((_logLevel & LogLevel.Error) == 0) return;

            _handleLog?.Invoke(Translations.Strings.ErrorLog + logMessage);
        }

        public void LogError(DefaultInterpolatedStringHandler logMessage)
        {
            if ((_logLevel & LogLevel.Error) == 0) return;

            _handleLog?.Invoke(Translations.Strings.ErrorLog + logMessage.ToStringAndClear());
        }

        public void LogFfmpeg(string logMessage)
        {
            if ((_logLevel & LogLevel.Ffmpeg) == 0) return;

            _handleFfmpegLog?.Invoke(logMessage);
        }
    }
}