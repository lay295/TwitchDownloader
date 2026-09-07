using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace TwitchDownloaderAvalonia.Services
{
    public sealed class FileDialogService
    {
        private Window? _owner;

        public void SetOwner(Window owner)
        {
            _owner = owner;
        }

        public async Task<string?> SaveFileAsync(string suggestedFileName, string filterName, string extension)
        {
            if (_owner is null)
                return null;

            var storage = _owner.StorageProvider;
            if (!storage.CanSave)
                return null;

            var normalizedExtension = extension.TrimStart('.');
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save file",
                SuggestedFileName = suggestedFileName,
                DefaultExtension = normalizedExtension,
                FileTypeChoices =
                [
                    new FilePickerFileType(filterName)
                    {
                        Patterns = [$"*.{normalizedExtension}"],
                    },
                ],
            });

            return file?.TryGetLocalPath();
        }

        public async Task<string?> OpenFileAsync(string title, string filterName, IReadOnlyList<string> patterns)
        {
            if (_owner is null)
                return null;

            var storage = _owner.StorageProvider;
            if (!storage.CanOpen)
                return null;

            var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(filterName)
                    {
                        Patterns = [.. patterns],
                    },
                ],
            });

            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }
    }
}
