using CommunityToolkit.Mvvm.Input;
using Proton.Drive.App.Windows.SystemIntegration;
using Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;
using Proton.Drive.Sdk.Sync.Client.Shares.SharedWithMe;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

namespace Proton.Drive.App.Windows.Views.Main.SharedWithMe;

internal sealed class SharedWithMeItemViewModelFactory
{
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ILocalFolderService _localFolderService;

    public SharedWithMeItemViewModelFactory(
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ILocalFolderService localFolderService)
    {
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _localFolderService = localFolderService;
    }

    public SharedWithMeItemViewModel Create(
        SharedWithMeItem dataItem,
        IAsyncRelayCommand<SharedWithMeItemViewModel> toggleSyncCommand,
        IAsyncRelayCommand removeMeCommand)
    {
        return new SharedWithMeItemViewModel(
            _fileSystemDisplayNameAndIconProvider,
            _localFolderService,
            toggleSyncCommand,
            removeMeCommand)
        {
            DataItem = dataItem,
        };
    }

    public SharedWithMeItemViewModel Create(
        SyncFolder syncFolder,
        IAsyncRelayCommand<SharedWithMeItemViewModel> toggleSyncCommand,
        IAsyncRelayCommand removeMeCommand)
    {
        return new SharedWithMeItemViewModel(
            _fileSystemDisplayNameAndIconProvider,
            _localFolderService,
            toggleSyncCommand,
            removeMeCommand)
        {
            SyncFolder = syncFolder,
        };
    }
}
