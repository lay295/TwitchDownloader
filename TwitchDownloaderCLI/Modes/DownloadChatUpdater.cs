using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;
using TwitchDownloaderCore.TwitchObjects;

namespace TwitchDownloaderCLI.Modes
{
    internal class DownloadChatUpdater
    {
        internal static void Update(ChatDownloadUpdaterArgs inputOptions)
        {

            // Check that both input and output are 
            DownloadFormat inFormat = Path.GetExtension(inputOptions.InputFile)!.ToLower() switch
            {
                ".json" => DownloadFormat.Json,
                ".html" => DownloadFormat.Html,
                ".htm" => DownloadFormat.Html,
                _ => DownloadFormat.Text
            };
            DownloadFormat outFormat = Path.GetExtension(inputOptions.OutputFile)!.ToLower() switch
            {
                ".json" => DownloadFormat.Json,
                ".html" => DownloadFormat.Html,
                ".htm" => DownloadFormat.Html,
                _ => DownloadFormat.Text
            };
            if (inFormat != DownloadFormat.Json || outFormat != DownloadFormat.Json)
            {
                Console.WriteLine("[ERROR] - Format much only be JSON");
                Environment.Exit(1);
            }
            if (!File.Exists(inputOptions.InputFile))
            {
                Console.WriteLine("[ERROR] - Input file does not exist!");
                Environment.Exit(1);
            }
            if (!inputOptions.EmbedMissingEmotes && !inputOptions.UpdateOldEmotes)
            {
                Console.WriteLine("[ERROR] - Please enable either EmbedMissingEmotes or UpdateOldEmotes");
                Environment.Exit(1);
            }

            // Read in the old input file
            // TODO: to be honest, this should just be copied here, we should instead use a function
            // TODO: that can be shared by the ChatRenderer file which also needs to open this file...
            ChatRoot chatRoot = new ChatRoot();
            using (FileStream fs = new FileStream(inputOptions.InputFile, FileMode.Open, FileAccess.Read))
            {
                using (var jsonDocument = JsonDocument.Parse(fs))
                {
                    if (jsonDocument.RootElement.TryGetProperty("streamer", out JsonElement streamerJson))
                    {
                        chatRoot.streamer = streamerJson.Deserialize<Streamer>();
                    }
                    if (jsonDocument.RootElement.TryGetProperty("video", out JsonElement videoJson))
                    {
                        if (videoJson.TryGetProperty("start", out JsonElement videoStartJson) && videoJson.TryGetProperty("end", out JsonElement videoEndJson))
                        {
                            chatRoot.video = videoJson.Deserialize<VideoTime>();
                        }
                    }
                    if (jsonDocument.RootElement.TryGetProperty("emotes", out JsonElement emotesJson))
                    {
                        chatRoot.emotes = emotesJson.Deserialize<Emotes>();
                    }
                    if (jsonDocument.RootElement.TryGetProperty("comments", out JsonElement commentsJson))
                    {
                        chatRoot.comments = commentsJson.Deserialize<List<Comment>>();
                    }
                }
            }
            if (chatRoot.streamer == null)
            {
                chatRoot.streamer = new Streamer();
                chatRoot.streamer.id = int.Parse(chatRoot.comments.First().channel_id);
                chatRoot.streamer.name = Task.Run(() => TwitchHelper.GetStreamerName(chatRoot.streamer.id)).Result;
            }
            if (chatRoot.emotes == null)
            {
                chatRoot.emotes = new Emotes();
            }

            string cacheFolder = Path.Combine(Path.GetTempPath(), "TwitchDownloader", "cache");

            // Thirdparty emotes
            if(chatRoot.emotes.thirdParty == null || inputOptions.UpdateOldEmotes)
            {
                chatRoot.emotes.thirdParty = new List<EmbedEmoteData>();
            }
            Console.WriteLine("Thirdparty: Before " + chatRoot.emotes.thirdParty.Count);
            List<TwitchEmote> thirdPartyEmotes = new List<TwitchEmote>();
            thirdPartyEmotes = Task.Run(() => TwitchHelper.GetThirdPartyEmotes(chatRoot.streamer.id, cacheFolder, bttv: inputOptions.BttvEmotes, ffz: inputOptions.FfzEmotes, stv: inputOptions.StvEmotes, embededEmotes: chatRoot.emotes)).Result;
            foreach (TwitchEmote emote in thirdPartyEmotes)
            {
                EmbedEmoteData newEmote = new EmbedEmoteData();
                newEmote.id = emote.Id;
                newEmote.imageScale = emote.ImageScale;
                newEmote.data = emote.ImageData;
                newEmote.name = emote.Name;
                newEmote.width = emote.Width / emote.ImageScale;
                newEmote.height = emote.Height / emote.ImageScale;
                chatRoot.emotes.thirdParty.Add(newEmote);
            }
            Console.WriteLine("Thirdparty: After " + chatRoot.emotes.thirdParty.Count);

            // Firstparty emotes
            if (chatRoot.emotes.firstParty == null || inputOptions.UpdateOldEmotes)
            {
                chatRoot.emotes.firstParty = new List<EmbedEmoteData>();
            }
            Console.WriteLine("Firstparty: Before " + chatRoot.emotes.firstParty.Count);
            List<TwitchEmote> firstPartyEmotes = new List<TwitchEmote>();
            firstPartyEmotes = Task.Run(() => TwitchHelper.GetEmotes(chatRoot.comments, cacheFolder, embededEmotes: chatRoot.emotes)).Result;
            foreach (TwitchEmote emote in firstPartyEmotes)
            {
                EmbedEmoteData newEmote = new EmbedEmoteData();
                newEmote.id = emote.Id;
                newEmote.imageScale = emote.ImageScale;
                newEmote.data = emote.ImageData;
                newEmote.width = emote.Width / emote.ImageScale;
                newEmote.height = emote.Height / emote.ImageScale;
                chatRoot.emotes.firstParty.Add(newEmote);
            }
            Console.WriteLine("Firstparty: After " + chatRoot.emotes.firstParty.Count);

            // Twitch badges
            if (chatRoot.emotes.twitchBadges == null || inputOptions.UpdateOldEmotes)
            {
                chatRoot.emotes.twitchBadges = new List<EmbedChatBadge>();
            }
            Console.WriteLine("TwitchBadges: Before " + chatRoot.emotes.twitchBadges.Count);
            List<ChatBadge> twitchBadges = new List<ChatBadge>();
            twitchBadges = Task.Run(() => TwitchHelper.GetChatBadges(chatRoot.streamer.id, cacheFolder, embededEmotes: chatRoot.emotes)).Result;
            foreach (ChatBadge badge in twitchBadges)
            {
                EmbedChatBadge newBadge = new EmbedChatBadge();
                newBadge.name = badge.Name;
                newBadge.versions = badge.VersionsData;
                chatRoot.emotes.twitchBadges.Add(newBadge);
            }
            Console.WriteLine("TwitchBadges: After " + chatRoot.emotes.twitchBadges.Count);

            // Twitch bits / cheers
            if (chatRoot.emotes.twitchBits == null || inputOptions.UpdateOldEmotes)
            {
                chatRoot.emotes.twitchBits = new List<EmbedCheerEmote>();
            }
            Console.WriteLine("TwitchBits: Before " + chatRoot.emotes.twitchBits.Count);
            List<CheerEmote> twitchBits = new List<CheerEmote>();
            twitchBits = Task.Run(() => TwitchHelper.GetBits(cacheFolder, chatRoot.streamer.id.ToString(), embededEmotes: chatRoot.emotes)).Result;
            foreach (CheerEmote bit in twitchBits)
            {
                EmbedCheerEmote newBit = new EmbedCheerEmote();
                newBit.prefix = bit.prefix;
                newBit.tierList = new Dictionary<int, EmbedEmoteData>();
                foreach (KeyValuePair<int, TwitchEmote> emotePair in bit.tierList)
                {
                    EmbedEmoteData newEmote = new EmbedEmoteData();
                    newEmote.id = emotePair.Value.Id;
                    newEmote.imageScale = emotePair.Value.ImageScale;
                    newEmote.data = emotePair.Value.ImageData;
                    newEmote.name = emotePair.Value.Name;
                    newEmote.width = emotePair.Value.Width / emotePair.Value.ImageScale;
                    newEmote.height = emotePair.Value.Height / emotePair.Value.ImageScale;
                    newBit.tierList.Add(emotePair.Key, newEmote);
                }
                chatRoot.emotes.twitchBits.Add(newBit);
            }
            Console.WriteLine("TwitchBits: After " + chatRoot.emotes.twitchBits.Count);

            // Finally save the output to file!
            // TODO: maybe in the future we could also export as HTML here too?
            if (outFormat == DownloadFormat.Json)
            {
                using (TextWriter writer = File.CreateText(inputOptions.OutputFile))
                {
                    var serializer = new Newtonsoft.Json.JsonSerializer();
                    serializer.Serialize(writer, chatRoot);
                }
            }

        }
    }
}
