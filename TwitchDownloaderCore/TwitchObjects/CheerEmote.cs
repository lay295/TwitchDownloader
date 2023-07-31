using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TwitchDownloaderCore.TwitchObjects
{
    [DebuggerDisplay("{prefix}")]
    public sealed class CheerEmote : IDisposable
    {
        public bool Disposed { get; private set; } = false;
        public string prefix { get; set; }
        public List<KeyValuePair<int, TwitchEmote>> tierList { get; set; } = new List<KeyValuePair<int, TwitchEmote>>();

        public KeyValuePair<int, TwitchEmote> getTier(int value)
        {
            KeyValuePair<int, TwitchEmote> returnPair = tierList.First();
            foreach (KeyValuePair<int, TwitchEmote> tierPair in tierList)
            {
                if (tierPair.Key > value)
                    break;
                returnPair = tierPair;
            }

            return returnPair;
        }

        public void Resize(double newScale)
        {
            for (int i = 0; i < tierList.Count; i++)
            {
                tierList[i].Value.Resize(newScale);
            }
        }

#region ImplementIDisposable

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool isDisposing)
        {
            try
            {
                if (Disposed)
                {
                    return;
                }

                if (isDisposing)
                {
                    foreach (var (_, emote) in tierList)
                    {
                        emote?.Dispose();
                    }
                }
            }
            finally
            {
                Disposed = true;
            }
        }

#endregion
    }
}
