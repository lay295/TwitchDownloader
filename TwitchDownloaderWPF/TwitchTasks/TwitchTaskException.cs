using System;
using System.Windows;

namespace TwitchDownloaderWPF.TwitchTasks;

public class TwitchTaskException {

    public TwitchTaskException() { }

    public TwitchTaskException(Exception ex) {
        this.Exception = ex;
        this.Visibility = Visibility.Visible;
    }

    public Exception Exception { get; private set; }
    public Visibility Visibility { get; private set; } = Visibility.Collapsed;
}
