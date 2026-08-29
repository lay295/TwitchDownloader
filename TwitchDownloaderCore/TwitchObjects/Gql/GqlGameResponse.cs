namespace TwitchDownloaderCore.TwitchObjects.Gql
{
	public class GameData
	{
		public string id { get; set; }
		public string displayName { get; set; }
		public string boxArtURL { get; set; }
	}

	public class GameResponseData
	{
		public GameData game { get; set; }
	}

	public class GqlGameResponse
	{
		public GameResponseData data { get; set; }
		public Extensions extensions { get; set; }
	}
}