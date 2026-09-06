using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class DialogService
    {
        private Window? _owner;

        public void SetOwner(Window owner)
        {
            _owner = owner;
        }

        public Task ShowErrorAsync(string title, string message)
        {
            return ShowMessageAsync(title, message);
        }

        public async Task ShowMessageAsync(string title, string message)
        {
            if (_owner is null)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = CreateMessageWindow(title, message, okOnly: true);
                await dialog.ShowDialog(_owner!);
            });
        }

        public CollisionPromptResult PromptCollision(string fileName, string fullPath)
        {
            if (_owner is null)
                return new CollisionPromptResult(CollisionChoice.Cancel, false);

            return Dispatcher.UIThread.InvokeAsync(() => ShowCollisionDialogAsync(fileName, fullPath)).GetAwaiter()
                .GetResult();
        }

        private async Task<CollisionPromptResult> ShowCollisionDialogAsync(string fileName, string fullPath)
        {
            var remember = false;
            var choice = CollisionChoice.Cancel;

            var rememberBox = new CheckBox { Content = "Remember this choice for this session" };
            var overwrite = new Button { Content = "Overwrite", MinWidth = 100 };
            var rename = new Button { Content = "Rename", MinWidth = 100 };
            var cancel = new Button { Content = "Cancel", MinWidth = 100 };

            var dialog = new Window
            {
                Title = "File already exists",
                Width = 460,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16),
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"{fileName} already exists.",
                            TextWrapping = TextWrapping.Wrap,
                            FontWeight = FontWeight.SemiBold,
                        },
                        new TextBlock
                        {
                            Text = fullPath,
                            TextWrapping = TextWrapping.Wrap,
                        },
                        rememberBox,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 8,
                            Children = { overwrite, rename, cancel },
                        },
                    },
                },
            };

            overwrite.Click += (_, _) =>
            {
                choice = CollisionChoice.Overwrite;
                remember = rememberBox.IsChecked == true;
                dialog.Close();
            };
            rename.Click += (_, _) =>
            {
                choice = CollisionChoice.Rename;
                remember = rememberBox.IsChecked == true;
                dialog.Close();
            };
            cancel.Click += (_, _) =>
            {
                choice = CollisionChoice.Cancel;
                remember = rememberBox.IsChecked == true;
                dialog.Close();
            };

            await dialog.ShowDialog(_owner!);
            return new CollisionPromptResult(choice, remember);
        }

        private static Window CreateMessageWindow(string title, string message, bool okOnly)
        {
            var close = new Button { Content = "OK", MinWidth = 80, HorizontalAlignment = HorizontalAlignment.Right };
            var window = new Window
            {
                Title = title,
                Width = 460,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16),
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                        close,
                    },
                },
            };
            close.Click += (_, _) => window.Close();
            _ = okOnly;
            return window;
        }
    }

    public enum CollisionChoice
    {
        Overwrite,
        Rename,
        Cancel,
    }

    public readonly record struct CollisionPromptResult(CollisionChoice Choice, bool Remember);
}