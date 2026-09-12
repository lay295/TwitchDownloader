using TwitchDownloaderAvalonia.Models;
using TwitchDownloaderAvalonia.Services;
using TwitchDownloaderAvalonia.ViewModels;
using TwitchDownloaderCore.Models;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderAvalonia.Tests.ServiceTests
{
    public class MassEnqueuePlannerTests
    {
        [Fact]
        public void VideoOnlyQueuesVideoJobs()
        {
            var jobs = Build(Items(Vod("1"), Vod("2"), Clip("clip")), new EnqueueOptions
            {
                Folder = Folder,
                Quality = "1080p",
                DownloadVideo = true,
            });

            Assert.Equal(3, jobs.Count);
            Assert.All(jobs, job => Assert.Null(job.DependantTask));
            Assert.Equal(QueueTaskKind.VodDownload, jobs[0].Kind);
            Assert.Equal(QueueTaskKind.VodDownload, jobs[1].Kind);
            Assert.Equal(QueueTaskKind.ClipDownload, jobs[2].Kind);
        }

        [Fact]
        public void ChatOnlyQueuesChatJobs()
        {
            var jobs = Build(Items(Vod("1"), Clip("clip")), new EnqueueOptions
            {
                Folder = Folder,
                Quality = "Source",
                DownloadVideo = false,
                DownloadChat = true,
                ChatFormat = ChatFormat.Json,
            });

            Assert.Equal(2, jobs.Count);
            Assert.All(jobs, job =>
            {
                Assert.Equal(QueueTaskKind.ChatDownload, job.Kind);
                Assert.Null(job.DependantTask);
            });
        }

        [Fact]
        public void VideoAndChatAreIndependent()
        {
            var jobs = Build(Items(Vod("1"), Clip("clip")), new EnqueueOptions
            {
                Folder = Folder,
                Quality = "Source",
                DownloadVideo = true,
                DownloadChat = true,
                ChatFormat = ChatFormat.Json,
            });

            Assert.Equal(4, jobs.Count);
            Assert.Equal(QueueTaskKind.VodDownload, jobs[0].Kind);
            Assert.Equal(QueueTaskKind.ChatDownload, jobs[1].Kind);
            Assert.Equal(QueueTaskKind.ClipDownload, jobs[2].Kind);
            Assert.Equal(QueueTaskKind.ChatDownload, jobs[3].Kind);
            Assert.All(jobs, job => Assert.Null(job.DependantTask));
        }

        [Fact]
        public void JsonRenderWaitsOnChat()
        {
            var jobs = Build(Items(Vod("1")), new EnqueueOptions
            {
                Folder = Folder,
                Quality = "Source",
                DownloadVideo = true,
                DownloadChat = true,
                ChatFormat = ChatFormat.Json,
                RenderChat = true,
            });

            Assert.Equal(3, jobs.Count);
            Assert.Equal(QueueTaskKind.VodDownload, jobs[0].Kind);
            Assert.Equal(QueueTaskKind.ChatDownload, jobs[1].Kind);
            Assert.Equal(QueueTaskKind.ChatRender, jobs[2].Kind);
            Assert.Null(jobs[0].DependantTask);
            Assert.Null(jobs[1].DependantTask);
            Assert.Same(jobs[1], jobs[2].DependantTask);
            Assert.Equal(QueueItemStatus.Waiting, jobs[2].Status);
        }

        [Theory]
        [InlineData(ChatFormat.Text)]
        [InlineData(ChatFormat.Html)]
        public void RenderIsIgnoredUnlessJson(ChatFormat format)
        {
            var jobs = Build(Items(Vod("1")), new EnqueueOptions
            {
                Folder = Folder,
                Quality = "Source",
                DownloadVideo = true,
                DownloadChat = true,
                ChatFormat = format,
                RenderChat = true,
            });

            Assert.Equal(2, jobs.Count);
            Assert.DoesNotContain(jobs, job => job.Kind == QueueTaskKind.ChatRender);
        }

        [Fact]
        public void GzipAppliesOnlyToJsonAndRenderStripsGz()
        {
            var jsonJobs = Build(Items(Vod("1")), new EnqueueOptions
            {
                Folder = Folder,
                Quality = "Source",
                DownloadVideo = false,
                DownloadChat = true,
                ChatFormat = ChatFormat.Json,
                ChatCompression = ChatCompression.Gzip,
                RenderChat = true,
            });
            var txtJobs = Build(Items(Vod("2")), new EnqueueOptions
            {
                Folder = Folder,
                Quality = "Source",
                DownloadVideo = false,
                DownloadChat = true,
                ChatFormat = ChatFormat.Text,
                ChatCompression = ChatCompression.Gzip,
            });

            var chat = Assert.IsType<ChatDownloadOptions>(jsonJobs[0].Options);
            var render = Assert.IsType<ChatRenderOptions>(jsonJobs[1].Options);
            Assert.Equal(ChatCompression.Gzip, chat.Compression);
            Assert.EndsWith(".json.gz", chat.Filename);
            Assert.Equal(chat.Filename, render.InputFile);
            Assert.DoesNotContain(".gz", render.OutputFile);
            Assert.Equal(Path.ChangeExtension(chat.Filename.Replace(".gz", ""), ".mp4"), render.OutputFile);

            var txt = Assert.IsType<ChatDownloadOptions>(txtJobs[0].Options);
            Assert.Equal(ChatCompression.None, txt.Compression);
            Assert.EndsWith(".txt", txt.Filename);
        }

        [Fact]
        public void RenderUsesChatSuffixWhenPathCollides()
        {
            var settings = new AppSettings
            {
                TemplateVod = "{title}",
                TemplateChat = "{title}",
                RenderVideoContainer = "MP4",
            };
            var jobs = Build(Items(Vod("1", "SameName")), new EnqueueOptions
            {
                Folder = Folder,
                Quality = "Source",
                DownloadVideo = true,
                DownloadChat = true,
                ChatFormat = ChatFormat.Json,
                RenderChat = true,
            }, settings);

            var video = Assert.IsType<VideoDownloadOptions>(jobs[0].Options);
            var chat = Assert.IsType<ChatDownloadOptions>(jobs[1].Options);
            var render = Assert.IsType<ChatRenderOptions>(jobs[2].Options);
            Assert.Equal(MassEnqueuePaths.ChatRenderOutput(chat.Filename, video.Filename, "mp4"), render.OutputFile);
            Assert.Contains(" - CHAT.", render.OutputFile);
        }

        [Fact]
        public void DelayFlagsApplyToVodsNeverClips()
        {
            var jobs = Build(Items(Vod("9", isRecording: true), Clip("clip")), new EnqueueOptions
            {
                Folder = Folder,
                Quality = "Source",
                DownloadVideo = true,
                DelayVideo = true,
                DownloadChat = true,
                DelayChat = true,
                ChatFormat = ChatFormat.Json,
            });

            var vod = Assert.IsType<VideoDownloadOptions>(jobs[0].Options);
            var vodChat = Assert.IsType<ChatDownloadOptions>(jobs[1].Options);
            Assert.IsType<ClipDownloadOptions>(jobs[2].Options);
            var clipChat = Assert.IsType<ChatDownloadOptions>(jobs[3].Options);
            Assert.True(vod.DelayDownload);
            Assert.True(vodChat.DelayDownload);
            Assert.False(clipChat.DelayDownload);
        }

        [Fact]
        public void DelayFlagsAreNotAppliedToFinishedVods()
        {
            var jobs = Build(Items(Vod("9"), Clip("clip")), new EnqueueOptions
            {
                Folder = Folder,
                Quality = "Source",
                DownloadVideo = true,
                DelayVideo = true,
                DownloadChat = true,
                DelayChat = true,
                ChatFormat = ChatFormat.Json,
            });

            var vod = Assert.IsType<VideoDownloadOptions>(jobs[0].Options);
            var vodChat = Assert.IsType<ChatDownloadOptions>(jobs[1].Options);
            Assert.False(vod.DelayDownload);
            Assert.False(vodChat.DelayDownload);
        }

        [Fact]
        public void NeitherVideoNorChatReturnsNoJobs()
        {
            var jobs = Build(Items(Vod("1")), new EnqueueOptions
            {
                Folder = Folder,
                Quality = "Source",
                DownloadVideo = false,
                DownloadChat = false,
                RenderChat = true,
            });

            Assert.Empty(jobs);
        }

        private static IReadOnlyList<QueueItemViewModel> Build(
            IReadOnlyList<QueueableMedia> items,
            EnqueueOptions options,
            AppSettings? settings = null)
        {
            return MassEnqueuePlanner.Build(items, options, new MassEnqueueContext
            {
                Settings = settings ?? new AppSettings(),
                FfmpegPath = "ffmpeg",
                CollisionCallback = info => info,
                LogLevel = LogLevel.None,
            });
        }

        private static string Folder => Path.Combine(Path.GetTempPath(), "TwitchDownloaderEnqueue");

        private static QueueableMedia[] Items(params QueueableMedia[] items) => items;

        private static QueueableMedia Vod(string id, string title = "Vod", bool isRecording = false) => new()
        {
            Id = id,
            Title = title,
            Time = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Length = 60,
            Views = 10,
            Game = "Game",
            IsClip = false,
            IsRecording = isRecording,
            StreamerName = "Streamer",
            StreamerId = "1",
        };

        private static QueueableMedia Clip(string id) => new()
        {
            Id = id,
            Title = "Clip",
            Time = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Length = 30,
            Views = 4,
            Game = "Game",
            IsClip = true,
            StreamerName = "Streamer",
            StreamerId = "1",
            ClipperName = "Clipper",
            ClipperId = "2",
        };
    }

    public class MassEnqueuePathsTests
    {
        [Fact]
        public void StripsGzBeforeChangingExtension()
        {
            var output = MassEnqueuePaths.ChatRenderOutput("/tmp/chat.json.gz", videoFilename: null, "MP4");

            Assert.Equal(Path.ChangeExtension("/tmp/chat.json", ".mp4"), output);
        }

        [Fact]
        public void AddsChatSuffixWhenOutputMatchesChatOrVideo()
        {
            var chatCollision = MassEnqueuePaths.ChatRenderOutput("/tmp/file.mp4", "/tmp/video.mp4", "mp4");
            var videoCollision = MassEnqueuePaths.ChatRenderOutput("/tmp/chat.json", "/tmp/chat.mp4", "mp4");

            Assert.Equal(Path.ChangeExtension("/tmp/file.mp4", " - CHAT.mp4"), chatCollision);
            Assert.Equal(Path.ChangeExtension("/tmp/chat.json", " - CHAT.mp4"), videoCollision);
        }
    }

    public class EnqueueOptionsViewModelTests
    {
        [Fact]
        public void CannotAddWithoutVideoOrChat()
        {
            using var vm = Create(hasVods: true);
            vm.Folder = "/tmp/queue";
            vm.DownloadVideo = false;
            vm.DownloadChat = false;

            Assert.False(vm.CanAdd);
            Assert.False(vm.AddCommand.CanExecute(null));
        }

        [Fact]
        public void ClipsOnlyHidesAudioOnlyAndDelay()
        {
            using var vm = Create(hasVods: false);

            Assert.DoesNotContain("Audio Only", vm.AvailableQualities);
            Assert.False(vm.ShowDelayControls);
            Assert.False(vm.IsDelayVideoEnabled);
            Assert.False(vm.IsDelayChatEnabled);
        }

        [Fact]
        public void FinishedVodsHideDelayButKeepAudioOnly()
        {
            using var vm = Create(hasVods: true, hasRecordingVods: false);

            Assert.Contains("Audio Only", vm.AvailableQualities);
            Assert.False(vm.ShowDelayControls);
            Assert.False(vm.IsDelayVideoEnabled);
            Assert.False(vm.IsDelayChatEnabled);
        }

        [Fact]
        public void EnablementMatchesFormatAndCheckboxes()
        {
            using var vm = Create(hasVods: true);
            vm.Folder = "/tmp/queue";
            Assert.True(vm.IsQualityEnabled);
            Assert.False(vm.IsChatOptionsEnabled);
            Assert.False(vm.IsRenderEnabled);

            vm.DownloadVideo = false;
            Assert.False(vm.IsQualityEnabled);
            Assert.False(vm.IsDelayVideoEnabled);

            vm.DownloadChat = true;
            vm.RenderChat = true;
            vm.ChatFormat = ChatFormat.Json;
            vm.EmbedImages = true;
            Assert.True(vm.ShowCompression);
            Assert.True(vm.IsEmbedEnabled);
            Assert.True(vm.IsThirdPartyEmbedEnabled);
            Assert.True(vm.IsRenderEnabled);
            Assert.True(vm.RenderChat);

            vm.ChatFormat = ChatFormat.Text;
            Assert.False(vm.ShowCompression);
            Assert.False(vm.IsEmbedEnabled);
            Assert.False(vm.IsThirdPartyEmbedEnabled);
            Assert.False(vm.IsRenderEnabled);
            Assert.False(vm.RenderChat);

            vm.ChatFormat = ChatFormat.Html;
            vm.EmbedImages = true;
            Assert.False(vm.ShowCompression);
            Assert.True(vm.IsEmbedEnabled);
            Assert.True(vm.IsThirdPartyEmbedEnabled);
            Assert.False(vm.IsRenderEnabled);

            vm.DownloadChat = false;
            Assert.False(vm.IsRenderEnabled);
            Assert.False(vm.RenderChat);
        }

        [Fact]
        public void AddOmitsDelayForClipsOnly()
        {
            EnqueueOptions? result = null;
            using var vm = Create(hasVods: false, close: options => result = options);
            vm.Folder = "/tmp/queue";
            vm.DownloadChat = true;
            vm.DelayVideo = true;
            vm.DelayChat = true;
            vm.RenderChat = true;
            vm.ChatFormat = ChatFormat.Text;

            vm.AddCommand.Execute(null);

            Assert.NotNull(result);
            Assert.False(result!.DelayVideo);
            Assert.False(result.DelayChat);
            Assert.False(result.RenderChat);
            Assert.True(result.DownloadChat);
        }

        private static EnqueueOptionsViewModel Create(bool hasVods, Action<EnqueueOptions?>? close = null, bool? hasRecordingVods = null)
        {
            var directory = Path.Combine(Path.GetTempPath(), "TwitchDownloaderTests", Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(directory, "avalonia-settings.json"));
            return new EnqueueOptionsViewModel(
                settings,
                new FileDialogService(),
                hasVods,
                hasRecordingVods ?? hasVods,
                close ?? (_ => { }));
        }
    }
}
