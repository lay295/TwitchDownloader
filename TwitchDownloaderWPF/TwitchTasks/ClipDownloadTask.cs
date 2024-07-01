using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderWPF.Translations;
using TwitchDownloaderWPF.Utils;

namespace TwitchDownloaderWPF.TwitchTasks;

internal class ClipDownloadTask : ITwitchTask {

    private bool _canCancel = true;

    private TwitchTaskException _exception = new();

    private int _progress;

    private TwitchTaskStatus _status = TwitchTaskStatus.Ready;

    public ClipDownloadOptions DownloadOptions { get; init; }
    public TaskData Info { get; set; } = new();

    public int Progress {
        get => this._progress;
        private set => this.SetField(ref this._progress, value);
    }

    public TwitchTaskStatus Status {
        get => this._status;
        private set => this.SetField(ref this._status, value);
    }

    public CancellationTokenSource TokenSource { get; set; } = new();
    public ITwitchTask DependantTask { get; set; }
    public string TaskType { get; } = Strings.ClipDownload;

    public TwitchTaskException Exception {
        get => this._exception;
        private set => this.SetField(ref this._exception, value);
    }

    public string OutputFile => this.DownloadOptions.Filename;

    public bool CanCancel {
        get => this._canCancel;
        private set => this.SetField(ref this._canCancel, value);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public void Cancel() {
        if (!this.CanCancel)
            return;

        this.TokenSource.Cancel();

        if (this.Status == TwitchTaskStatus.Running) {
            this.ChangeStatus(TwitchTaskStatus.Stopping);
            return;
        }

        this.ChangeStatus(TwitchTaskStatus.Canceled);
    }

    public bool CanRun() => this.Status == TwitchTaskStatus.Ready;

    public async Task RunAsync() {
        if (this.TokenSource.IsCancellationRequested) {
            this.TokenSource.Dispose();
            this.ChangeStatus(TwitchTaskStatus.Canceled);
            return;
        }

        var progress = new WpfTaskProgress(i => this.Progress = i);
        var downloader = new ClipDownloader(this.DownloadOptions, progress);
        this.ChangeStatus(TwitchTaskStatus.Running);
        try {
            await downloader.DownloadAsync(this.TokenSource.Token);
            if (this.TokenSource.IsCancellationRequested)
                this.ChangeStatus(TwitchTaskStatus.Canceled);
            else {
                progress.ReportProgress(100);
                this.ChangeStatus(TwitchTaskStatus.Finished);
            }
        } catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException
            && this.TokenSource.IsCancellationRequested) {
            this.ChangeStatus(TwitchTaskStatus.Canceled);
        } catch (Exception ex) {
            this.ChangeStatus(TwitchTaskStatus.Failed);
            this.Exception = new(ex);
        }

        downloader = null;
        this.TokenSource.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    public void ChangeStatus(TwitchTaskStatus newStatus) {
        this.Status = newStatus;

        if (this.CanCancel
            && newStatus is TwitchTaskStatus.Canceled
                or TwitchTaskStatus.Failed
                or TwitchTaskStatus.Finished
                or TwitchTaskStatus.Stopping)
            this.CanCancel = false;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
        this.PropertyChanged?.Invoke(this, new(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;

        field = value;
        this.OnPropertyChanged(propertyName);
        return true;
    }
}
