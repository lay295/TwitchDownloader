using System.Runtime.ExceptionServices;

namespace TwitchDownloaderWPF.Tests.TestHelpers
{
    /// <summary>
    /// WPF controls such as <see cref="System.Windows.Controls.TextBox"/> can only be created on an STA thread, and xunit runs tests on
    /// MTA thread-pool threads. This runs a test body on a dedicated STA thread (no Application or window needed) and rethrows its failure.
    /// </summary>
    internal static class StaThread
    {
        public static void Run(Action action)
        {
            ExceptionDispatchInfo? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            failure?.Throw();
        }
    }
}
