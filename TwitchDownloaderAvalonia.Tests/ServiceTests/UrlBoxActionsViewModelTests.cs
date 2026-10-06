using TwitchDownloaderAvalonia.Tests.Fakes;
using TwitchDownloaderAvalonia.ViewModels;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class UrlBoxActionsViewModelTests
    {
        [Fact]
        public async Task PasteOverwritesTextFromClipboard()
        {
            var dialogs = new FakeDialogService { ClipboardText = "https://twitch.tv/videos/1" };
            var text = "old";
            var vm = new UrlBoxActionsViewModel(TestLocalization.Instance, dialogs, value => text = value);

            await vm.PasteCommand.ExecuteAsync(null);

            Assert.Equal("https://twitch.tv/videos/1", text);
        }

        [Fact]
        public void ClearEmptiesText()
        {
            var text = "https://twitch.tv/videos/1";
            var vm = new UrlBoxActionsViewModel(TestLocalization.Instance, new FakeDialogService(), value => text = value);

            vm.ClearCommand.Execute(null);

            Assert.Equal(string.Empty, text);
        }

        [Fact]
        public async Task PasteIgnoresEmptyClipboard()
        {
            var dialogs = new FakeDialogService { ClipboardText = "" };
            var text = "kept";
            var vm = new UrlBoxActionsViewModel(TestLocalization.Instance, dialogs, value => text = value);

            await vm.PasteCommand.ExecuteAsync(null);

            Assert.Equal("kept", text);
        }
    }
}
