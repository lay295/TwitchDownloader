using TwitchDownloaderAvalonia.Tests.Fakes;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class FakeDialogServiceTests
    {
        [Fact]
        public async Task RecordsMessagesConfirmsAndClipboard()
        {
            var dialogs = new FakeDialogService { ConfirmResult = true };

            await dialogs.ShowMessageAsync("t1", "m1");
            var confirmed = await dialogs.ShowConfirmAsync("t2", "m2");
            await dialogs.CopyTextAsync("clipboard");

            Assert.True(confirmed);
            Assert.Equal(("t1", "m1"), Assert.Single(dialogs.Messages));
            Assert.Equal(("t2", "m2"), Assert.Single(dialogs.Confirms));
            Assert.Equal("clipboard", Assert.Single(dialogs.Copied));
        }
    }
}
