using System;
using System.Runtime.CompilerServices;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Translations;

namespace TwitchDownloaderWPF.Utils;

internal class WpfTaskProgress : ITaskProgress {
    private readonly Action<string> _handleFfmpegLog;
    private readonly Action<string> _handleLog;

    private readonly Action<int> _handlePercent;
    private readonly Action<string> _handleStatus;

    private readonly LogLevel _logLevel;

    private int _lastPercent = -1;
    private TimeSpan _lastTime1 = new(-1);
    private TimeSpan _lastTime2 = new(-1);
    private string _status;
    private bool _statusIsTemplate;

    public WpfTaskProgress(Action<int> handlePercent) {
        this._handlePercent = handlePercent;
        this._handleStatus = null;
        this._handleLog = null;
        this._handleFfmpegLog = null;

        this._logLevel = LogLevel.None;
    }

    public WpfTaskProgress(
        LogLevel logLevel,
        Action<int> handlePercent,
        Action<string> handleStatus,
        Action<string> handleLog,
        Action<string> handleFfmpegLog = null
    ) {
        this._handlePercent = handlePercent;
        this._handleStatus = handleStatus;
        this._handleLog = handleLog;
        this._handleFfmpegLog = handleFfmpegLog;

        this._logLevel = logLevel;
        if (handleFfmpegLog is not null)
            // TODO: Make this user configurable
            this._logLevel |= LogLevel.Ffmpeg;
    }

    public void SetStatus(string status) {
        lock (this) {
            this._status = status;
            this._statusIsTemplate = false;

            this._handleStatus?.Invoke(status);
        }
    }

    public void SetTemplateStatus(string status, int initialPercent) {
        lock (this) {
            this._status = status;
            this._statusIsTemplate = true;

            this._lastPercent = -1; // Ensure that the progress report runs
            this.ReportProgress(initialPercent);
        }
    }

    public void SetTemplateStatus(string status, int initialPercent, TimeSpan initialTime1, TimeSpan initialTime2) {
        lock (this) {
            this._status = status;
            this._statusIsTemplate = true;

            this._lastPercent = -1; // Ensure that the progress report runs
            this.ReportProgress(initialPercent, initialTime1, initialTime2);
        }
    }

    public void ReportProgress(int percent) {
        lock (this) {
            if (this._lastPercent == percent)
                return;

            this._handlePercent(percent);
            this._lastPercent = percent;

            if (!this._statusIsTemplate)
                return;

            var status = string.Format(this._status, percent);
            this._handleStatus?.Invoke(status);
        }
    }

    public void ReportProgress(int percent, TimeSpan time1, TimeSpan time2) {
        lock (this) {
            if (this._lastPercent == percent && this._lastTime1 == time1 && this._lastTime2 == time2)
                return;

            this._handlePercent(percent);
            this._lastPercent = percent;
            this._lastTime1 = time1;
            this._lastTime2 = time2;

            if (!this._statusIsTemplate)
                return;

            var status = string.Format(this._status, percent, time1, time2);
            this._handleStatus?.Invoke(status);
        }
    }

    public void LogVerbose(string logMessage) {
        if ((this._logLevel & LogLevel.Verbose) == 0) return;

        this._handleLog?.Invoke(logMessage);
    }

    public void LogVerbose(DefaultInterpolatedStringHandler logMessage) {
        if ((this._logLevel & LogLevel.Verbose) == 0) return;

        this._handleLog?.Invoke(logMessage.ToStringAndClear());
    }

    public void LogInfo(string logMessage) {
        if ((this._logLevel & LogLevel.Info) == 0) return;

        this._handleLog.Invoke(logMessage);
    }

    public void LogInfo(DefaultInterpolatedStringHandler logMessage) {
        if ((this._logLevel & LogLevel.Info) == 0) return;

        this._handleLog.Invoke(logMessage.ToStringAndClear());
    }

    public void LogWarning(string logMessage) {
        if ((this._logLevel & LogLevel.Warning) == 0) return;

        this._handleLog?.Invoke(logMessage);
    }

    public void LogWarning(DefaultInterpolatedStringHandler logMessage) {
        if ((this._logLevel & LogLevel.Warning) == 0) return;

        this._handleLog?.Invoke(logMessage.ToStringAndClear());
    }

    public void LogError(string logMessage) {
        if ((this._logLevel & LogLevel.Error) == 0) return;

        this._handleLog?.Invoke(Strings.ErrorLog + logMessage);
    }

    public void LogError(DefaultInterpolatedStringHandler logMessage) {
        if ((this._logLevel & LogLevel.Error) == 0) return;

        this._handleLog?.Invoke(Strings.ErrorLog + logMessage.ToStringAndClear());
    }

    public void LogFfmpeg(string logMessage) {
        if ((this._logLevel & LogLevel.Ffmpeg) == 0) return;

        this._handleFfmpegLog?.Invoke(logMessage);
    }
}
