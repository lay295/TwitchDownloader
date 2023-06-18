using System;
using System.Windows;

namespace TwitchDownloaderWPF.TwitchTasks
{
    public class TwitchTaskException
    {
        public Exception Exception { get; private set; } = null;
        public Visibility Visibility { get; private set; } = Visibility.Collapsed;

        public TwitchTaskException() { }

        public TwitchTaskException(Exception ex)
        {
            Exception = ex;
            Visibility = Visibility.Visible;
        }
    }
}
