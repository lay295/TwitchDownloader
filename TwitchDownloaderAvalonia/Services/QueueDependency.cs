namespace TwitchDownloaderAvalonia.Services
{
    internal enum QueueRunDecision
    {
        NotReady,
        Ready,
        WaitingForDependant,
        Start,
        CancelBecauseDependantFailed,
    }

    internal static class QueueDependency
    {
        public static QueueRunDecision Evaluate(QueueItemStatus status, QueueItemStatus? dependantStatus)
        {
            if (dependantStatus is null)
                return status == QueueItemStatus.Ready ? QueueRunDecision.Ready : QueueRunDecision.NotReady;

            if (status != QueueItemStatus.Waiting)
                return QueueRunDecision.NotReady;

            return dependantStatus switch
            {
                QueueItemStatus.Finished => QueueRunDecision.Start,
                QueueItemStatus.Failed or QueueItemStatus.Canceled => QueueRunDecision.CancelBecauseDependantFailed,
                _ => QueueRunDecision.WaitingForDependant,
            };
        }
    }
}
