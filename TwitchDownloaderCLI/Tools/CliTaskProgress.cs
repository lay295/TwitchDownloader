using System;
using System.Runtime.CompilerServices;
using TwitchDownloaderCLI.Models;
using TwitchDownloaderCore.Interfaces;

namespace TwitchDownloaderCLI.Tools
{
    internal class CliTaskProgress : ITaskProgress
    {
        private const string STATUS_PREAMBLE = "[STATUS] - ";
        private const string VERBOSE_LOG_PREAMBLE = "[VERBOSE] - ";
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

        private readonly LogLevel _logLevel;

        public CliTaskProgress(LogLevel logLevel)
        {
            if ((logLevel & LogLevel.None) == 0)
                this._logLevel = logLevel;
        }

        public void SetStatus(string status)
        {
            if ((this._logLevel & LogLevel.Status) == 0) return;

            lock (this)
            {
                this._status = status;
                this._statusIsTemplate = false;

                this.WriteNewLineMessage(STATUS_PREAMBLE, status);
            }
        }

        public void SetTemplateStatus(string status, int initialPercent)
        {
            if ((this._logLevel & LogLevel.Status) == 0) return;

            lock (this)
            {
                this._status = status;
                this._statusIsTemplate = true;

                if (!this._lastWriteHadNewLine)
                    Console.WriteLine();

                this._lastPercent = -1; // Ensure that the progress report runs
                this.ReportProgress(initialPercent);
            }
        }

        public void SetTemplateStatus(string status, int initialPercent, TimeSpan initialTime1, TimeSpan initialTime2)
        {
            if ((this._logLevel & LogLevel.Status) == 0) return;

            lock (this)
            {
                this._status = status;
                this._statusIsTemplate = true;

                if (!this._lastWriteHadNewLine)
                    Console.WriteLine();

                this._lastPercent = -1; // Ensure that the progress report runs
                this.ReportProgress(initialPercent, initialTime1, initialTime2);
            }
        }

        public void ReportProgress(int percent) {
            if ((this._logLevel & LogLevel.Status) == 0) return;

            lock (this) 
            {
                if ((!this._lastWriteHadNewLine && this._lastPercent == percent)
                    || !this._statusIsTemplate)
                    return;

                var status = string.Format(this._status, percent);
                this._lastStatusLength = this.WriteSameLineMessage(
                    CliTaskProgress.STATUS_PREAMBLE,
                    status,
                    this._lastStatusLength
                );

                this._lastWriteHadNewLine = false;
                this._lastPercent = percent;
            }
        }

        public void ReportProgress(int percent, TimeSpan time1, TimeSpan time2)
        {
            if ((this._logLevel & LogLevel.Status) == 0) return;

            lock (this) 
            {
                if ((!this._lastWriteHadNewLine
                        && this._lastPercent == percent
                        && this._lastTime1 == time1
                        && this._lastTime2 == time2)
                    || !this._statusIsTemplate)
                    return;

                var status = string.Format(this._status, percent, time1, time2);
                this._lastStatusLength = this.WriteSameLineMessage(
                    CliTaskProgress.STATUS_PREAMBLE,
                    status,
                    this._lastStatusLength
                );

                this._lastWriteHadNewLine = false;
                this._lastPercent = percent;
                this._lastTime1 = time1;
                this._lastTime2 = time2;
            }
        }

        private int WriteSameLineMessage(string preamble, string message, int previousMessageLength)
        {
            if (!this._lastWriteHadNewLine)
            {
                Console.Write('\r');
            }

            Console.Write(preamble);
            Console.Write(message);

            var messageLength = preamble.Length + message.Length;
            if (messageLength >= previousMessageLength)
                return messageLength;

            // Ensure that the previous line is completely overwritten
            for (var i = 0; i < previousMessageLength - messageLength; ++i)
                Console.Write(' ');

            return messageLength;
        }

        public void LogVerbose(string logMessage)
        {
            if ((this._logLevel & LogLevel.Verbose) == 0) return;

            lock (this)
                this.WriteNewLineMessage(VERBOSE_LOG_PREAMBLE, logMessage);
        }

        public void LogVerbose(DefaultInterpolatedStringHandler logMessage)
        {
            if ((this._logLevel & LogLevel.Verbose) == 0) return;

            lock (this)
                this.WriteNewLineMessage(VERBOSE_LOG_PREAMBLE, logMessage.ToStringAndClear());
        }

        public void LogInfo(string logMessage)
        {
            if ((this._logLevel & LogLevel.Info) == 0) return;

            lock (this)
                this.WriteNewLineMessage(INFO_LOG_PREAMBLE, logMessage);
        }

        public void LogInfo(DefaultInterpolatedStringHandler logMessage)
        {
            if ((this._logLevel & LogLevel.Info) == 0) return;

            lock (this)
                this.WriteNewLineMessage(INFO_LOG_PREAMBLE, logMessage.ToStringAndClear());
        }

        public void LogWarning(string logMessage)
        {
            if ((this._logLevel & LogLevel.Warning) == 0) return;

            lock (this)
                this.WriteNewLineMessage(WARNING_LOG_PREAMBLE, logMessage);
        }

        public void LogWarning(DefaultInterpolatedStringHandler logMessage)
        {
            if ((this._logLevel & LogLevel.Warning) == 0) return;

            lock (this)
                this.WriteNewLineMessage(WARNING_LOG_PREAMBLE, logMessage.ToStringAndClear());
        }

        public void LogError(string logMessage)
        {
            if ((this._logLevel & LogLevel.Error) == 0) return;

            lock (this)
                this.WriteNewLineMessage(ERROR_LOG_PREAMBLE, logMessage);
        }

        public void LogError(DefaultInterpolatedStringHandler logMessage)
        {
            if ((this._logLevel & LogLevel.Error) == 0) return;

            lock (this)
                this.WriteNewLineMessage(ERROR_LOG_PREAMBLE, logMessage.ToStringAndClear());
        }

        public void LogFfmpeg(string logMessage)
        {
            if ((this._logLevel & LogLevel.Ffmpeg) == 0) return;

            lock (this)
                this.WriteNewLineMessage(FFMPEG_LOG_PREAMBLE, logMessage);
        }

        private void WriteNewLineMessage(string preamble, string message)
        {
            if (!this._lastWriteHadNewLine)
                Console.WriteLine();

            Console.Write(preamble);
            Console.WriteLine(message);
            this._lastWriteHadNewLine = true;
        }

        ~CliTaskProgress() {
            if (this._lastWriteHadNewLine)
                return;

            // Some shells don't like when an application exits without writing a newline to the end of stdout
            Console.WriteLine();
            this._lastWriteHadNewLine = true;
        }
    }
}