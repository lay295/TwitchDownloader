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
            {
                return null;
            }

            var normalizedExtension = extension.TrimStart('.');
            var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = suggestedFileName,
                DefaultExtension = normalizedExtension,
                FileTypeChoices =
                [
                    new FilePickerFileType(filterName)
                    {
                        Patterns = [$"*.{normalizedExtension}"]
                    }
                ]
            });

            return file?.TryGetLocalPath();
        }
    }
}
