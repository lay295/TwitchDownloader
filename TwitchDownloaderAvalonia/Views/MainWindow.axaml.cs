using Avalonia.Controls;

namespace TwitchDownloaderAvalonia.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Opened += OnOpened;
        }

        private async void OnOpened(object? sender, EventArgs e)
        {
            Opened -= OnOpened;
            if (DataContext is ViewModels.MainWindowViewModel vm)
                await vm.InitializeAsync();
        }
    }
}
