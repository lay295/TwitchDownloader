using System.Windows;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderWPF.Behaviors
{
    public class WindowIntegrityCheckBehavior : DependencyObject
    {
        public static readonly DependencyProperty IntegrityCheckProperty = DependencyProperty.RegisterAttached(
            nameof(IntegrityCheck), typeof(bool), typeof(WindowIntegrityCheckBehavior), new PropertyMetadata(false, OnPropertyChanged));

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Window)
            {
                return;
            }

            CoreLicensor.EnsureFilesExist(null);
        }

        public bool IntegrityCheck
        {
            get => (bool)GetValue(IntegrityCheckProperty);
            set => SetValue(IntegrityCheckProperty, value);
        }
    }
}