using System.Text;
using Proton.Drive.Sdk.Sync.Agent.Mapping;

namespace Proton.Drive.App.Windows.Views.Main.MyComputer;

internal sealed class AddFoldersValidationResultMessageBuilder
{
    public string? BuildErrorMessage(IReadOnlyCollection<SelectableFolderViewModel> syncFolders)
    {
        var foldersByValidationResult = syncFolders
            .Where(x => x is { IsChecked: true, IsDisabled: false })
            .GroupBy(x => x.ValidationResult)
            .Select(x => new { Result = x.Key, Folders = x })
            .ToDictionary(x => x.Result, y => y.Folders.ToList());

        var numberOfValidFolders = foldersByValidationResult.GetValueOrDefault(SyncFolderValidationResult.Succeeded)?.Count ?? 0;

        var numberOfInvalidFolders = foldersByValidationResult.Values.Sum(x => x.Count) - numberOfValidFolders;

        if (numberOfInvalidFolders == 0)
        {
            return null;
        }

        return numberOfInvalidFolders == 1
            ? BuildSingleErrorMessage(foldersByValidationResult)
            : BuildMultipleErrorMessages(foldersByValidationResult, numberOfInvalidFolders);
    }

    private static string BuildSingleErrorMessage(IReadOnlyDictionary<SyncFolderValidationResult, List<SelectableFolderViewModel>> info)
    {
        var defaultErrorMessage = Resources.Strings.Main_MyComputer_Folders_AddFolders_ValidationError_UnableToAddOneFolder;

        if (info.ContainsKey(SyncFolderValidationResult.NonSyncableFolder))
        {
            return $"{defaultErrorMessage}: {Resources.Strings.SyncFolderValidationResult_Value_NonSyncableFolder}";
        }

        if (info.ContainsKey(SyncFolderValidationResult.FolderIncludedByAnAlreadySyncedFolder))
        {
            return $"{defaultErrorMessage}: {Resources.Strings.SyncFolderValidationResult_Value_FolderIncludedByAnAlreadySyncedFolder}";
        }

        if (info.ContainsKey(SyncFolderValidationResult.FolderIncludesAnAlreadySyncedFolder))
        {
            return $"{defaultErrorMessage}: {Resources.Strings.SyncFolderValidationResult_Value_FolderIncludesAnAlreadySyncedFolder}";
        }

        if (info.ContainsKey(SyncFolderValidationResult.LocalVolumeNotSupported))
        {
            return $"{defaultErrorMessage}: {Resources.Strings.SyncFolderValidationResult_Value_LocalVolumeNotSupported}";
        }

        if (info.ContainsKey(SyncFolderValidationResult.LocalFolderDoesNotExist))
        {
            return $"{defaultErrorMessage}: {Resources.Strings.SyncFolderValidationResult_Value_LocalFolderDoesNotExist}";
        }

        if (info.ContainsKey(SyncFolderValidationResult.LocalFileSystemAccessFailed))
        {
            return $"{defaultErrorMessage}: {Resources.Strings.SyncFolderValidationResult_Value_LocalFileSystemAccessFailed}";
        }

        if (info.ContainsKey(SyncFolderValidationResult.NetworkFolderNotSupported))
        {
            return $"{defaultErrorMessage}: {Resources.Strings.SyncFolderValidationResult_Value_NetworkFolderNotSupported}";
        }

        return defaultErrorMessage;
    }

    private static string BuildMultipleErrorMessages(
        IReadOnlyDictionary<SyncFolderValidationResult, List<SelectableFolderViewModel>> info,
        int numberOfInvalidFolders)
    {
        var genericError = string.Format(Resources.Strings.Main_MyComputer_Folders_AddFolders_ValidationError_UnableToAddFoldersFormat, numberOfInvalidFolders);
        var messageBuilder = new StringBuilder($"{genericError}: ");
        var oneMessageHasAlreadyBeenAppended = false;

        if (info.TryGetValue(SyncFolderValidationResult.NonSyncableFolder, out var folders))
        {
            var count = folders.Count;
            var errorMessage = string.Format(Resources.Strings.Main_MyComputer_Folders_AddFolders_ValidationError_NonSyncableFolderFormat, count);
            messageBuilder.Append(errorMessage);
            oneMessageHasAlreadyBeenAppended = true;
        }

        if (info.TryGetValue(SyncFolderValidationResult.FolderIncludedByAnAlreadySyncedFolder, out folders))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = folders.Count;
            var errorMessage = string.Format(Resources.Strings.Main_MyComputer_Folders_AddFolders_ValidationError_FolderIncludedByAnAlreadySyncedFolderFormat, count);
            messageBuilder.Append(errorMessage);
            oneMessageHasAlreadyBeenAppended = true;
        }

        if (info.TryGetValue(SyncFolderValidationResult.FolderIncludesAnAlreadySyncedFolder, out folders))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = folders.Count;
            var errorMessage = string.Format(Resources.Strings.Main_MyComputer_Folders_AddFolders_ValidationError_FolderIncludesAnAlreadySyncedFolderFormat, count);
            messageBuilder.Append(errorMessage);
            oneMessageHasAlreadyBeenAppended = true;
        }

        if (info.TryGetValue(SyncFolderValidationResult.LocalVolumeNotSupported, out folders))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = folders.Count;
            var errorMessage = string.Format(Resources.Strings.Main_MyComputer_Folders_AddFolders_ValidationError_LocalVolumeNotSupportedFormat, count);
            messageBuilder.Append(errorMessage);
            oneMessageHasAlreadyBeenAppended = true;
        }

        if (info.TryGetValue(SyncFolderValidationResult.LocalFolderDoesNotExist, out folders))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = folders.Count;
            var errorMessage = string.Format(Resources.Strings.Main_MyComputer_Folders_AddFolders_ValidationError_LocalFolderDoesNotExistFormat, count);
            messageBuilder.Append(errorMessage);
            oneMessageHasAlreadyBeenAppended = true;
        }

        if (info.TryGetValue(SyncFolderValidationResult.LocalFileSystemAccessFailed, out folders))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = folders.Count;
            var errorMessage = string.Format(Resources.Strings.Main_MyComputer_Folders_AddFolders_ValidationError_LocalFileSystemAccessFailedFormat, count);
            messageBuilder.Append(errorMessage);
            oneMessageHasAlreadyBeenAppended = true;
        }

        if (info.TryGetValue(SyncFolderValidationResult.NetworkFolderNotSupported, out folders))
        {
            if (oneMessageHasAlreadyBeenAppended)
            {
                messageBuilder.Append(", ");
            }

            var count = folders.Count;
            var errorMessage = string.Format(Resources.Strings.Main_MyComputer_Folders_AddFolders_ValidationError_NetworkFolderNotSupportedFormat, count);
            messageBuilder.Append(errorMessage);
        }

        messageBuilder.Append('.');

        return messageBuilder.ToString();
    }
}
