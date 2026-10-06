namespace TwitchDownloaderAvalonia.Updater.ViewModels
{
    public abstract class ViewModelBase : ObservableObject
    {
        protected ViewModelBase(LocalizationService loc)
        {
            Loc = loc;
            Loc.CultureChanged += OnLocCultureChanged;
        }

        public LocalizationService Loc { get; }

        private void OnLocCultureChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(Loc));
        }
    }
}
