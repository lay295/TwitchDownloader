using System.Text.Json;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tests.ToolTests
{

	public class EventHubMessageConverterFixture
	{
		public JsonSerializerOptions Options { get; }

		public EventHubMessageConverterFixture()
		{
			Options = new JsonSerializerOptions();
			Options.AllowOutOfOrderMetadataProperties = true; // necessary, broadcast_settings_update does not start with type
			Options.Converters.Add(new EventHubMessageConverter());
		}
	}

	public class EventHubParserTests : IClassFixture<EventHubMessageConverterFixture>
	{
		private readonly EventHubMessageConverterFixture _fixture;

		public EventHubParserTests(EventHubMessageConverterFixture fixture)
		{
			_fixture = fixture;
		}

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
		public static IEnumerable<object[]> ParseDataMessages => [
			[
				"{\"welcome\":{\"keepaliveSec\":99,\"recoveryUrl\":\"wss://hermes.twitch.tv/b/v1?clientId=kimne78kx3ncx6brgo4mv6wki5h1ko&t=_pZFusBEGksnqB7bAIaGMLts_qr38QA\",\"sessionId\":\"b9457b15-ceeb-4c1f-ac47-ab321601df80\"},\"id\":\"cab7bea8-c243-4271-b259-1ad7ce41dd5a\",\"type\":\"welcome\",\"timestamp\":\"2026-01-01T12:12:12.123456789Z\"}",
				"cab7bea8-c243-4271-b259-1ad7ce41dd5a",
				EventHubMessageType.Welcome,
				"2026-01-01T12:12:12.123456789Z",
				null,
				new WelcomeData { keepaliveSec = 99, recoveryUrl = "wss://hermes.twitch.tv/b/v1?clientId=kimne78kx3ncx6brgo4mv6wki5h1ko&t=_pZFusBEGksnqB7bAIaGMLts_qr38QA", sessionId = "b9457b15-ceeb-4c1f-ac47-ab321601df80" }
			],
			[
				"{ \"type\": \"subscribe\", \"id\": \"RN2FfwtNVMLz1rDq019gb\", \"subscribe\": { \"id\": \"B8eXrEKW70eEQlKmlHwbq\", \"type\": \"pubsub\", \"pubsub\": { \"topic\": \"video-playback-by-id.0123456789\" } }, \"timestamp\": \"2026-01-01T12:12:12.123456789Z\" }",
				"RN2FfwtNVMLz1rDq019gb",
				EventHubMessageType.Subscribe,
				"2026-01-01T12:12:12.123456789Z",
				null,
				new SubscribeData { id = "B8eXrEKW70eEQlKmlHwbq", pubsub = new SubscriptionPubSub { topic = "video-playback-by-id.0123456789" } }
			],
			[
				"{ \"type\": \"unsubscribe\", \"id\": \"RN2FfwtNVmLz1rDq019gb\", \"unsubscribe\": { \"id\": \"B3eXrEKW70eEQlKmlHwbq\" }, \"timestamp\": \"2026-01-01T12:12:12.123456789Z\" }",
				"RN2FfwtNVmLz1rDq019gb",
				EventHubMessageType.Unsubscribe,
				"2026-01-01T12:12:12.123456789Z",
				null,
				new UnsubscribeData { id = "B3eXrEKW70eEQlKmlHwbq" }
			],
			[
				"{\"subscribeResponse\":{\"subscription\":{\"id\":\"B8eXrEKW70eEQlKmlHwbq\"},\"result\":\"ok\"},\"id\":\"a0702d23-cbd4-46fb-b292-12d4522a1ec2\",\"parentId\":\"RN2FfwtNVMLz1rDq019gb\",\"type\":\"subscribeResponse\",\"timestamp\":\"2026-01-01T12:12:12.123456789Z\"}",
				"a0702d23-cbd4-46fb-b292-12d4522a1ec2",
				EventHubMessageType.SubscribeResponse,
				"2026-01-01T12:12:12.123456789Z",
				"RN2FfwtNVMLz1rDq019gb",
				new SubscriptionChangeResponseData { result = EventHubSubscriptionResult.Ok, subscription = new SubscriptionId { id = "B8eXrEKW70eEQlKmlHwbq" } }
			],
			[
				"{\"unsubscribeResponse\":{\"subscription\":{\"id\":\"B8eXrEKW70eEQlKmlHwbq\"},\"result\":\"ok\"},\"id\":\"a0702d23-cbd4-46fb-b292-12d4522a1ec2\",\"parentId\":\"RN2FfwtNVMLz1rDq019gb\",\"type\":\"unsubscribeResponse\",\"timestamp\":\"2026-01-01T12:12:12.123456789Z\"}",
				"a0702d23-cbd4-46fb-b292-12d4522a1ec2",
				EventHubMessageType.UnsubscribeResponse,
				"2026-01-01T12:12:12.123456789Z",
				"RN2FfwtNVMLz1rDq019gb",
				new SubscriptionChangeResponseData { result = EventHubSubscriptionResult.Ok, subscription = new SubscriptionId { id = "B8eXrEKW70eEQlKmlHwbq" } }
			],
			[
				"{\"subscribeResponse\":{\"subscription\":{\"id\":\"B8eXrEKW70eEQlKmlHw2q\"},\"result\":\"error\",\"error\":\"invalid topic\",\"errorCode\":\"SUB002\"},\"id\":\"298a7948-9ef0-4097-a9b5-eb72b29a505b\",\"parentId\":\"RN2FfwtNVMLz1rDq0192b\",\"type\":\"subscribeResponse\",\"timestamp\":\"2026-06-06T15:05:47.066161078Z\"}",
				"298a7948-9ef0-4097-a9b5-eb72b29a505b",
				EventHubMessageType.SubscribeResponse,
				"2026-06-06T15:05:47.066161078Z",
				"RN2FfwtNVMLz1rDq0192b",
				new SubscriptionChangeResponseData { result = EventHubSubscriptionResult.Error, subscription = new SubscriptionId { id = "B8eXrEKW70eEQlKmlHw2q" } }
			],
			[
				"{\"id\":\"434e1ded-eb19-4c1b-b987-f563a43c30b3\",\"type\":\"keepalive\",\"timestamp\":\"2026-06-06T13:08:31.249576855Z\"}",
				"434e1ded-eb19-4c1b-b987-f563a43c30b3",
				EventHubMessageType.KeepAlive,
				"2026-06-06T13:08:31.249576855Z",
				null,
				null
			],
			[
				"{\"reconnect\":{\"url\":\"wss://hermes.twitch.tv/c/v1?clientId=kimne78kx3ncx6brgo4mv6wki5h1ko&t=PH8DAQEIZW52ZWxvcGUB_4AAAQMBDEVuY3J5cHRlZEtleQEKAAEKQ2lwaGVyVGV4dAEKAAECSVYBCgAAAP4Bc_\"},\"id\":\"25e70642-9cde-5a16-91d7-64cc82fcc993-msg47-topic3B8eXrEKW70eEQlKmlHwbq\",\"type\":\"reconnect\",\"timestamp\":\"2026-06-06T23:08:51.267573703Z\"}",
				"25e70642-9cde-5a16-91d7-64cc82fcc993-msg47-topic3B8eXrEKW70eEQlKmlHwbq",
				EventHubMessageType.Reconnect,
				"2026-06-06T23:08:51.267573703Z",
				null,
				new ReconnectData { url = "wss://hermes.twitch.tv/c/v1?clientId=kimne78kx3ncx6brgo4mv6wki5h1ko&t=PH8DAQEIZW52ZWxvcGUB_4AAAQMBDEVuY3J5cHRlZEtleQEKAAEKQ2lwaGVyVGV4dAEKAAECSVYBCgAAAP4Bc_" }
			]
		];

		public static IEnumerable<object[]> ParseDataNotifications => [
			[
				"{\"notification\":{\"subscription\":{\"id\":\"B8eXrEKW70eEQlKmlHwbq\"},\"type\":\"pubsub\",\"pubsub\":\"{\\\"type\\\":\\\"stream-up\\\",\\\"server_time\\\":123.456,\\\"play_delay\\\":42}\"},\"id\":\"25e70652-9cde-5a16-91d7-64cc82fcc993-msg47-topic3B8eXrEKW70eEQlKmlHwbq\",\"type\":\"notification\",\"timestamp\":\"2026-06-06T13:08:51.267573703Z\"}",
				new StreamUpData { server_time = 123.456, play_delay = 42 }
			],
			[
				"{\"notification\":{\"subscription\":{\"id\":\"B8eXrEKW70eEQlKmlHwbq\"},\"type\":\"pubsub\",\"pubsub\":\"{\\\"type\\\":\\\"stream-down\\\",\\\"server_time\\\":123.456}\"},\"id\":\"25e70652-9cde-5a16-91d7-64cc82fcc993-msg47-topic3B8eXrEKW70eEQlKmlHwbq\",\"type\":\"notification\",\"timestamp\":\"2026-06-06T13:08:51.267573703Z\"}",
				new StreamDownData { server_time = 123.456 }
			],
			[
				"{\"notification\":{\"subscription\":{\"id\":\"B8eXrEKW70eEQlKmlHwbq\"},\"type\":\"pubsub\",\"pubsub\":\"{\\\"type\\\":\\\"viewcount\\\",\\\"server_time\\\":123.456,\\\"viewers\\\":196,\\\"collaboration_status\\\":\\\"none\\\",\\\"collaboration_viewers\\\":0,\\\"costream_status\\\":\\\"\\\",\\\"costream_viewers\\\":0}\"},\"id\":\"25e70652-9cde-5a16-91d7-64cc82fcc993-msg47-topic3B8eXrEKW70eEQlKmlHwbq\",\"type\":\"notification\",\"timestamp\":\"2026-06-06T13:08:51.267573703Z\"}",
				new ViewCountData { server_time = 123.456, viewers = 196, collaboration_status = "none", collaboration_viewers = 0, costream_status = "", costream_viewers = 0 }
			],
			[
				"{\"notification\":{\"subscription\":{\"id\":\"B8eXrEKW70eEQlKmlHwbq\"},\"type\":\"pubsub\",\"pubsub\":\"{\\\"type\\\":\\\"commercial\\\",\\\"server_time\\\":123.456,\\\"length\\\":42,\\\"scheduled\\\":true}\"},\"id\":\"25e70652-9cde-5a16-91d7-64cc82fcc993-msg47-topic3B8eXrEKW70eEQlKmlHwbq\",\"type\":\"notification\",\"timestamp\":\"2026-06-06T13:08:51.267573703Z\"}",
				new CommercialData { length = 42, scheduled = true, server_time = 123.456 }
			],
			[
				"{\"notification\":{\"subscription\":{\"id\":\"B8eXrEKW70eEQlKmlHwbq\"},\"type\":\"pubsub\",\"pubsub\":\"{\\\"channel_id\\\":\\\"12345677\\\",\\\"type\\\":\\\"broadcast_settings_update\\\",\\\"channel\\\":\\\"testchannel\\\",\\\"old_status\\\":\\\"oldstat\\\",\\\"status\\\":\\\"stat\\\",\\\"old_game_id\\\":33,\\\"game_id\\\":22,\\\"old_game\\\":\\\"oGame\\\",\\\"game\\\":\\\"gaem\\\"}\"},\"id\":\"25e70652-9cde-5a16-91d7-64cc82fcc993-msg47-topic3B8eXrEKW70eEQlKmlHwbq\",\"type\":\"notification\",\"timestamp\":\"2026-06-06T13:08:51.267573703Z\"}",
				new BroadcastSettingsUpdateData { channel = "testchannel", channel_id = "12345677", old_status = "oldstat", status = "stat", old_game_id = 33, game_id = 22, old_game = "oGame", game = "gaem" }
			],
		];
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.


		[Theory]
		[MemberData(nameof(ParseDataMessages))]
		public void CorrectlyParsesMessage(string rawMessage, string messageId, EventHubMessageType type, DateTime timestamp, string parentId, EventHubMessageData expectedData)
		{
			var message = JsonSerializer.Deserialize<EventHubMessage>(rawMessage, _fixture.Options);

			Assert.NotNull(message);
			Assert.Equal(messageId, message.id);
			Assert.Equal(type, message.type);
			Assert.Equal(timestamp.ToUniversalTime(), message.timestamp, TimeSpan.FromTicks(1));
			Assert.Equal(parentId, message.parentId);


			switch (type)
			{
				case EventHubMessageType.Welcome:
					Assert.IsType<WelcomeData>(message.Data);
					var welcomeExpected = (WelcomeData)expectedData;
					var welcomeData = (WelcomeData)message.Data;
					Assert.Equal(welcomeExpected.keepaliveSec, welcomeData.keepaliveSec);
					Assert.Equal(welcomeExpected.recoveryUrl, welcomeData.recoveryUrl);
					Assert.Equal(welcomeExpected.sessionId, welcomeData.sessionId);
					break;
				case EventHubMessageType.Subscribe:
					Assert.IsType<SubscribeData>(message.Data);
					var subscribeExpected = (SubscribeData)expectedData;
					var subscribeData = (SubscribeData)message.Data;
					Assert.Equal(subscribeExpected.id, subscribeData.id);
					Assert.Equal(subscribeExpected.pubsub.topic, subscribeData.pubsub.topic);
					break;
				case EventHubMessageType.Unsubscribe:
					Assert.IsType<UnsubscribeData>(message.Data);
					var unsubscribeExpected = (UnsubscribeData)expectedData;
					var unsubscribeData = (UnsubscribeData)message.Data;
					Assert.Equal(unsubscribeExpected.id, unsubscribeData.id);
					break;
				case EventHubMessageType.UnsubscribeResponse:
				case EventHubMessageType.SubscribeResponse:
					Assert.IsType<SubscriptionChangeResponseData>(message.Data);
					var subscribeResponseExpected = (SubscriptionChangeResponseData)expectedData;
					var subscribeResponseData = (SubscriptionChangeResponseData)message.Data;
					Assert.Equal(subscribeResponseExpected.result, subscribeResponseData.result);
					Assert.Equal(subscribeResponseExpected.subscription.id, subscribeResponseData.subscription.id);
					break;
				case EventHubMessageType.KeepAlive:
					Assert.Null(message.Data);
					break;
				case EventHubMessageType.Reconnect:
					Assert.IsType<ReconnectData>(message.Data);
					var reconnectExpected = (ReconnectData)expectedData;
					var reconnectData = (ReconnectData)message.Data;
					Assert.Equal(reconnectExpected.url, reconnectData.url);
					break;
				case EventHubMessageType.Unknown:
					Assert.Fail("this case shouldn't be reached");
					break;
			}
		}

		[Theory]
		[MemberData(nameof(ParseDataNotifications))]
		public void CorrectlyParsesNotifications(string rawMessage, PubSubData expectedData)
		{
			var message = JsonSerializer.Deserialize<EventHubMessage>(rawMessage, _fixture.Options);

			Assert.NotNull(message);
			Assert.IsType<NotificationData>(message.Data);

			switch (((NotificationData)message.Data).pubsub)
			{
				case StreamUpData streamUp:
					StreamUpData expectedStreamUp = (StreamUpData)expectedData;
					Assert.Equal(expectedStreamUp.server_time, streamUp.server_time);
					Assert.Equal(expectedStreamUp.play_delay, streamUp.play_delay);
					break;
				case StreamDownData streamDown:
					StreamDownData expectedStreamDown = (StreamDownData)expectedData;
					Assert.Equal(expectedStreamDown.server_time, streamDown.server_time);
					break;
				case ViewCountData viewCount:
					ViewCountData expectedViewCount = (ViewCountData)expectedData;
					Assert.Equal(expectedViewCount.server_time, viewCount.server_time);
					Assert.Equal(expectedViewCount.viewers, viewCount.viewers);
					Assert.Equal(expectedViewCount.collaboration_status, viewCount.collaboration_status);
					Assert.Equal(expectedViewCount.collaboration_viewers, viewCount.collaboration_viewers);
					Assert.Equal(expectedViewCount.costream_status, viewCount.costream_status);
					Assert.Equal(expectedViewCount.collaboration_viewers, viewCount.collaboration_viewers);
					break;
				case CommercialData commercial:
					CommercialData expectedCommercial = (CommercialData)expectedData;
					Assert.Equal(expectedCommercial.server_time, commercial.server_time);
					Assert.Equal(expectedCommercial.length, commercial.length);
					Assert.Equal(expectedCommercial.scheduled, commercial.scheduled);
					break;
				case BroadcastSettingsUpdateData broadcastSettings:
					BroadcastSettingsUpdateData expectedBroadcast = (BroadcastSettingsUpdateData)expectedData;
					Assert.Equal(expectedBroadcast.channel_id, broadcastSettings.channel_id);
					Assert.Equal(expectedBroadcast.channel, broadcastSettings.channel);
					Assert.Equal(expectedBroadcast.old_status, broadcastSettings.old_status);
					Assert.Equal(expectedBroadcast.status, broadcastSettings.status);
					Assert.Equal(expectedBroadcast.old_game_id, broadcastSettings.old_game_id);
					Assert.Equal(expectedBroadcast.game_id, broadcastSettings.game_id);
					Assert.Equal(expectedBroadcast.old_game, broadcastSettings.old_game);
					Assert.Equal(expectedBroadcast.game, broadcastSettings.game);
					break;
				default:
					Assert.Fail("unknown PubSub subtype");
					break;
			}
		}


		// un/subscriptions are really the only thing this tool should ever need to send, so for now it should be fine to only test those
		[Fact]
		public void CorrectlySerializeEventHubMessages()
		{
			// ============== SUBSCRIPTION 

			var subRequest = new EventHubMessage
			{
				id = "testid",
				type = EventHubMessageType.Subscribe,
				timestamp = DateTime.UnixEpoch,
				Data = new SubscribeData
				{
					id = "subid",
					pubsub = new SubscriptionPubSub
					{
						topic = "testtopic"
					}
				}
			};

			const string EXPECTED_SUB = "{\"id\":\"testid\",\"type\":\"subscribe\",\"timestamp\":\"1970-01-01T00:00:00Z\",\"subscribe\":{\"id\":\"subid\",\"type\":\"pubsub\",\"pubsub\":{\"topic\":\"testtopic\"}}}";

			Assert.Equal(EXPECTED_SUB, JsonSerializer.Serialize(subRequest, _fixture.Options));

			// ============== UNSUBSCRIPTION 

			var unsubRequest = new EventHubMessage
			{
				id = "testid",
				type = EventHubMessageType.Unsubscribe,
				timestamp = DateTime.UnixEpoch,
				Data = new UnsubscribeData
				{
					id = "subid"
				}
			};

			const string EXPECTED_UNSUB = "{\"id\":\"testid\",\"type\":\"unsubscribe\",\"timestamp\":\"1970-01-01T00:00:00Z\",\"unsubscribe\":{\"id\":\"subid\"}}";

			Assert.Equal(EXPECTED_UNSUB, JsonSerializer.Serialize(unsubRequest, _fixture.Options));
		}
	}
}