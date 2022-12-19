using System;

namespace TwitchDownloader.TwitchTasks
{
    public class TwitchTaskException
    {
        public Exception Exception { get; private set; } = null;
        public string Visibility { get; private set; } = "Collapsed";

        public TwitchTaskException() { }

        public TwitchTaskException(Exception ex)
        {
            Exception = ex;
            Visibility = "Visible";
        }
    }
}
