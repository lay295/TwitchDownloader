using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using Ookii.Dialogs.Wpf;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderWPF.Models;
using TwitchDownloaderWPF.Properties;

namespace TwitchDownloaderWPF.Services
{
    public static class FileCollisionService
    {
        private static CollisionBehavior? _sessionCollisionBehavior;

        [return: MaybeNull]
        public static FileInfo HandleCollisionCallback(FileInfo fileInfo, Window owner)
        {
            var collisionBehavior = _sessionCollisionBehavior ?? (CollisionBehavior)Settings.Default.FileCollisionBehavior;

            if (collisionBehavior is not CollisionBehavior.Prompt)
            {
                return GetResult(fileInfo, collisionBehavior);
            }

            var result = ShowDialog(fileInfo, owner, out var rememberChoice);

            if (rememberChoice)
            {
                _sessionCollisionBehavior = result;
            }

            return GetResult(fileInfo, result);
        }

        private static CollisionBehavior ShowDialog(FileInfo fileInfo, Window owner, out bool rememberChoice)
        {
            using var dialog = new TaskDialog();
            dialog.WindowTitle = Translations.Strings.TitleFileAlreadyExists;
            dialog.MainInstruction = string.Format(Translations.Strings.FileAlreadyExistsHeader, fileInfo.Name);
            dialog.Content = string.Format(Translations.Strings.FileAlreadyExistsBody, $"<a href=\"{fileInfo.FullName}\">{fileInfo.FullName}</a>");
            dialog.MainIcon = TaskDialogIcon.Warning;

            dialog.EnableHyperlinks = true;
            dialog.HyperlinkClicked += Hyperlink_OnClicked;

            dialog.ButtonStyle = TaskDialogButtonStyle.CommandLinks;

            var overwriteButton = new TaskDialogButton(Translations.Strings.FileAlreadyExistsOverwrite);
            overwriteButton.CommandLinkNote = Translations.Strings.FileAlreadyExistsOverwriteDescription;
            dialog.Buttons.Add(overwriteButton);

            var renameButton = new TaskDialogButton(Translations.Strings.FileAlreadyExistsRename);
            renameButton.CommandLinkNote = Translations.Strings.FileAlreadyExistsRenameDescription;
            dialog.Buttons.Add(renameButton);

            var cancelButton = new TaskDialogButton(Translations.Strings.FileAlreadyExistsCancel);
            cancelButton.CommandLinkNote = Translations.Strings.FileAlreadyExistsCancelDescription;
            dialog.Buttons.Add(cancelButton);

            dialog.VerificationText = Translations.Strings.FileAlreadyExistsRememberMyChoice;
            dialog.IsVerificationChecked = false;

            var buttonResult = dialog.ShowDialog(owner);

            rememberChoice = dialog.IsVerificationChecked;

            if (buttonResult == overwriteButton)
                return CollisionBehavior.Overwrite;

            if (buttonResult == renameButton)
                return CollisionBehavior.Rename;

            if (buttonResult == cancelButton)
                return CollisionBehavior.Cancel;

            // This should never happen
            throw new ArgumentOutOfRangeException();
        }

        [return: MaybeNull]
        private static FileInfo GetResult(FileInfo fileInfo, CollisionBehavior command)
        {
            return command switch
            {
                CollisionBehavior.Overwrite => fileInfo,
                CollisionBehavior.Rename => FilenameService.GetNonCollidingName(fileInfo),
                CollisionBehavior.Cancel => null,
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
            };
        }

        private static void Hyperlink_OnClicked(object sender, HyperlinkClickedEventArgs e)
        {
            FileService.OpenExplorerForFile(new FileInfo(e.Href));
        }
    }
}