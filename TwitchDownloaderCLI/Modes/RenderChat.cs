﻿using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using TwitchDownloaderCLI.Modes.Arguments;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal class RenderChat
    {
        internal static void Render(ChatRenderArgs inputOptions)
        {
            FfmpegHandler.DetectFfmpeg(inputOptions.FfmpegPath);

            Progress<ProgressReport> progress = new();
            progress.ProgressChanged += ProgressHandler.Progress_ProgressChanged;

            ChatRenderOptions renderOptions = GetRenderOptions(inputOptions);
            ChatRenderer chatRenderer = new(renderOptions);
            chatRenderer.ParseJsonAsync().Wait();
            chatRenderer.RenderVideoAsync(progress, new CancellationToken()).Wait();
        }

        private static ChatRenderOptions GetRenderOptions(ChatRenderArgs inputOptions)
        {
            ChatRenderOptions renderOptions = new()
            {
                InputFile = inputOptions.InputFile,
                OutputFile = inputOptions.OutputFile,
                BackgroundColor = SKColor.Parse(inputOptions.BackgroundColor),
                MessageColor = SKColor.Parse(inputOptions.MessageColor),
                ChatHeight = inputOptions.ChatHeight,
                ChatWidth = inputOptions.ChatWidth,
                BttvEmotes = (bool)inputOptions.BttvEmotes,
                FfzEmotes = (bool)inputOptions.FfzEmotes,
                StvEmotes = (bool)inputOptions.StvEmotes,
                Outline = inputOptions.Outline,
                OutlineSize = inputOptions.OutlineSize,
                Font = inputOptions.Font,
                FontSize = inputOptions.FontSize,
                ChatBadgeMask = inputOptions.BadgeFilterMask,
                MessageFontStyle = inputOptions.MessageFontStyle.ToLower() switch
                {
                    "normal" => SKFontStyle.Normal,
                    "bold" => SKFontStyle.Bold,
                    "italic" or "italics" => SKFontStyle.Italic,
                    _ => throw new NotImplementedException("Invalid message font style. Valid values are: normal, bold, and italic")
                },
                UsernameFontStyle = inputOptions.UsernameFontStyle.ToLower() switch
                {
                    "normal" => SKFontStyle.Normal,
                    "bold" => SKFontStyle.Bold,
                    "italic" or "italics" => SKFontStyle.Italic,
                    _ => throw new NotImplementedException("Invalid username font style. Valid values are: normal, bold, and italic")
                },
                UpdateRate = inputOptions.UpdateRate,
                Framerate = inputOptions.Framerate,
                GenerateMask = inputOptions.GenerateMask,
                InputArgs = inputOptions.InputArgs,
                OutputArgs = inputOptions.OutputArgs,
                FfmpegPath = string.IsNullOrWhiteSpace(inputOptions.FfmpegPath) ? FfmpegHandler.ffmpegExecutableName : Path.GetFullPath(inputOptions.FfmpegPath),
                TempFolder = inputOptions.TempFolder,
                SubMessages = (bool)inputOptions.SubMessages,
                ChatBadges = (bool)inputOptions.ChatBadges,
                Timestamp = inputOptions.Timestamp,
                Offline = inputOptions.Offline,
                LogFfmpegOutput = inputOptions.LogFfmpegOutput
            };

            if (renderOptions.GenerateMask && renderOptions.BackgroundColor.Alpha == 255)
            {
                Console.WriteLine("[WARNING] - Generate mask option has been selected with an opaque background. You most likely want to set a transparent background with --background-color \"#00000000\"");
            }

            if (renderOptions.ChatHeight % 2 != 0 || renderOptions.ChatWidth % 2 != 0)
            {
                Console.WriteLine("[WARNING] - Width and Height MUST be even, rounding up to the nearest even number to prevent errors");
                if (renderOptions.ChatHeight % 2 != 0)
                {
                    renderOptions.ChatHeight++;
                }
                if (renderOptions.ChatWidth % 2 != 0)
                {
                    renderOptions.ChatWidth++;
                }
            }

            if (inputOptions.IgnoreUsersList != "")
            {
                renderOptions.IgnoreUsersList = inputOptions.IgnoreUsersList.ToLower().Split(',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            return renderOptions;
        }
    }
}
