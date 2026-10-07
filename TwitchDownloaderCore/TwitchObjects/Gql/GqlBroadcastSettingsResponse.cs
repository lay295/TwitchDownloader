namespace TwitchDownloaderCore.TwitchObjects.Gql
{
	public class BroadcastSettings
	{
		public string title { get; set; }
		public GameData game { get; set; }
	}

	public class BroadcastSettingsUser
	{
		public BroadcastSettings broadcastSettings { get; set; }
	}

	public class BroadcastSettingsData
	{
		public BroadcastSettingsUser user { get; set; }
	}

	public class GqlBroadcastSettingsResponse
	{
		public BroadcastSettingsData data { get; set; }
		public Extensions extensions { get; set; }
	}
}