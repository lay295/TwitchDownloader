namespace TwitchDownloaderAvalonia.Models
{
    public sealed record LabeledOption(string Value, string Label)
    {
        public override string ToString() => Label;
    }
}
