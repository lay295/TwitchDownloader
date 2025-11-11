using System;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Interfaces;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal static class RecordChat
    {
        public static void Record(ChatRecorderArgs inputOptions)
        {
            using var progress = new CliTaskProgress(inputOptions.LogLevel);
            var collisionHandler = new FileCollisionHandler(inputOptions, progress);
            var recorderOptions = GetRecorderOptions(inputOptions, collisionHandler, progress);

            using var chatRecorder = new LiveChatRecorder(recorderOptions, progress);
            chatRecorder.RecordAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        private static LiveChatRecorderOptions GetRecorderOptions(ChatRecorderArgs inputOptions, FileCollisionHandler collisionHandler, ITaskLogger logger)
        {
            var options = new LiveChatRecorderOptions
            {
                Channel = inputOptions.Channel,
                OutputFile = inputOptions.OutputFile,
                FileCollisionCallback = collisionHandler.HandleCollisionCallback,
            };

            Console.CancelKeyPress += (_, args) =>
            {
                options.OnStopRecording();
                args.Cancel = true;
            };

            return options;
        }
    }
}