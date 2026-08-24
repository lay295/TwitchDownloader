using System.Text.Json;
using System.Text.Json.Serialization;

namespace TwitchDownloaderCore.Models
{
	public sealed class EventHubMessageConverter : JsonConverter<EventHubMessage>
	{
		public override EventHubMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			using var doc = JsonDocument.ParseValue(ref reader);
			var root = doc.RootElement;

			var message = new EventHubMessage
			{
				id = root.GetProperty("id").GetString(),
				type = root.GetProperty("type").GetString() switch
				{
					"welcome" => EventHubMessageType.Welcome,
					"subscribe" => EventHubMessageType.Subscribe,
					"subscribeResponse" => EventHubMessageType.SubscribeResponse,
					"unsubscribe" => EventHubMessageType.Unsubscribe,
					"unsubscribeResponse" => EventHubMessageType.UnsubscribeResponse,
					"keepalive" => EventHubMessageType.KeepAlive,
					"reconnect" => EventHubMessageType.Reconnect,
					"notification" => EventHubMessageType.Notification,
					_ => EventHubMessageType.Unknown
				},
				timestamp = root.GetProperty("timestamp").GetDateTime()
			};

			message.Data = message.type switch
			{
				EventHubMessageType.Welcome => root.GetProperty("welcome").Deserialize<WelcomeData>(options),
				EventHubMessageType.Subscribe => root.GetProperty("subscribe").Deserialize<SubscribeData>(options),
				EventHubMessageType.SubscribeResponse => new SubscriptionChangeResponseData
				{
					result = root.GetProperty("subscribeResponse").GetProperty("result").GetString() switch
					{
						"ok" => EventHubSubscriptionResult.Ok,
						"error" => EventHubSubscriptionResult.Error,
						string str => throw new JsonException($"unknown subscription response result {str}")
					},
					subscription = root.GetProperty("subscribeResponse").GetProperty("subscription").Deserialize<SubscriptionId>(options)
				},
				EventHubMessageType.Unsubscribe => root.GetProperty("unsubscribe").Deserialize<UnsubscribeData>(options),
				EventHubMessageType.UnsubscribeResponse => new SubscriptionChangeResponseData
				{
					result = root.GetProperty("unsubscribeResponse").GetProperty("result").GetString() switch
					{
						"ok" => EventHubSubscriptionResult.Ok,
						"error" => EventHubSubscriptionResult.Error,
						string str => throw new JsonException($"unknown subscription response result {str}")
					},
					subscription = root.GetProperty("unsubscribeResponse").GetProperty("subscription").Deserialize<SubscriptionId>(options)
				},
				EventHubMessageType.Reconnect => root.GetProperty("reconnect").Deserialize<ReconnectData>(options),
				EventHubMessageType.Notification => root.GetProperty("notification").Deserialize<NotificationData>(options),
				_ => null
			};

			if (message.type is EventHubMessageType.SubscribeResponse or EventHubMessageType.UnsubscribeResponse)
			{
				message.parentId = root.GetProperty("parentId").GetString();
			}

			return message;
		}

		public override void Write(Utf8JsonWriter writer, EventHubMessage value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();

			writer.WriteString("id", value.id);
			writer.WriteString("type", value.type switch
			{
				EventHubMessageType.Welcome => "welcome",
				EventHubMessageType.Subscribe => "subscribe",
				EventHubMessageType.SubscribeResponse => "subscribeResponse",
				EventHubMessageType.Unsubscribe => "unsubscribe",
				EventHubMessageType.UnsubscribeResponse => "unsubscribeResponse",
				EventHubMessageType.Notification => "notification",
				EventHubMessageType.KeepAlive => "keepalive",
				EventHubMessageType.Reconnect => "reconnect",
				_ => throw new JsonException($"can't serialize unknown event type {value.type}")
			});
			writer.WriteString("timestamp", value.timestamp);

			switch (value.Data)
			{
				case WelcomeData welcome:
					writer.WritePropertyName("welcome");
					JsonSerializer.Serialize(writer, welcome, options);
					break;
				case SubscribeData subscribe:
					writer.WritePropertyName("subscribe");
					JsonSerializer.Serialize(writer, subscribe, options);
					break;
				case UnsubscribeData unsubscribe:
					writer.WritePropertyName("unsubscribe");
					JsonSerializer.Serialize(writer, unsubscribe, options);
					break;
				case SubscriptionChangeResponseData subscriptionChangeResponse:
					writer.WritePropertyName(value.type == EventHubMessageType.SubscribeResponse ? "subscribeResponse" : "unsubscribeResponse");
					JsonSerializer.Serialize(writer, subscriptionChangeResponse, options);
					break;
				case ReconnectData reconnect:
					writer.WritePropertyName("reconnect");
					JsonSerializer.Serialize(writer, reconnect, options);
					break;
				case NotificationData notification:
					writer.WritePropertyName("notification");
					JsonSerializer.Serialize(writer, notification, options);
					break;
				default:
					break;
			}

			writer.WriteEndObject();
		}
	}
}
