using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using Ookii.Dialogs.Wpf;
using TwitchDownloaderCore.Tools;
using TwitchDownloaderWPF.Translations;

namespace TwitchDownloaderWPF.Services;

public static class FileCollisionService {

    private static CollisionCommand _collisionCommand = CollisionCommand.Prompt;

    [return: MaybeNull]
    public static FileInfo HandleCollisionCallback(FileInfo fileInfo, Window owner) {
        if (FileCollisionService._collisionCommand is not CollisionCommand.Prompt)
            return GetResult(fileInfo, FileCollisionService._collisionCommand);

        var result = ShowDialog(fileInfo, owner, out var rememberChoice);

        if (rememberChoice)
            FileCollisionService._collisionCommand = result;

        return GetResult(fileInfo, result);
    }

    private static CollisionCommand ShowDialog(FileInfo fileInfo, Window owner, out bool rememberChoice) {
        using var dialog = new TaskDialog();
        dialog.WindowTitle = Strings.TitleFileAlreadyExists;
        dialog.MainInstruction = string.Format(Strings.FileAlreadyExistsHeader, fileInfo.Name);
        dialog.Content = string.Format(
            Strings.FileAlreadyExistsBody,
            $"<a href=\"{fileInfo.FullName}\">{fileInfo.FullName}</a>"
        );
        dialog.MainIcon = TaskDialogIcon.Warning;

        dialog.EnableHyperlinks = true;
        dialog.HyperlinkClicked += Hyperlink_OnClicked;

        dialog.ButtonStyle = TaskDialogButtonStyle.CommandLinks;

        var overwriteButton = new TaskDialogButton(Strings.FileAlreadyExistsOverwrite);
        overwriteButton.CommandLinkNote = Strings.FileAlreadyExistsOverwriteDescription;
        dialog.Buttons.Add(overwriteButton);

        var renameButton = new TaskDialogButton(Strings.FileAlreadyExistsRename);
        renameButton.CommandLinkNote = Strings.FileAlreadyExistsRenameDescription;
        dialog.Buttons.Add(renameButton);

        var cancelButton = new TaskDialogButton(Strings.FileAlreadyExistsCancel);
        cancelButton.CommandLinkNote = Strings.FileAlreadyExistsCancelDescription;
        dialog.Buttons.Add(cancelButton);

        dialog.VerificationText = Strings.FileAlreadyExistsRememberMyChoice;
        dialog.IsVerificationChecked = false;

        var buttonResult = dialog.ShowDialog(owner);

        rememberChoice = dialog.IsVerificationChecked;

        if (buttonResult == overwriteButton)
            return CollisionCommand.Overwrite;

        if (buttonResult == renameButton)
            return CollisionCommand.Rename;

        if (buttonResult == cancelButton)
            return CollisionCommand.Cancel;

        // This should never happen
        throw new ArgumentOutOfRangeException();
    }

    [return: MaybeNull]
    private static FileInfo GetResult(FileInfo fileInfo, CollisionCommand command) {
        return command switch {
            CollisionCommand.Overwrite => fileInfo,
            CollisionCommand.Rename => FilenameService.GetNonCollidingName(fileInfo),
            CollisionCommand.Cancel => null,
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
        };
    }

    private static void Hyperlink_OnClicked(object sender, HyperlinkClickedEventArgs e) {
        FileService.OpenExplorerForFile(new(e.Href));
    }

    private enum CollisionCommand {
        Prompt,
        Overwrite,
        Rename,
        Cancel
    }
}
