using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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

		public class SubscriptionGroup : IDisposable
		{
			internal readonly Channel<EventHubMessage> _channel;
			private readonly TwitchChatEvent[] _events;
			public ChannelReader<EventHubMessage> Messages { get => _channel.Reader; }

			internal SubscriptionGroup(Channel<EventHubMessage> channel, TwitchChatEvent[] events)
			{
				_channel = channel;
				_events = events;
			}

			public void Dispose()
			{
				_channel.Writer.TryComplete();
				foreach (var evt in _events)
				{
					// TODO: unsubscribe
				}
			}
		}

		private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { Converters = { new EventHubMessageConverter() } };

		private readonly ITaskLogger _logger;
		// internally used to signal that the connection loop should end
		private readonly CancellationTokenSource _endConnection = new CancellationTokenSource();

		// stores the subscription messages that are handled by the subscription loop
		private readonly Channel<EventHubMessage> _subscriptionRequestsWaitingForSending = Channel.CreateUnbounded<EventHubMessage>();
		// stores currently queued subscriptions so that their success/failure can be signaled
		private readonly ConcurrentDictionary<string, TaskCompletionSource<SubscribeResponseData>> _subscriptionResponseHandlers = new();

		// allows mapping a TwitchChatEvent to a subId if such a subscription is already ongoing
		private readonly ConcurrentDictionary<TwitchChatEvent, string> _subscriptionIds = new();
		// all currently ongoing listeners for each subscription
		private readonly ConcurrentDictionary<string, List<SubscriptionGroup>> _notificationChannels = new();

		// this is set when receiving a welcome message and gets cleaned up before using it to recover a connection
		private string _recoveryUrl;
		private TimeSpan _keepAliveSec = TimeSpan.Zero;
		private DateTimeOffset _lastMessageReceived;

		public TwitchEventHub(ITaskLogger logger)
		{
			_logger = logger;
			_ = RunConnectionLoop();
		}

		public async Task<SubscriptionGroup> SubscribeTo(string streamerId, TwitchChatEvent[] events)
		{
			var subChannel = Channel.CreateUnbounded<EventHubMessage>();

			var subGroup = new SubscriptionGroup(subChannel, events);

			var subTasks = events.Select(async evt =>
			{
				// ensure that there is a subscription ongoing
				if (!_subscriptionIds.ContainsKey(evt))
				{
					await Subscribe(evt, streamerId);
				}

				_subscriptionIds.TryGetValue(evt, out var subId);

				var listenerList = _notificationChannels.GetOrAdd(subId, new List<SubscriptionGroup>());

				listenerList.Add(subGroup);
			});

			try
			{
				Task.WaitAll(subTasks);
			}
			catch
			{
				subGroup.Dispose();
				throw;
			}


			return subGroup;
		}

		/// <summary>
		/// send a subscription request and wait for the response, throws if not sucessful<br/>
		/// handles adding the id to _subscriptionIds in case of success
		/// </summary>
		/// <param name="evt">the name of the event</param>
		/// <param name="streamerId">the id (not login or displayname) of the streamer</param>
		private async Task Subscribe(TwitchChatEvent evt, string streamerId)
		{
			const int SUBSCRIPTION_TIMEOUT_SECS = 20;

			var subscribeId = GenerateNanoId();
			EventHubMessage request = CreateSubscriptionMessage(evt, streamerId, subscribeId);

			var subscriptionTaskSource = new TaskCompletionSource<SubscribeResponseData>();
			_subscriptionResponseHandlers.TryAdd(subscribeId, subscriptionTaskSource);

			_subscriptionRequestsWaitingForSending.Writer.TryWrite(request);

			try
			{
				var subResponse = await subscriptionTaskSource.Task.WaitAsync(TimeSpan.FromSeconds(SUBSCRIPTION_TIMEOUT_SECS));

				if (subResponse.result == EventHubSubscriptionResult.Error)
				{
					throw new HttpRequestException("the subscription was rejected");
				}

				_subscriptionIds.TryAdd(evt, subResponse.subscription.id);
			}
			catch
			{
				// if sending failed, this is already set to the exception, but timeout does not complete task
				subscriptionTaskSource.TrySetCanceled();
				throw;
			}
			finally
			{
				_subscriptionResponseHandlers.TryRemove(subscribeId, out _);
			}
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
					var uri = new Uri(_recoveryUrl ?? "wss://hermes.twitch.tv/v1?clientId=kimne78kx3ncx6brgo4mv6wki5h1ko");
					_recoveryUrl = null;
					// TODO: retry connect as in irc client
					await websocket.ConnectAsync(uri, _endConnection.Token);

					// setup handling loops
					using var supportLoopCancellationSource = new CancellationTokenSource();
					var subscriptionProcessingTask = ProcessSubscriptionRequests(websocket, supportLoopCancellationSource.Token);
					var monitoringKeepaliveTask = MonitorConnectionKeepalive(reconnectionRequired, supportLoopCancellationSource.Token);

					// wait until either the end of the connection is desired or a reconnection is needed
					// reconnection might be needed if: the underlying websocket closes for some reason, the subscription processing errors out
					var completedTask = await Task.WhenAny(reconnectionRequired.Task, subscriptionProcessingTask, monitoringKeepaliveTask, endConnectionTask);

					// ensure that the subscription processing ends cleanly before websocket is closed
					// this is important for the recoveryUrl to truly give all subscriptions that are processed
					supportLoopCancellationSource.Cancel();
					await subscriptionProcessingTask;

					// remove message handler and clean up connection state
					websocket.MessageReceived -= eventHandler;
					_keepAliveSec = TimeSpan.Zero;
					_lastMessageReceived = default;

					if (_endConnection.IsCancellationRequested)
					{
						await websocket.CloseAsync();
					}
					else
					{
						/*
						we need to use the recoveryUrl, which gets invalidated upon normal close
						the connection for the recovery url would be instant refused 
						in my tests there usually weren't any messages being lost, but technically there could be
						messages arriving in between deregistering the message handler and connecting with the new websocket
						*/
					}
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

				foreach (var subscriptionListeners in _notificationChannels)
				{
					// for subgroups with n subscriptions this will be called for each underlying subscription but the operation is idempotent
					subscriptionListeners.Value.ForEach(subGroup => subGroup._channel.Writer.TryComplete(ex));
				}
			}
		}

		private void OnMessageReceived(object sender, EventingWebSocket.Message msgEvent, TaskCompletionSource reconnectRequired)
		{
			_lastMessageReceived = DateTimeOffset.UtcNow;

			switch (msgEvent.MessageType)
			{
				case WebSocketMessageType.Binary:
					_logger.LogWarning("Binary messages are not supported. See verbose log for more info.");
					_logger.LogVerbose($"Binary message content: {Convert.ToBase64String(msgEvent.Buffer)}");
					return;
				case WebSocketMessageType.Close:
					// this can happen when legitimatly closing after finishing to collect all data.
					// Still, if something else triggers it, we need to reconnect, otherwise the stopListening check will catch it
					_logger.LogVerbose($"received close request from underlying websocket");
					reconnectRequired.TrySetResult();
					return;
			}

			var message = JsonSerializer.Deserialize<EventHubMessage>(msgEvent.Buffer, _jsonSerializerOptions);

			switch (message.Data)
			{
				case WelcomeData welcomeData:
					_logger.LogVerbose($"welcome message received: [{welcomeData.sessionId}] {welcomeData.keepaliveSec}s");
					_recoveryUrl = welcomeData.recoveryUrl;
					_keepAliveSec = TimeSpan.FromSeconds(welcomeData.keepaliveSec);
					break;
				case SubscribeResponseData subscribeResponse:
					_logger.LogVerbose($"subscribe response: {subscribeResponse.result} -> {subscribeResponse.subscription.id}");
					// for expected subscriptions, complete the subscription task instead of calling the regular message handler
					if (_subscriptionResponseHandlers.TryRemove(subscribeResponse.subscription.id, out var subscriptionTaskSource))
					{
						subscriptionTaskSource.SetResult(subscribeResponse);
					}
					else
					{
						_logger.LogWarning($"received subscription response for unknown subscription request {subscribeResponse.subscription.id} ({subscribeResponse.result})");
					}
					break;
				case null:
					// keepalive message, whose only purpose it is to update _lastMessageReceived
					break;
				case NotificationData notifData:
					if (!_notificationChannels.TryGetValue(notifData.subscription.id, out var subGroups))
					{
						_logger.LogWarning($"no subGroups found for {notifData.subscription.id}.");
						_logger.LogWarning($"available subscriptions: {_notificationChannels.Keys}");
						return;
					}

					foreach (var subGroup in subGroups)
					{
						subGroup._channel.Writer.TryWrite(message);
					}
					break;
			}
		}

		private async Task MonitorConnectionKeepalive(TaskCompletionSource reconnectRequired, CancellationToken cancellationToken)
		{
			try
			{
				while (!cancellationToken.IsCancellationRequested)
				{
					if (_keepAliveSec == TimeSpan.Zero)
					{
						// welcome mesage not yet received
						await Task.Delay(1000, cancellationToken);
						continue;
					}

					var nextDeadline = _lastMessageReceived + _keepAliveSec;
					var delay = nextDeadline - DateTimeOffset.UtcNow;

					if (delay < TimeSpan.Zero)
					{
						_logger.LogVerbose("keepalive failure, requiring reconnect");
						reconnectRequired.TrySetResult();
						return;
					}

					await Task.Delay(delay, cancellationToken);
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex)
			{
				_logger.LogError($"monitoring the connection failed unexpectedly {ex.GetType()} {ex.Message}");
			}
		}

		private async Task ProcessSubscriptionRequests(EventingWebSocket socket, CancellationToken cancellationToken)
		{
			try
			{
				await foreach (var subrequest in _subscriptionRequestsWaitingForSending.Reader.ReadAllAsync(cancellationToken))
				{
					_logger.LogVerbose($"send sub request {subrequest}");
					try
					{
						var message = JsonSerializer.Serialize(subrequest, _jsonSerializerOptions);
						// can't be interrupted as the subrequest has already been removed from the channel and now needs to be processed.
						// if sending fails, then this subscription will just fail
						await socket.SendTextPooledAsync(message, CancellationToken.None);
					}
					catch (Exception ex)
					{
						if (_subscriptionResponseHandlers.TryRemove(((SubscribeData)subrequest.Data).id, out var subscriptionTaskSource))
						{
							subscriptionTaskSource.SetException(ex);
						}
					}
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

		private EventHubMessage CreateSubscriptionMessage(TwitchChatEvent evt, string streamerId, string subscribeId)
		{
			string topic = evt switch
			{
				TwitchChatEvent.VideoPlaybackById => $"video-playback-by-id.{streamerId}",
				_ => throw new NotImplementedException(),
			};

			return new EventHubMessage
			{
				id = GenerateNanoId(),
				type = EventHubMessageType.Subscribe,
				timestamp = DateTime.UtcNow,
				Data = new SubscribeData
				{
					id = subscribeId,
					pubsub = new SubscriptionPubSub { topic = topic }
				}
			};
		}

		public void Dispose()
		{
			try
			{
				_subscriptionRequestsWaitingForSending.Writer.TryComplete();
				foreach (var subscriptionListeners in _notificationChannels)
				{
					// for subgroups with n subscriptions this will be called for each underlying subscription but the operation is idempotent
					subscriptionListeners.Value.ForEach(subGroup => subGroup._channel.Writer.TryComplete());
				}
				// this ends the connection loop and cleans up the underlying websocket
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