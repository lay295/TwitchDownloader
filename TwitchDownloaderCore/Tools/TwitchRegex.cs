using System.Text.RegularExpressions;

namespace TwitchDownloaderCore.Tools
{
    public static class TwitchRegex
    {
        public static readonly Regex UrlTimeCode = new(@"(?<=(?:\?|&)t=)\d+h\d+m\d+s(?=$|\?|\s)", RegexOptions.Compiled);
        public static readonly Regex BitsRegex = new(
            @"(?<=(?:\s|^)(?:4Head|Anon|Bi(?:bleThumb|tBoss)|bday|C(?:h(?:eer|arity)|orgo)|cheerwal|D(?:ansGame|oodleCheer)|EleGiggle|F(?:rankerZ|ailFish)|Goal|H(?:eyGuys|olidayCheer)|K(?:appa|reygasm)|M(?:rDestructoid|uxy)|NotLikeThis|P(?:arty|ride|JSalt)|RIPCheer|S(?:coops|h(?:owLove|amrock)|eemsGood|wiftRage|treamlabs)|TriHard|uni|VoHiYo))[1-9]\d{0,6}(?=\s|$)",
            RegexOptions.Compiled);
    }
}