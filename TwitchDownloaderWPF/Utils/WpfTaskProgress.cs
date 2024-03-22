using System;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderWPF.Utils
{
    internal class WpfTaskProgress : ITaskProgress
    {
        private string _status;
        private bool _statusIsTemplate;

        private int _lastPercent = -1;

        private readonly Action<int> _handlePercent;
        private readonly Action<string> _handleStatus;
        private readonly Action<string> _handleLog;
        private readonly Action<string> _handleFfmpegLog;

        public WpfTaskProgress(Action<int> handlePercent)
        {
            _handlePercent = handlePercent;
            _handleStatus = static _ => { };
            _handleLog = static _ => { };
            _handleFfmpegLog = static _ => { };
        }

        public WpfTaskProgress(Action<int> handlePercent, Action<string> handleStatus, Action<string> handleLog, Action<string> handleFfmpegLog = null)
        {
            _handlePercent = handlePercent;
            _handleStatus = handleStatus;
            _handleLog = handleLog;
            _handleFfmpegLog = handleFfmpegLog;
        }

        public void SetStatus(string status, bool isTemplate)
        {
            lock (this)
            {
                _status = status;
                _statusIsTemplate = isTemplate;

                if (isTemplate)
                {
                    _lastPercent = -1; // Ensure that the progress report runs
                    ReportProgress(0);
                }
                else
                {
                    _handleStatus(status);
                }
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
                _handleStatus(status);
            }
        }

        public void ReportProgress<T1, T2>(int percent, T1 arg1, T2 arg2)
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

                var status = string.Format(_status, percent, arg1, arg2);
                _handleStatus(status);
            }
        }

        public void LogInfo(string logMessage)
        {
            _handleLog(logMessage);
        }

        public void LogWarning(string logMessage)
        {
            _handleLog(logMessage);
        }

        public void LogError(string logMessage)
        {
            _handleLog(logMessage);
        }

        public void LogFfmpeg(string logMessage)
        {
            _handleFfmpegLog?.Invoke(logMessage);
        }
    }
}