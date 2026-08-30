using System.ComponentModel;

namespace TwitchDownloaderWPF.Models
{
    public class MonitoredChannel : INotifyPropertyChanged
    {
        private string _status;

        public string Login { get; init; }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value)
                    return;
                _status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }

        // The stream id currently being recorded, so the same broadcast is not enqueued twice.
        public string RecordingStreamId { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
