using System.Threading.Tasks;

namespace TwitchDownloaderCore.Extensions
{
    public static class ValueTaskExtensions
    {
        public static void RunSynchronously(this ValueTask valueTask)
        {
            if (!valueTask.IsCompleted)
            {
                valueTask.AsTask().GetAwaiter().GetResult();
            }
        }

        public static T RunSynchronously<T>(this ValueTask<T> valueTask)
        {
            return valueTask.IsCompleted ? valueTask.Result : valueTask.AsTask().GetAwaiter().GetResult();
        }
    }
}