namespace TwitchDownloaderAvalonia.ViewModels
{
    public abstract class ViewModelBase : ObservableObject, IDisposable
    {
        private bool _disposed;

        protected ViewModelBase()
        {
            LocalizationService.Current.CultureChanged += OnCultureChanged;
        }

        protected virtual void OnCultureChanged(object? sender, EventArgs e) { }

        protected void Notify(params string[] properties)
        {
            foreach (var property in properties)
                OnPropertyChanged(property);
        }

        protected virtual void DisposeCore() { }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            LocalizationService.Current.CultureChanged -= OnCultureChanged;
            DisposeCore();
            GC.SuppressFinalize(this);
        }
    }
}
