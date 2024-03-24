using System;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderWPF.Utils
{
    internal class WpfTaskProgress : ITaskProgress
    {
        private string _status;
        private bool _statusIsTemplate;

        private int _lastPercent = -1;
        private TimeSpan _lastTime1 = new(-1);
        private TimeSpan _lastTime2 = new(-1);

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
                    _handleStatus?.Invoke(status);
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

        public void LogInfo(string logMessage)
        {
            _handleLog?.Invoke(logMessage);
        }

        public void LogWarning(string logMessage)
        {
            _handleLog?.Invoke(logMessage);
        }

        public void LogError(string logMessage)
        {
            _handleLog?.Invoke(Translations.Strings.ErrorLog + logMessage);
        }

        public void LogFfmpeg(string logMessage)
        {
            _handleFfmpegLog?.Invoke(logMessage);
        }
    }
}