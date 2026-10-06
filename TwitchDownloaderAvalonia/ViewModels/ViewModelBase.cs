namespace TwitchDownloaderAvalonia.ViewModels
{
    public abstract class ViewModelBase : ObservableObject, IDisposable
    {
        private bool _disposed;

        protected ViewModelBase(LocalizationService loc)
        {
            Loc = loc;
            Loc.CultureChanged += HandleCultureChanged;
        }

        public LocalizationService Loc { get; }

        private void HandleCultureChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(Loc));
            OnCultureChanged(sender, e);
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
            Loc.CultureChanged -= HandleCultureChanged;
            DisposeCore();
            GC.SuppressFinalize(this);
        }
    }
}
