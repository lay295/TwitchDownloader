using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace TwitchDownloaderWPF
{
    public sealed class SettingsImportCandidate
    {
        public string ConfigPath { get; init; }
        public string DisplayName { get; init; }
        public DateTime LastModified { get; init; }
        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// A code-defined (no XAML) one-time dialog that lets the user import an existing
    /// TwitchDownloader user.config from another install/build identity.
    /// </summary>
    public sealed class WindowSettingsImport : Window
    {
        private readonly ListBox _list;

        /// <summary>The chosen user.config path, or null if the user chose to start fresh.</summary>
        public string SelectedConfigPath { get; private set; }

        public WindowSettingsImport(IReadOnlyList<SettingsImportCandidate> candidates)
        {
            Title = "Import TwitchDownloader Settings";
            Width = 720;
            Height = 360;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            var grid = new Grid { Margin = new Thickness(14) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var info = new TextBlock
            {
                Text = "Existing TwitchDownloader settings were found from other installs/builds. "
                     + "Pick one to import into this build (it will be copied in and used from now on), "
                     + "or start fresh. This prompt only appears once.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(info, 0);
            grid.Children.Add(info);

            _list = new ListBox { Margin = new Thickness(0, 0, 0, 10) };
            foreach (var c in candidates)
                _list.Items.Add(c);
            if (_list.Items.Count > 0)
                _list.SelectedIndex = 0;
            _list.MouseDoubleClick += (_, _) => Import();
            Grid.SetRow(_list, 1);
            grid.Children.Add(_list);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var importBtn = new Button { Content = "Import Selected", MinWidth = 120, Height = 28, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            importBtn.Click += (_, _) => Import();
            var freshBtn = new Button { Content = "Start Fresh", MinWidth = 100, Height = 28, IsCancel = true };
            freshBtn.Click += (_, _) => { SelectedConfigPath = null; DialogResult = true; Close(); };
            buttons.Children.Add(importBtn);
            buttons.Children.Add(freshBtn);
            Grid.SetRow(buttons, 2);
            grid.Children.Add(buttons);

            Content = grid;
        }

        private void Import()
        {
            if (_list.SelectedItem is not SettingsImportCandidate candidate)
                return;

            SelectedConfigPath = candidate.ConfigPath;
            DialogResult = true;
            Close();
        }
    }
}
