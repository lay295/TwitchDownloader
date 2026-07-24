namespace TwitchDownloaderCore.Models.Render
{
    public class CommentSection
    {
        public SectionImage Image { get; set; }
        public List<EmotePosition> Emotes { get; set; }
        public int CommentIndex { get; set; }
    }
}
