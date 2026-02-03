using System.Text.RegularExpressions;

namespace TwitchDownloaderCore.Tools
{
    public static partial class TwitchRegex
    {
        [GeneratedRegex("""(?<=(?:\?|&)t=)(?:\d+[dhms]){1,4}(?=$|&|\s)""")]
        public static partial Regex UrlTimeCode { get; }

        [GeneratedRegex("""(?<=(?:\s|^)(?:4Head|Anon|Bi(?:bleThumb|tBoss)|bday|C(?:h(?:eer|arity)|orgo)|cheerwal|D(?:ansGame|oodleCheer)|EleGiggle|F(?:rankerZ|ailFish)|Goal|H(?:eyGuys|olidayCheer)|K(?:appa|reygasm)|M(?:rDestructoid|uxy)|NotLikeThis|P(?:arty|ride|JSalt)|RIPCheer|S(?:coops|h(?:owLove|amrock)|eemsGood|wiftRage|treamlabs)|TriHard|uni|VoHiYo))[1-9]\d{0,6}(?=\s|$)""")]
        public static partial Regex BitsRegex { get; }
    }
}