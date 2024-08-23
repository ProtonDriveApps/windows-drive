using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Windows.Dialogs;
using ProtonDrive.App.Windows.Services;
using ProtonDrive.App.Windows.Views.Shared;

namespace ProtonDrive.App.Windows.Views.Main.Computers;

internal class SyncedFolderViewModel : ObservableObject, IEquatable<SyncFolder>, IMappingStatusViewModel
{
    private readonly SyncFolder _syncFolder;
    private readonly ILocalFolderService _localFolderService;
    private readonly IDialogService _dialogService;
    private readonly RemoveSyncFolderConfirmationViewModel _removeSyncFolderConfirmationViewModel;

    private readonly Func<SyncFolder, CancellationToken, Task> _removeSyncFolderAsync;
    private MappingSetupStatus _status;
    private MappingErrorCode _errorCode;

    public SyncedFolderViewModel(
        SyncFolder syncFolder,
        string name,
        ImageSource? icon,
        ILocalFolderService localFolderService,
        IDialogService dialogService,
        Func<SyncFolder, CancellationToken, Task> removeSyncFolderAsync)
    {
        _syncFolder = syncFolder;
        _localFolderService = localFolderService;
        _dialogService = dialogService;
        _removeSyncFolderAsync = removeSyncFolderAsync;
        _removeSyncFolderConfirmationViewModel = new RemoveSyncFolderConfirmationViewModel();

        Name = name;
        Icon = icon;
        Status = _syncFolder.Status;
        ErrorCode = _syncFolder.ErrorCode;

        OpenFolderCommand = new AsyncRelayCommand(OpenFolder);
        RemoveFolderCommand = new AsyncRelayCommand(RemoveSyncFolderAsync);
    }

    public string Path => _syncFolder.LocalPath;
    public string Name { get; }
    public ImageSource? Icon { get; }

    public ICommand OpenFolderCommand { get; }
    public ICommand RemoveFolderCommand { get; }

    public MappingSetupStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public MappingErrorCode ErrorCode
    {
        get => _errorCode;
        set => SetProperty(ref _errorCode, value);
    }

    public MappingErrorRenderingMode RenderingMode => MappingErrorRenderingMode.IconAndText;

    public bool Equals(SyncFolder? other)
    {
        // ReSharper disable once PossibleUnintendedReferenceComparison
        return other is not null && _syncFolder == other;
    }

    private async Task OpenFolder()
    {
        await _localFolderService.OpenFolderAsync(Path).ConfigureAwait(true);
    }

    private async Task RemoveSyncFolderAsync(CancellationToken cancellationToken)
    {
        _removeSyncFolderConfirmationViewModel.SetContentWithFolderName(Name);

        var confirmationResult = _dialogService.ShowConfirmationDialog(_removeSyncFolderConfirmationViewModel);

        if (confirmationResult != ConfirmationResult.Confirmed)
        {
            return;
        }

        Status = MappingSetupStatus.SettingUp;

        await _removeSyncFolderAsync(_syncFolder, cancellationToken).ConfigureAwait(true);
    }

    private sealed class RemoveSyncFolderConfirmationViewModel : ConfirmationDialogViewModelBase
    {
        private static readonly string ContentText =
            "Remove folder?" + Environment.NewLine +
            "Any files added to '@folderName' folder will no longer be synced and this folder will be moved to Trash in Proton Drive." + Environment.NewLine +
            "Existing files will remain on your computer.";

        public RemoveSyncFolderConfirmationViewModel()
            : base(
                title: "Proton Drive",
                message: ContentText,
                confirmButtonText: "Remove folder",
                cancelButtonText: "Cancel")
        {
        }

        public void SetContentWithFolderName(string folderName)
        {
            Message = ContentText.Replace("@folderName", folderName);
        }
    }
}
