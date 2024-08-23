using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.Client.Shares.SharedWithMe;

namespace ProtonDrive.App.Windows.Views.Main.SharedWithMe;

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
