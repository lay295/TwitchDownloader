using CommunityToolkit.Mvvm.ComponentModel;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.ViewModels
{
    public abstract class ViewModelBase : ObservableObject
    {
        protected ViewModelBase()
        {
            LocalizationService.Current.CultureChanged += OnCultureChanged;
        }

        protected virtual void OnCultureChanged(object? sender, EventArgs e)
        {
        }

        protected void Notify(params string[] properties)
        {
            foreach (var property in properties)
                OnPropertyChanged(property);
        }
    }
}
