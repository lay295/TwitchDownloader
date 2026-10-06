using TwitchDownloaderAvalonia.ViewModels;

namespace TwitchDownloaderAvalonia.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Closing += OnClosing;
        }

        private bool _closeConfirmed;
        private bool _closePromptOpen;

        private async void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_closeConfirmed || DataContext is not MainWindowViewModel vm)
                return;

            if (!vm.HasUnfinishedWork)
                return;

            e.Cancel = true;
            if (_closePromptOpen)
                return;

            _closePromptOpen = true;

            try
            {
                if (!await vm.ConfirmCloseAsync())
                    return;

                _closeConfirmed = true;
                Close();
            }
            finally
            {
                _closePromptOpen = false;
            }
        }
    }
}
