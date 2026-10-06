using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class QueueDependencyTests
    {
        [Fact]
        public void IndependentChatTaskIsReady()
        {
            var decision = QueueDependency.Evaluate(QueueItemStatus.Ready, dependantStatus: null);

            Assert.Equal(QueueRunDecision.Ready, decision);
        }

        [Fact]
        public void ChatWithDependantStartsAfterDependantFinishes()
        {
            var decision = QueueDependency.Evaluate(QueueItemStatus.Waiting, QueueItemStatus.Finished);

            Assert.Equal(QueueRunDecision.Start, decision);
        }

        [Fact]
        public void ChatWithDependantWaitsWhileDependantRuns()
        {
            var decision = QueueDependency.Evaluate(QueueItemStatus.Waiting, QueueItemStatus.Running);

            Assert.Equal(QueueRunDecision.WaitingForDependant, decision);
        }

        [Fact]
        public void ChatWithFailedDependantIsCanceled()
        {
            var decision = QueueDependency.Evaluate(QueueItemStatus.Waiting, QueueItemStatus.Failed);

            Assert.Equal(QueueRunDecision.CancelBecauseDependantFailed, decision);
        }

        [Fact]
        public void WaitingIndependentTaskIsNotReady()
        {
            var decision = QueueDependency.Evaluate(QueueItemStatus.Waiting, dependantStatus: null);

            Assert.Equal(QueueRunDecision.NotReady, decision);
        }
    }
}
