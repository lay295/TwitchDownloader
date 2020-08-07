using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TwitchDownloaderCore.TwitchObjects
{
    public class CheerEmote
    {
        public string prefix { get; set; }
        public List<KeyValuePair<int, ThirdPartyEmote>> tierList { get; set; } = new List<KeyValuePair<int, ThirdPartyEmote>>();

        public KeyValuePair<int, ThirdPartyEmote> getTier(int value)
        {
            KeyValuePair<int, ThirdPartyEmote> returnPair = tierList.First();
            foreach (KeyValuePair<int, ThirdPartyEmote> tierPair in tierList)
            {
                if (tierPair.Key > value)
                    break;
                returnPair = tierPair;
            }

            return returnPair;
        }
    }
}
