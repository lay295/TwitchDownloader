﻿using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using TwitchDownloaderCLI.Tools;
using TwitchDownloaderCore;
using TwitchDownloaderCore.Options;

namespace TwitchDownloaderCLI.Modes
{
    internal class RenderChat
    {
        internal static void Render(Options inputOptions, string ffmpegExecutableName)
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
                    "italic" => SKFontStyle.Italic,
                    "italics" => SKFontStyle.Italic,
                    _ => throw new NotImplementedException("Invalid message font style")
                },

                UsernameFontStyle = inputOptions.UsernameFontStyle.ToLower() switch
                {
                    "normal" => SKFontStyle.Normal,
                    "bold" => SKFontStyle.Bold,
                    "italic" => SKFontStyle.Italic,
                    "italics" => SKFontStyle.Italic,
                    _ => throw new NotImplementedException("Invalid username font style")
                },
                UpdateRate = inputOptions.UpdateRate,
                Framerate = inputOptions.Framerate,
                GenerateMask = inputOptions.GenerateMask,
                InputArgs = inputOptions.InputArgs,
                OutputArgs = inputOptions.OutputArgs,
                FfmpegPath = inputOptions.FfmpegPath is null || inputOptions.FfmpegPath == string.Empty ? ffmpegExecutableName : Path.GetFullPath(inputOptions.FfmpegPath),
                TempFolder = inputOptions.TempFolder,
                SubMessages = (bool)inputOptions.SubMessages,
                ChatBadges = (bool)inputOptions.ChatBadges,
                Timestamp = inputOptions.Timestamp

            };

            if (renderOptions.GenerateMask && renderOptions.BackgroundColor.Alpha == 255)
            {
                Console.WriteLine("[WARNING] - Generate mask option has been selected with an opaque background. You most likely want to set a transparent background with --background-color \"#00000000\"");
            }

            if (renderOptions.ChatHeight % 2 != 0 || renderOptions.ChatWidth % 2 != 0)
            {
                Console.WriteLine("[WARNING] - Height and Width MUST be even, rounding up to the nearest even number to prevent errors");
                if (renderOptions.ChatHeight % 2 != 0)
                    renderOptions.ChatHeight++;
                if (renderOptions.ChatWidth % 2 != 0)
                    renderOptions.ChatWidth++;
            }

            if (inputOptions.IgnoreUsersList != string.Empty)
            {
                renderOptions.IgnoreUsersList = inputOptions.IgnoreUsersList.ToLower().Split(',',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            }


            ChatRenderer chatDownloader = new(renderOptions);
            Progress<ProgressReport> progress = new();
            progress.ProgressChanged += ProgressHandler.Progress_ProgressChanged;
            chatDownloader.ParseJson().Wait();
            chatDownloader.RenderVideoAsync(progress, new CancellationToken()).Wait();
        }
    }
}
