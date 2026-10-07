namespace TwitchDownloaderCore.TwitchObjects.Gql
{

	public class StreamId
	{
		public string id { get; set; }
	}

	public class ChannelStreamData
	{
		public string id { get; set; }
		public StreamId stream { get; set; }
	}

	public class GqlStreamResponse
	{
		public ChannelStreamData data { get; set; }
		public Extensions extensions { get; set; }
	}
}