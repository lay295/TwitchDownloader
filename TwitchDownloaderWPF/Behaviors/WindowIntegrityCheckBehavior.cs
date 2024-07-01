using System.Windows;
using TwitchDownloaderCore.Tools;

namespace TwitchDownloaderWPF.Behaviors;

public class WindowIntegrityCheckBehavior : DependencyObject {
    public static readonly DependencyProperty IntegrityCheckProperty = DependencyProperty.RegisterAttached(
        nameof(WindowIntegrityCheckBehavior.IntegrityCheck),
        typeof(bool),
        typeof(WindowIntegrityCheckBehavior),
        new(false, OnPropertyChanged)
    );

    public bool IntegrityCheck {
        get => (bool)this.GetValue(WindowIntegrityCheckBehavior.IntegrityCheckProperty);
        set => this.SetValue(WindowIntegrityCheckBehavior.IntegrityCheckProperty, value);
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        if (d is not Window)
            return;

        CoreLicensor.EnsureFilesExist(null);
    }
}
