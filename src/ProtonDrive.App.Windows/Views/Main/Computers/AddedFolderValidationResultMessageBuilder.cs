using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtonDrive.App.Mapping;

namespace ProtonDrive.App.Windows.Views.Main.Computers;

internal sealed class AddedFolderValidationResultMessageBuilder
{
    public string? BuildErrorMessage(IReadOnlyCollection<SelectableFolderViewModel> syncFolders)
    {
        var foldersByValidationResult = syncFolders
            .Where(x => x.IsChecked && !x.IsDisabled)
            .GroupBy(x => x.ValidationResult)
            .Select(x => new { Result = x.Key, Folders = x })
            .ToDictionary(x => x.Result, y => y.Folders.ToList());

        var numberOfValidFolders = foldersByValidationResult.ContainsKey(SyncFolderValidationResult.Succeeded)
            ? foldersByValidationResult[SyncFolderValidationResult.Succeeded].Count
            : 0;

        var numberOfInvalidFolders = foldersByValidationResult.Values.Sum(x => x.Count) - numberOfValidFolders;

        if (numberOfInvalidFolders == 0)
        {
            return default;
        }

        return numberOfInvalidFolders == 1
            ? BuildSingleErrorMessage(foldersByValidationResult)
            : BuildMultipleErrorMessages(foldersByValidationResult, numberOfInvalidFolders);
    }

    private static string BuildSingleErrorMessage(IReadOnlyDictionary<SyncFolderValidationResult, List<SelectableFolderViewModel>> info)
    {
        if (info.ContainsKey(SyncFolderValidationResult.NonSyncableFolder))
        {
            return "Unable to add 1 folder: It is a system folder that can't be synced.";
        }

        if (info.ContainsKey(SyncFolderValidationResult.FolderIncludedByAnAlreadySyncedFolder))
        {
            return "Unable to add 1 folder: It is a subfolder of a folder synced to Proton Drive.";
        }

        if (info.ContainsKey(SyncFolderValidationResult.FolderIncludesAnAlreadySyncedFolder))
        {
            return "Unable to add 1 folder: It has a subfolder synced to Proton Drive.";
        }

        if (info.ContainsKey(SyncFolderValidationResult.LocalVolumeNotSupported))
        {
            return "Unable to add 1 folder: Drive not supported.";
        }

        if (info.ContainsKey(SyncFolderValidationResult.LocalFolderDoesNotExist))
        {
            return "Unable to add 1 folder: It does not exist or cannot be opened.";
        }

        if (info.ContainsKey(SyncFolderValidationResult.LocalFileSystemAccessFailed))
        {
            return "Unable to add 1 folder: It cannot be accessed.";
        }

        if (info.ContainsKey(SyncFolderValidationResult.NetworkFolderNotSupported))
        {
            return "Unable to add 1 folder: Syncing folders from network drives is not supported.";
        }

        throw new InvalidOperationException("Error message cannot be build.");
    }

    private static string BuildMultipleErrorMessages(
        IReadOnlyDictionary<SyncFolderValidationResult, List<SelectableFolderViewModel>> info,
        int numberOfInvalidFolders)
    {
        var messageBuilder = new StringBuilder($"Unable to add {numberOfInvalidFolders} folder{(numberOfInvalidFolders == 1 ? string.Empty : "s")}: ");
        var oneMessageHasAlreadyBeenAppended = false;

        if (info.ContainsKey(SyncFolderValidationResult.NonSyncableFolder))
        {
            var count = info[SyncFolderValidationResult.NonSyncableFolder].Count;
            messageBuilder
                .Append(count == 1 ? "1 folder is a system folder" : $"{count} folders are system folders")
                .Append(" that can't be synced");
            oneMessageHasAlreadyBeenAppended = true;
        }

        if (info.ContainsKey(SyncFolderValidationResult.FolderIncludedByAnAlreadySyncedFolder))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = info[SyncFolderValidationResult.FolderIncludedByAnAlreadySyncedFolder].Count;
            messageBuilder
                .Append(count == 1 ? "1 is a subfolder" : $"{count} folders are subfolders")
                .Append(" of a folder synced to Proton Drive");
            oneMessageHasAlreadyBeenAppended = true;
        }

        if (info.ContainsKey(SyncFolderValidationResult.FolderIncludesAnAlreadySyncedFolder))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = info[SyncFolderValidationResult.FolderIncludesAnAlreadySyncedFolder].Count;
            messageBuilder
                .Append(count == 1 ? "1 has" : $"{count} folders have")
                .Append(" a subfolder synced to Proton Drive");
            oneMessageHasAlreadyBeenAppended = true;
        }

        if (info.ContainsKey(SyncFolderValidationResult.LocalVolumeNotSupported))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = info[SyncFolderValidationResult.LocalVolumeNotSupported].Count;
            messageBuilder
                .Append(count == 1 ? "1 is" : $"{count} folders are")
                .Append(" located on a non-supported Drive");
            oneMessageHasAlreadyBeenAppended = true;
        }

        if (info.ContainsKey(SyncFolderValidationResult.LocalFolderDoesNotExist))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = info[SyncFolderValidationResult.LocalFolderDoesNotExist].Count;
            messageBuilder
                .Append(count == 1 ? "1 does" : $"{count} folders do")
                .Append(" not exist or cannot be opened");
            oneMessageHasAlreadyBeenAppended = true;
        }

        if (info.ContainsKey(SyncFolderValidationResult.LocalFileSystemAccessFailed))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = info[SyncFolderValidationResult.LocalFileSystemAccessFailed].Count;
            messageBuilder
                .Append(count == 1 ? "1" : $"{count} folders")
                .Append(" cannot be accessed");
        }

        if (info.ContainsKey(SyncFolderValidationResult.NetworkFolderNotSupported))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = info[SyncFolderValidationResult.NetworkFolderNotSupported].Count;
            messageBuilder
                .Append(count == 1 ? "1" : $"{count} folders")
                .Append(" cannot be synced because network folders are not supported");
        }

        messageBuilder.Append('.');

        return messageBuilder.ToString();
    }
}
