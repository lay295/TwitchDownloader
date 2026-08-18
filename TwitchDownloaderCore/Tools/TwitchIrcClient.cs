using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Models;

namespace TwitchDownloaderCore.Tools
{
	public sealed class TwitchIrcClient
	{
		private const string ANONYMOUS_PASSWORD = "SCHMOOPIIE";
		private static readonly string AnonymousUsername = $"justinfan{Random.Shared.Next(10_000, 99_999)}";


		public static ChannelReader<IrcMessage> MessagesFor(string channelName, Task stopSignal, CancellationToken cancellationToken, ITaskLogger logger)
		{
			var channel = Channel.CreateUnbounded<IrcMessage>();

			_ = RunMessagePump(channelName, channel.Writer, stopSignal, cancellationToken, logger);

			return channel.Reader;
		}

		private static async Task RunMessagePump(
			string channelName,
			ChannelWriter<IrcMessage> channel,
			Task stopSignal,
			CancellationToken cancellationToken,
			ITaskLogger logger
		)
		{
			try
			{
				var parser = new IrcParser(logger);

				while (true)
				{
					// this is used to signal a need for reconnection, which is then awaited and leads to this while loop going into the next round
					var reconnectionRequired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

					using var websocket = new EventingWebSocket(logger);
					EventHandler<EventingWebSocket.Message> eventHandler = (object sender, EventingWebSocket.Message msg) => HandleMessageReceived(sender, msg, websocket, channel, reconnectionRequired, logger, parser);
					websocket.MessageReceived += eventHandler;

					await ConnectToTwitchIrc(websocket, cancellationToken, logger);

					await websocket.SendTextPooledAsync("CAP REQ :twitch.tv/commands twitch.tv/tags", CancellationToken.None);
					await websocket.SendTextPooledAsync($"PASS {ANONYMOUS_PASSWORD}", CancellationToken.None);
					await websocket.SendTextPooledAsync($"NICK {AnonymousUsername}", CancellationToken.None);
					await websocket.SendTextPooledAsync($"USER {AnonymousUsername} 8 * :{AnonymousUsername}", CancellationToken.None);
					logger.LogVerbose($"Joining #{channelName}...");
					await websocket.SendTextPooledAsync($"JOIN #{channelName}", CancellationToken.None);

					// this waits for either a need for reconnection or the desired end of the listening
					// WhenAny will mask the stopListening throw, so Disconnect happens either way
					var completedTask = await Task.WhenAny(reconnectionRequired.Task, stopSignal, Task.Delay(Timeout.Infinite, cancellationToken));

					websocket.MessageReceived -= eventHandler;
					await websocket.CloseAsync();

					cancellationToken.ThrowIfCancellationRequested();
					if (completedTask != reconnectionRequired.Task)
					{
						break;
					}
				}

				channel.TryComplete();
			}
			catch (Exception ex)
			{
				channel.TryComplete(ex);
			}
		}

		private static async Task ConnectToTwitchIrc(EventingWebSocket client, CancellationToken cancellationToken, ITaskLogger logger)
		{
			var count = 0;
			const int MAX_TRIES = 10;
			while (true)
			{
				try
				{
					await client.ConnectAsync(new Uri("wss://irc-ws.chat.twitch.tv/"), cancellationToken);
					return;
				}
				catch (Exception)
				{
					if (++count >= MAX_TRIES)
					{
						logger.LogWarning($"Failed to connect to Twitch IRC after {MAX_TRIES} tries.");
						throw;
					}
					var sleepTime = GetExponentialBackoff(count);
					logger.LogVerbose($"Failed to connect to Twitch IRC, retrying in {sleepTime:N0}ms...");
					await Task.Delay(sleepTime, cancellationToken);
				}
			}
		}

		private static int GetExponentialBackoff(int count)
		{
			return (int)Math.Min(
				Math.Pow(2.25, count) * Random.Shared.Next(50, 100),
				30_000
			);
		}

		private static void HandleMessageReceived(object sender, EventingWebSocket.Message e, EventingWebSocket client, ChannelWriter<IrcMessage> writer, TaskCompletionSource reconnectRequired, ITaskLogger logger, IrcParser parser)
		{
			switch (e.MessageType)
			{
				case WebSocketMessageType.Binary:
					logger.LogWarning("Binary messages are not supported. See verbose log for more info.");
					logger.LogVerbose($"Binary message content: {Convert.ToBase64String(e.Buffer)}");
					return;
				case WebSocketMessageType.Close:
					// this can happen when legitimatly closing after finishing to collect all data.
					// Still, if something else triggers it, we need to reconnect, otherwise the stopListening check will catch it
					reconnectRequired.TrySetResult();
					return;
			}

			var messages = parser.Parse(e.Buffer);

			foreach (var ircMessage in messages)
			{
				switch (ircMessage.Command)
				{
					case IrcCommand.Ping:
						client.SendTextPooledAsync(
							ircMessage.ParametersRaw is { Length: > 0 } ? $"PONG {ircMessage.ParametersRaw}" : "PONG",
							CancellationToken.None
						);
						break;
					case IrcCommand.Reconnect:
						logger.LogVerbose("Twitch requested reconnect, initiating reconnect...");
						reconnectRequired.TrySetResult();
						break;
					case IrcCommand.PrivMsg:
					case IrcCommand.UserNotice:
						writer.TryWrite(ircMessage);
						break;
					case IrcCommand.Unknown:
						logger.LogWarning($"Got unknown IRC command: {Encoding.UTF8.GetString(e.Buffer).TrimEnd()}");
						break;
				}
			}
		}
	}
}
