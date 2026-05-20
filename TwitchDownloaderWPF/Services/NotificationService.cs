using System.Windows;

namespace TwitchDownloaderWPF.Services
{
    public static class NotificationService
    {
        public static void Show(string title, string message, bool isError)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var toast = new WindowToast(title, message, isError);
                toast.Show();
            });
        }
    }
}
