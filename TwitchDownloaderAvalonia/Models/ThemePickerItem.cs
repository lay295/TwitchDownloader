namespace TwitchDownloaderAvalonia.Models
{
    public sealed record ThemePickerItem(
        string Value,
        string Label,
        bool IsDark = false,
        bool IsSystem = false,
        bool IsHeader = false,
        bool IsPreferred = false)
    {
        public bool IsSelectable => !IsHeader && !IsSystem;

        public bool HasName(string name) => StringComparer.OrdinalIgnoreCase.Equals(Value, name);

        public override string ToString() => Label;
    }
}
