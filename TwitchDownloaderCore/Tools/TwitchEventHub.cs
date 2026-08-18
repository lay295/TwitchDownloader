using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tools
{
	// TODO: there is an issue where if you leave the scope of a subGroup and it triggers an unsubscription
	// and then create a new SubGroup for the same topic it will go through with the unsubscription but
	// the new subGroup thinks it is valid
	public sealed class TwitchEventHub : IDisposable
	{

		public class SubscriptionGroup : IDisposable
		{
			private readonly TwitchEventHub _hub;
			internal readonly Channel<EventHubMessage> _channel;
			private readonly string[] _topics;
			public ChannelReader<EventHubMessage> Messages { get => _channel.Reader; }

			internal SubscriptionGroup(TwitchEventHub hub, Channel<EventHubMessage> channel, string[] topics)
			{
				_hub = hub;
				_channel = channel;
				_topics = topics;
			}

			public void Dispose()
			{
				_channel.Writer.TryComplete();
				foreach (var topic in _topics)
				{
					if (!_hub._subscriptionIds.TryGetValue(topic, out var subId))
					{
						_hub._logger.LogWarning($"tried to remove subscription for a topic that has no subscriptions: {topic}");
						continue;
					}

					if (!_hub._notificationChannels.TryGetValue(subId, out var subGroups))
					{
						_hub._logger.LogWarning($"tried to remove subGroup for a subscription that has no subGroups: {subId}");
						continue;
					}

					if (!subGroups.Remove(this))
					{
						_hub._logger.LogWarning($"subGroup is not a known listener for the subscription: {subId}");
						continue;
					}

					// if this is the last listener for that subscription, clean up the subscription and unsubscribe
					if (subGroups.Count < 1)
					{
						_hub._notificationChannels.TryRemove(subId, out _);
						try
						{
							// we do not await unsubscriptions, they are fire and forget to keep the unsubscribe/dispose simple
							_ = _hub.Unsubscribe(topic);
						}
						catch { }
					}
				}
			}
		}

		private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { Converters = { new EventHubMessageConverter() } };

		private readonly ITaskLogger _logger;
		// internally used to signal that the connection loop should end
		private readonly CancellationTokenSource _endConnection = new CancellationTokenSource();

		// stores the subscription messages that are handled by the subscription loop
		private readonly Channel<EventHubMessage> _messagesWaitingForSending = Channel.CreateUnbounded<EventHubMessage>();
		// stores currently queued subscriptions so that their success/failure can be signaled
		private readonly ConcurrentDictionary<string, TaskCompletionSource<SubscriptionChangeResponseData>> _subscriptionResponseHandlers = new();
		// stores currently queued unsubscriptions so that their success/failure can be signaled
		private readonly ConcurrentDictionary<string, TaskCompletionSource<SubscriptionChangeResponseData>> _unsubscriptionResponseHandlers = new();

		// allows mapping a topic to a subId if such a subscription is already ongoing
		private readonly ConcurrentDictionary<string, string> _subscriptionIds = new();
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

			var topics = events.Select(evt => GetTopic(evt, streamerId));

			var subGroup = new SubscriptionGroup(this, subChannel, topics.ToArray());

			var subTasks = topics.Select(async topic =>
			{
				// ensure that there is a subscription ongoing
				if (!_subscriptionIds.ContainsKey(topic))
				{
					await Subscribe(topic);
				}

				_subscriptionIds.TryGetValue(topic, out var subId);

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
		/// <param name="topic">the combined string of the event name and streamer used for identifying what to subscribe to</param>
		private async Task Subscribe(string topic)
		{
			const int SUBSCRIPTION_TIMEOUT_SECS = 20;

			var subscribeId = GenerateNanoId();
			EventHubMessage request = CreateSubscriptionMessage(topic, subscribeId);

			var subscriptionTaskSource = new TaskCompletionSource<SubscriptionChangeResponseData>();
			_subscriptionResponseHandlers.TryAdd(subscribeId, subscriptionTaskSource);

			_messagesWaitingForSending.Writer.TryWrite(request);

			try
			{
				var subResponse = await subscriptionTaskSource.Task.WaitAsync(TimeSpan.FromSeconds(SUBSCRIPTION_TIMEOUT_SECS));

				if (subResponse.result == EventHubSubscriptionResult.Error)
				{
					throw new HttpRequestException("the subscription was rejected");
				}

				_subscriptionIds.TryAdd(topic, subResponse.subscription.id);
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

		/// <summary>
		/// send an unsubscription request and wait for the response, throws if not sucessful<br/>
		/// handles removing the id from _subscriptionIds in case of success
		/// </summary>
		/// <param name="evt">the name of the event</param>
		private async Task Unsubscribe(string topic)
		{
			const int TIMEOUT_SECS = 20;

			if (!_subscriptionIds.TryGetValue(topic, out var subId))
			{
				throw new ArgumentException($"no known subscription for {topic}", "evt");
			}

			EventHubMessage request = CreateUnsubscriptionMessage(subId);

			var completionTaskSource = new TaskCompletionSource<SubscriptionChangeResponseData>();
			_unsubscriptionResponseHandlers.TryAdd(subId, completionTaskSource);

			_messagesWaitingForSending.Writer.TryWrite(request);

			try
			{
				var response = await completionTaskSource.Task.WaitAsync(TimeSpan.FromSeconds(TIMEOUT_SECS));

				if (response.result == EventHubSubscriptionResult.Error)
				{
					throw new HttpRequestException("the unsubscription has failed");
				}

				_subscriptionIds.TryRemove(topic, out _);
			}
			catch
			{
				// if sending failed, this is already set to the exception, but timeout does not complete task
				completionTaskSource.TrySetCanceled();
				throw;
			}
			finally
			{
				_unsubscriptionResponseHandlers.TryRemove(subId, out _);
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
					// TODO: during long connections the server might request a close and then the subscriptions need to be renewed
					// TODO: retry connect as in irc client
					await websocket.ConnectAsync(uri, _endConnection.Token);

					// setup handling loops
					using var supportLoopCancellationSource = new CancellationTokenSource();
					var subscriptionProcessingTask = SendMessageLoop(websocket, supportLoopCancellationSource.Token);
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
						in case the cancellation was not requested we need to use the recoveryUrl, which gets invalidated upon normal close
						the connection for the recovery url would be instantly refused 
						therefore we can't call CloseAsync here
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
				case SubscriptionChangeResponseData subscriptionChangeData:
					_logger.LogVerbose($"subscription change [{message.type}]: {subscriptionChangeData.result} -> {subscriptionChangeData.subscription.id}");

					var responseHandlers = message.type switch
					{
						EventHubMessageType.SubscribeResponse => _subscriptionResponseHandlers,
						EventHubMessageType.UnsubscribeResponse => _unsubscriptionResponseHandlers,
						_ => throw new NotImplementedException()
					};

					// for expected subscriptions, complete the subscription task instead of calling the regular message handler
					if (responseHandlers.TryRemove(subscriptionChangeData.subscription.id, out var subscriptionChangeTaskSource))
					{
						subscriptionChangeTaskSource.SetResult(subscriptionChangeData);
					}
					else
					{
						_logger.LogWarning($"received subscription change response for unknown subscription {message.type} {subscriptionChangeData.subscription.id} ({subscriptionChangeData.result})");
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

		private async Task SendMessageLoop(EventingWebSocket socket, CancellationToken cancellationToken)
		{
			try
			{
				await foreach (var message in _messagesWaitingForSending.Reader.ReadAllAsync(cancellationToken))
				{
					try
					{
						var messageStr = JsonSerializer.Serialize(message, _jsonSerializerOptions);
						_logger.LogVerbose($"send message {messageStr}");
						// can't be interrupted as the subrequest has already been removed from the channel and now needs to be processed.
						// if sending fails, then this subscription will just fail
						await socket.SendTextPooledAsync(messageStr, CancellationToken.None);
					}
					catch (Exception ex)
					{

						var responseHandlers = message.type switch
						{
							EventHubMessageType.SubscribeResponse => _subscriptionResponseHandlers,
							EventHubMessageType.UnsubscribeResponse => _unsubscriptionResponseHandlers,
							_ => throw new NotImplementedException()
						};

						var subId = message.type switch
						{
							EventHubMessageType.SubscribeResponse => ((SubscribeData)message.Data).id,
							EventHubMessageType.UnsubscribeResponse => ((UnsubscribeData)message.Data).id,
							_ => throw new NotImplementedException()
						};

						if (_subscriptionResponseHandlers.TryRemove(subId, out var subscriptionTaskSource))
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

		private string GetTopic(TwitchChatEvent evt, string streamerId)
		{
			return evt switch
			{
				TwitchChatEvent.VideoPlaybackById => $"video-playback-by-id.{streamerId}",
				_ => throw new NotImplementedException(),
			};
		}

		private EventHubMessage CreateSubscriptionMessage(string subId, string subscribeId)
		{
			return new EventHubMessage
			{
				id = GenerateNanoId(),
				type = EventHubMessageType.Subscribe,
				timestamp = DateTime.UtcNow,
				Data = new SubscribeData
				{
					id = subscribeId,
					pubsub = new SubscriptionPubSub { topic = subId }
				}
			};
		}

		private EventHubMessage CreateUnsubscriptionMessage(string subscribeId)
		{
			return new EventHubMessage
			{
				id = GenerateNanoId(),
				type = EventHubMessageType.Unsubscribe,
				timestamp = DateTime.UtcNow,
				Data = new UnsubscribeData { id = subscribeId }
			};
		}

		public void Dispose()
		{
			try
			{
				_messagesWaitingForSending.Writer.TryComplete();
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
	}
}