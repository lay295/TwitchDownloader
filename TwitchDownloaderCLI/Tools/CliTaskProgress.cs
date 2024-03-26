using System;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCLI.Tools
{
    public class CliTaskProgress : ITaskProgress
    {
        private const string STATUS_PREAMBLE = "[STATUS] - ";
        private const string INFO_LOG_PREAMBLE = "[INFO] - ";
        private const string WARNING_LOG_PREAMBLE = "[WARNING] - ";
        private const string ERROR_LOG_PREAMBLE = "[ERROR] - ";
        private const string FFMPEG_LOG_PREAMBLE = "<FFMPEG> ";

        private string _status;
        private bool _statusIsTemplate;

        private bool _lastWriteHadNewLine = true;
        private int _lastStatusLength;
        private int _lastPercent = -1;
        private TimeSpan _lastTime1 = new(-1);
        private TimeSpan _lastTime2 = new(-1);

        public CliTaskProgress()
        {
            // TODO: Take in ITwitchDownloaderArgs to configure log levels
        }

        public void SetStatus(string status)
        {
            lock (this)
            {
                _status = status;
                _statusIsTemplate = false;

                WriteNewLineMessage(STATUS_PREAMBLE, status);
            }
        }

        public void SetTemplateStatus(string status, int initialPercent)
        {
            lock (this)
            {
                _status = status;
                _statusIsTemplate = true;

                if (!_lastWriteHadNewLine)
                {
                    Console.WriteLine();
                }

                _lastPercent = -1; // Ensure that the progress report runs
                ReportProgress(initialPercent);
            }
        }

        public void SetTemplateStatus(string status, int initialPercent, TimeSpan initialTime1, TimeSpan initialTime2)
        {
            lock (this)
            {
                _status = status;
                _statusIsTemplate = true;

                if (!_lastWriteHadNewLine)
                {
                    Console.WriteLine();
                }

                _lastPercent = -1; // Ensure that the progress report runs
                ReportProgress(initialPercent, initialTime1, initialTime2);
            }
        }

        public void ReportProgress(int percent)
        {
            lock (this)
            {
                if ((!_lastWriteHadNewLine && _lastPercent == percent)
                    || !_statusIsTemplate)
                {
                    return;
                }

                if (!_lastWriteHadNewLine)
                {
                    Console.Write('\r');
                }

                Console.Write(STATUS_PREAMBLE);

                var status = string.Format(_status, percent);
                Console.Write(status);

                var totalStatusLength = STATUS_PREAMBLE.Length + status.Length;
                if (totalStatusLength < _lastStatusLength)
                {
                    // Ensure that the previous status is completely overwritten
                    for (var i = 0; i < _lastStatusLength - totalStatusLength; i++)
                    {
                        Console.Write(' ');
                    }
                }

                _lastWriteHadNewLine = false;
                _lastStatusLength = totalStatusLength;
                _lastPercent = percent;
            }
        }

        public void ReportProgress(int percent, TimeSpan time1, TimeSpan time2)
        {
            lock (this)
            {
                if ((!_lastWriteHadNewLine && _lastPercent == percent && _lastTime1 == time1 && _lastTime2 == time2)
                    || !_statusIsTemplate)
                {
                    return;
                }

                if (!_lastWriteHadNewLine)
                {
                    Console.Write('\r');
                }

                Console.Write(STATUS_PREAMBLE);

                var status = string.Format(_status, percent, time1, time2);
                Console.Write(status);

                var totalStatusLength = STATUS_PREAMBLE.Length + status.Length;
                if (totalStatusLength < _lastStatusLength)
                {
                    // Ensure that the previous status is completely overwritten
                    for (var i = 0; i < _lastStatusLength - totalStatusLength; i++)
                    {
                        Console.Write(' ');
                    }
                }

                _lastWriteHadNewLine = false;
                _lastStatusLength = totalStatusLength;
                _lastPercent = percent;
                _lastTime1 = time1;
                _lastTime2 = time2;
            }
        }

        public void LogInfo(string logMessage)
        {
            lock (this)
            {
                WriteNewLineMessage(INFO_LOG_PREAMBLE, logMessage);
            }
        }

        public void LogWarning(string logMessage)
        {
            lock (this)
            {
                WriteNewLineMessage(WARNING_LOG_PREAMBLE, logMessage);
            }
        }

        public void LogError(string logMessage)
        {
            lock (this)
            {
                WriteNewLineMessage(ERROR_LOG_PREAMBLE, logMessage);
            }
        }

        public void LogFfmpeg(string logMessage)
        {
            lock (this)
            {
                WriteNewLineMessage(FFMPEG_LOG_PREAMBLE, logMessage);
            }
        }

        private void WriteNewLineMessage(string preamble, string message)
        {
            if (!_lastWriteHadNewLine)
            {
                Console.WriteLine();
            }

            Console.Write(preamble);
            Console.WriteLine(message);
            _lastWriteHadNewLine = true;
        }
    }
}