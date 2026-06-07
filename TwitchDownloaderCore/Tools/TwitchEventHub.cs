using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tools
{
	public sealed class TwitchEventHub : IDisposable
	{
		private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { Converters = { new EventHubMessageConverter() } };

		private readonly ITaskLogger _logger;
		private readonly CancellationTokenSource _endConnection = new CancellationTokenSource();

		private readonly Channel<string> _subscriptionRequestsWaitingForSending = Channel.CreateUnbounded<string>();
		private readonly ConcurrentDictionary<string, TaskCompletionSource<SubscribeResponseData>> _subscriptionResponseHandlers = new();

		private readonly Channel<EventHubMessage> _notifications = Channel.CreateUnbounded<EventHubMessage>();
		public ChannelReader<EventHubMessage> Notifications { get => _notifications.Reader; }

		public TwitchEventHub(ITaskLogger logger)
		{
			_logger = logger;
			_ = RunConnectionLoop();
		}

		public Task<SubscribeResponseData> Subscribe(TwitchChatEvent evt, string streamerId)
		{
			string topic;
			switch (evt)
			{
				case TwitchChatEvent.VideoPlaybackById:
					topic = $"video-playback-by-id.{streamerId}";
					break;
				default:
					throw new ArgumentException($"subscription for that event not yet supported: {evt}", nameof(evt));
			}

			var messageId = GenerateNanoId();
			var subscribeId = GenerateNanoId();

			EventHubMessage request = new EventHubMessage
			{
				id = messageId,
				type = EventHubMessageType.Subscribe,
				timestamp = DateTime.UtcNow,
				Data = new SubscribeData
				{
					id = subscribeId,
					pubsub = new SubscriptionPubSub { topic = topic }
				}
			};

			var message = JsonSerializer.Serialize(request, _jsonSerializerOptions);
			var subscriptionTaskSource = new TaskCompletionSource<SubscribeResponseData>();
			_subscriptionResponseHandlers.TryAdd(subscribeId, subscriptionTaskSource);
			_subscriptionRequestsWaitingForSending.Writer.TryWrite(message);

			return subscriptionTaskSource.Task;
		}

		private async Task RunConnectionLoop()
		{
			try
			{
				// this essentially makes the _endConnection CancellationToken awaitable
				var endConnectionTaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				var endConnectionTask = endConnectionTaskCompletionSource.Task;
				using var reg = _endConnection.Token.Register(() => endConnectionTaskCompletionSource.TrySetResult());

				while (!_endConnection.IsCancellationRequested)
				{
					var reconnectionRequired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

					using var websocket = new EventingWebSocket(_logger);

					// register message handler
					EventHandler<EventingWebSocket.Message> eventHandler = (object sender, EventingWebSocket.Message msg) => OnMessageReceived(sender, msg, reconnectionRequired);
					websocket.MessageReceived += eventHandler;

					// connect to the underlying websocket
					await websocket.ConnectAsync(new Uri("wss://hermes.twitch.tv/v1?clientId=kimne78kx3ncx6brgo4mv6wki5h1ko"), _endConnection.Token);

					// register subscription message handler
					using var subscriptionProcessingCancellationSource = new CancellationTokenSource();
					var subscriptionProcessingTask = ProcessSubscriptionRequests(websocket, subscriptionProcessingCancellationSource.Token);

					// TODO: restore subscriptions if no recoveryUrl was used.

					// wait until either the end of the connection is desired or a reconnection is needed
					// reconnection might be needed if: the underlying websocket closes for some reason, the subscription processing errors out
					var completedTask = await Task.WhenAny(reconnectionRequired.Task, subscriptionProcessingTask, endConnectionTask);

					// ensure that the subscription processing ends cleanly before websocket is closed
					// this is important for the recoveryUrl to truly give all subscriptions that are processed
					subscriptionProcessingCancellationSource.Cancel();
					await subscriptionProcessingTask;

					// remove message handler and close connection
					websocket.MessageReceived -= eventHandler;
					await websocket.CloseAsync();
				}
			}
			catch (OperationCanceledException)
			{
				// should really be okay
			}
			catch (Exception ex)
			{
				_logger.LogError($"TwitchEventHub failed unexpectedly: {ex.Message}");
				Console.WriteLine(ex);
				_notifications.Writer.TryComplete(ex);
			}
		}

		private void OnMessageReceived(object sender, EventingWebSocket.Message msgEvent, TaskCompletionSource reconnectRequired)
		{
			switch (msgEvent.MessageType)
			{
				case WebSocketMessageType.Binary:
					_logger.LogWarning("Binary messages are not supported. See verbose log for more info.");
					_logger.LogVerbose($"Binary message content: {Convert.ToBase64String(msgEvent.Buffer)}");
					return;
				case WebSocketMessageType.Close:
					// this can happen when legitimatly closing after finishing to collect all data.
					// Still, if something else triggers it, we need to reconnect, otherwise the stopListening check will catch it
					reconnectRequired.TrySetResult();
					return;
			}

			var message = JsonSerializer.Deserialize<EventHubMessage>(msgEvent.Buffer, _jsonSerializerOptions);
			_logger.LogVerbose($"event message received {message.Data?.GetType()}");

			switch (message.Data)
			{
				case WelcomeData welcomeData:
					// TODO: handle welcome
					break;
				case SubscribeResponseData subscribeResponse:
					// for expected subscriptions, complete the subscription task instead of calling the regular message handler
					if (_subscriptionResponseHandlers.TryRemove(subscribeResponse.subscription.id, out var subscriptionTaskSource))
					{
						subscriptionTaskSource.SetResult(subscribeResponse);
					}
					break;
				case null:
					// TODO: keepalive handling
					break;
				case NotificationData:
					_notifications.Writer.TryWrite(message);
					break;
			}
		}

		private async Task ProcessSubscriptionRequests(EventingWebSocket socket, CancellationToken cancellationToken)
		{
			try
			{
				await foreach (var subrequest in _subscriptionRequestsWaitingForSending.Reader.ReadAllAsync(cancellationToken))
				{
					_logger.LogVerbose($"send sub request {subrequest}");
					// can't be interrupted as the subrequest has already been removed from the channel and now needs to be processed.
					await socket.SendTextPooledAsync(subrequest, CancellationToken.None);
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				_logger.LogError($"sending subscriptions was interrupted unexpectedly: {ex.GetType()}: {ex.Message}");
			}
		}

		private string GenerateNanoId()
		{
			const string ID_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";

			char[] result = new char[21];

			for (int i = 0; i < result.Length; i++)
			{
				result[i] = ID_CHARS[RandomNumberGenerator.GetInt32(ID_CHARS.Length)];
			}

			return new string(result);
		}

		public void Dispose()
		{
			try
			{
				_subscriptionRequestsWaitingForSending.Writer.TryComplete();
				_notifications.Writer.TryComplete();
				_endConnection.Cancel();
			}
			finally
			{
				_endConnection.Dispose();
			}
		}

		public enum TwitchChatEvent
		{
			VideoPlaybackById,
			ChannelSubGifts,
			BroadcastSettingsUpdate
		}

		public struct Subscription
		{
			public readonly TwitchChatEvent EventType;
			public readonly string SubscriptionKey;
			public string SubscriptionId;
		}
	}
}