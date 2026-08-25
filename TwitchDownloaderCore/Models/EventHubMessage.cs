using System.Text.Json.Serialization;

namespace TwitchDownloaderCore.Models
{
	public enum EventHubMessageType
	{
		Unknown,
		Subscribe,
		SubscribeResponse,
		Unsubscribe,
		UnsubscribeResponse,
		Welcome,
		KeepAlive,
		Reconnect,
		Notification
	}

	public enum EventHubSubscriptionResult
	{
		Ok,
		Error
	}

	public abstract class EventHubMessageData;

	public sealed class EventHubMessage
	{
		public string id { get; set; }
		public EventHubMessageType type { get; set; }
		public DateTimeOffset timestamp { get; set; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // unnecessary since this is handled by a custom converter, but here for documentation
		public string parentId { get; set; } = null;
		public EventHubMessageData Data { get; set; }
	}

	public sealed class SubscribeData : EventHubMessageData
	{
		public string id { get; set; }
		public string type { get => "pubsub"; }
		public SubscriptionPubSub pubsub { get; set; }
	}

	public sealed class SubscriptionPubSub
	{
		public string topic { get; set; }
	}

	public sealed class UnsubscribeData : EventHubMessageData
	{
		public string id { get; set; }
	}

	public sealed class SubscriptionChangeResponseData : EventHubMessageData
	{
		public EventHubSubscriptionResult result { get; set; }
		public SubscriptionId subscription { get; set; }
	}

	public sealed class SubscriptionId
	{
		public string id { get; set; }
	}

	public sealed class WelcomeData : EventHubMessageData
	{
		public int keepaliveSec { get; set; }
		public string recoveryUrl { get; set; }
		public string sessionId { get; set; }
	}

	public sealed class ReconnectData : EventHubMessageData
	{
		public string url { get; set; }
	}

	public sealed class NotificationData : EventHubMessageData
	{
		public SubscriptionId subscription { get; set; }
		public string type { get => "pubsub"; }
		public PubSubData pubsub { get; set; }
	}

	/* 
	================================================================================================================
	================================================ NOTIFICATIONS =================================================
	================================================================================================================
	These are the types that can be parsed from a EventHubMessage.Data.pubsub for notifications
	*/

	/*
	not all possible pubsub values have a type property
	should we ever need for example PlusProgramPoints or LeaderBoardEventsBitsAndGiftSubs
	then we would need separate handling for those
	*/
	[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
	[JsonDerivedType(typeof(StreamUpData), typeDiscriminator: "stream-up")]
	[JsonDerivedType(typeof(StreamDownData), typeDiscriminator: "stream-down")]
	[JsonDerivedType(typeof(ViewCountData), typeDiscriminator: "viewcount")]
	[JsonDerivedType(typeof(CommercialData), typeDiscriminator: "commercial")]
	[JsonDerivedType(typeof(BroadcastSettingsUpdateData), typeDiscriminator: "broadcast_settings_update")]
	public abstract class PubSubData;

	public abstract class VideoPlaybackByIdData : PubSubData
	{
		public double server_time { get; set; }
	}

	public sealed class StreamUpData : VideoPlaybackByIdData
	{
		public int play_delay { get; set; }
	}

	public sealed class StreamDownData : VideoPlaybackByIdData
	{
	}

	public sealed class ViewCountData : VideoPlaybackByIdData
	{
		public int viewers { get; set; }
		public string collaboration_status { get; set; } /* "none" */
		public int collaboration_viewers { get; set; }
		public string costream_status { get; set; } /* "" */
		public int costream_viewers { get; set; }
	}

	public sealed class CommercialData : VideoPlaybackByIdData
	{
		public int length { get; set; }
		public bool scheduled { get; set; }
	}

	public sealed class BroadcastSettingsUpdateData : PubSubData
	{
		public string channel_id { get; set; }
		public string channel { get; set; }
		public string old_status { get; set; }
		public string status { get; set; }
		public int old_game_id { get; set; }
		public string old_game { get; set; }
		public int game_id { get; set; }
		public string game { get; set; }
	};
}