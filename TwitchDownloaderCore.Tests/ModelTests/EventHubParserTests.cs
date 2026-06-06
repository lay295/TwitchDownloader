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
		public static IEnumerable<object[]> ParseData => [
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
				"{\"subscribeResponse\":{\"subscription\":{\"id\":\"B8eXrEKW70eEQlKmlHwbq\"},\"result\":\"ok\"},\"id\":\"a0702d23-cbd4-46fb-b292-12d4522a1ec2\",\"parentId\":\"RN2FfwtNVMLz1rDq019gb\",\"type\":\"subscribeResponse\",\"timestamp\":\"2026-01-01T12:12:12.123456789Z\"}",
				"a0702d23-cbd4-46fb-b292-12d4522a1ec2",
				EventHubMessageType.SubscribeResponse,
				"2026-01-01T12:12:12.123456789Z",
				"RN2FfwtNVMLz1rDq019gb",
				new SubscribeResponseData { result = EventHubSubscriptionResult.Ok, subscription = new SubscriptionId { id = "B8eXrEKW70eEQlKmlHwbq" } }
			],
			[
				"{\"subscribeResponse\":{\"subscription\":{\"id\":\"B8eXrEKW70eEQlKmlHw2q\"},\"result\":\"error\",\"error\":\"invalid topic\",\"errorCode\":\"SUB002\"},\"id\":\"298a7948-9ef0-4097-a9b5-eb72b29a505b\",\"parentId\":\"RN2FfwtNVMLz1rDq0192b\",\"type\":\"subscribeResponse\",\"timestamp\":\"2026-06-06T15:05:47.066161078Z\"}",
				"298a7948-9ef0-4097-a9b5-eb72b29a505b",
				EventHubMessageType.SubscribeResponse,
				"2026-06-06T15:05:47.066161078Z",
				"RN2FfwtNVMLz1rDq0192b",
				new SubscribeResponseData { result = EventHubSubscriptionResult.Error, subscription = new SubscriptionId { id = "B8eXrEKW70eEQlKmlHw2q" } }
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
				"{\"notification\":{\"subscription\":{\"id\":\"B8eXrEKW70eEQlKmlHwbq\"},\"type\":\"pubsub\",\"pubsub\":\"{\\\"type\\\":\\\"viewcount\\\",\\\"server_time\\\":1780751330.723397,\\\"viewers\\\":196,\\\"collaboration_status\\\":\\\"none\\\",\\\"collaboration_viewers\\\":0,\\\"costream_status\\\":\\\"\\\",\\\"costream_viewers\\\":0}\"},\"id\":\"25e70652-9cde-5a16-91d7-64cc82fcc993-msg47-topic3B8eXrEKW70eEQlKmlHwbq\",\"type\":\"notification\",\"timestamp\":\"2026-06-06T13:08:51.267573703Z\"}",
				"25e70652-9cde-5a16-91d7-64cc82fcc993-msg47-topic3B8eXrEKW70eEQlKmlHwbq",
				EventHubMessageType.Notification,
				"2026-06-06T13:08:51.267573703Z",
				null,
				new NotificationData { subscription = new SubscriptionId { id = "B8eXrEKW70eEQlKmlHwbq" }, pubsub = "{\"type\":\"viewcount\",\"server_time\":1780751330.723397,\"viewers\":196,\"collaboration_status\":\"none\",\"collaboration_viewers\":0,\"costream_status\":\"\",\"costream_viewers\":0}" }
			]
		];
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.


		[Theory]
		[MemberData(nameof(ParseData))]
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
				case EventHubMessageType.SubscribeResponse:
					Assert.IsType<SubscribeResponseData>(message.Data);
					var subscribeResponseExpected = (SubscribeResponseData)expectedData;
					var subscribeResponseData = (SubscribeResponseData)message.Data;
					Assert.Equal(subscribeResponseExpected.result, subscribeResponseData.result);
					Assert.Equal(subscribeResponseExpected.subscription.id, subscribeResponseData.subscription.id);
					break;
				case EventHubMessageType.KeepAlive:
					Assert.Null(message.Data);
					break;
				case EventHubMessageType.Notification:
					Assert.IsType<NotificationData>(message.Data);
					var notificationExpected = (NotificationData)expectedData;
					var notificationData = (NotificationData)message.Data;
					Assert.Equal(notificationExpected.type, notificationData.type);
					Assert.Equal(notificationExpected.subscription.id, notificationData.subscription.id);
					Assert.Equal(notificationExpected.pubsub, notificationData.pubsub);
					break;
				case EventHubMessageType.Unknown:
					Assert.Fail("this case shouldn't be reached");
					break;
			}
		}


		// subscriptions are really the only thing this tool shjould ever need to send, so for now it should be fine to only test those
		[Fact]
		public void CorrectlySerializeEventHubMessages()
		{
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

			const string EXPECTED = "{\"id\":\"testid\",\"type\":\"subscribe\",\"timestamp\":\"1970-01-01T00:00:00Z\",\"subscribe\":{\"id\":\"subid\",\"type\":\"pubsub\",\"pubsub\":{\"topic\":\"testtopic\"}}}";

			Assert.Equal(EXPECTED, JsonSerializer.Serialize(subRequest, _fixture.Options));
		}
	}
}