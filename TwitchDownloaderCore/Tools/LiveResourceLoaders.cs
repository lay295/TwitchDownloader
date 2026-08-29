using System.Collections.Concurrent;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCore.Tools
{
	public class FirstParteEmoteLoader
	{
		private readonly ITaskLogger _logger;
		private readonly DirectoryInfo _cache;
		private ConcurrentDictionary<string, Task<TwitchEmote>> _firstPartyEmotes = new();

		public FirstParteEmoteLoader(ITaskLogger logger, DirectoryInfo cache)
		{
			_logger = logger;
			_cache = cache;
		}

		public void ProcessComment(Comment comment)
		{
			foreach (var emoticon in comment.message.emoticons)
			{
				_ = _firstPartyEmotes.GetOrAdd(emoticon._id, emoticonId => TwitchHelper.GetFirstPartyEmote(emoticonId, _cache, false, _logger, CancellationToken.None));
			}
		}

		public async Task<List<EmbedEmoteData>> GetList()
		{
			var downloadedEmotes = await Task.WhenAll(_firstPartyEmotes.Values);
			return downloadedEmotes
				.Select(emote => new EmbedEmoteData
				{
					id = emote.Id,
					imageScale = emote.ImageScale,
					data = emote.ImageData,
					width = emote.Width / emote.ImageScale,
					height = emote.Height / emote.ImageScale,
				})
				.ToList();
		}
	}

	public class BadgeLoader
	{
		private readonly ITaskLogger _logger;
		private readonly DirectoryInfo _cache;
		private Task<List<EmbedChatBadge>> _chatBadgeData;
		private ConcurrentDictionary<string, Task<EmbedChatBadge>> _chatBadges = new();

		public BadgeLoader(int streamerId, ITaskLogger logger, DirectoryInfo cache)
		{
			_logger = logger;
			_cache = cache;
			_chatBadgeData = TwitchHelper.GetChatBadgesData(null, streamerId);
		}

		public void ProcessComment(Comment comment)
		{
			foreach (var badge in comment.message.user_badges)
			{
				_ = _chatBadges.GetOrAdd(badge._id, _ => LoadBadge(badge));
			}
		}

		private async Task<EmbedChatBadge> LoadBadge(UserBadge badge)
		{
			var chatBadges = await _chatBadgeData;

			var embedChatBadge = chatBadges.Find(embedChatBadge => embedChatBadge.name == badge._id);
			if (embedChatBadge is null)
			{
				return null;
			}

			var chatBadge = await TwitchHelper.GetChatBadge(embedChatBadge, _cache, _logger, CancellationToken.None);
			if (chatBadge is null)
			{
				return null;
			}

			var newBadge = new EmbedChatBadge
			{
				name = chatBadge.Name,
				versions = chatBadge.VersionsData
			};

			return newBadge;
		}

		public async Task<List<EmbedChatBadge>> GetList()
		{
			var downloadedBadges = await Task.WhenAll(_chatBadges.Values);
			return downloadedBadges
				.Where(badge => badge is not null)
				.ToList();
		}
	}
}