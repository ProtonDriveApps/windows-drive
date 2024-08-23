using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.Windows.SystemIntegration;

namespace ProtonDrive.App.Windows.Views.Main.Computers;

internal sealed class AddFoldersViewModel : ObservableObject, IDialogViewModel
{
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ISyncFolderService _syncFolderService;
    private readonly AddedFolderValidationResultMessageBuilder _messageBuilder;
    private readonly IAsyncRelayCommand _saveCommand;
    private readonly IRelayCommand _selectArbitraryFolderCommand;

    private bool _syncFoldersSaved;
    private bool _isSaving;
    private SyncFolderValidationResult _folderValidationResult;
    private string? _errorMessage;

    public AddFoldersViewModel(
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ISyncFolderService syncFolderService,
        AddedFolderValidationResultMessageBuilder messageBuilder)
    {
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _syncFolderService = syncFolderService;
        _messageBuilder = messageBuilder;

        foreach (var knownFolder in KnownFolders.IdsByPath)
        {
            TryAddFolder(knownFolder.Key, isChecked: false);
        }

        _selectArbitraryFolderCommand = new RelayCommand(SelectArbitraryFolder, CanSelectArbitraryFolder);
        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);

        SyncFolders.CollectionChanged += OnSyncFoldersCollectionChanged;
    }

    string? IDialogViewModel.Title => default;

    public bool SyncFoldersSaved
    {
        get => _syncFoldersSaved;
        private set => SetProperty(ref _syncFoldersSaved, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value))
            {
                _saveCommand.NotifyCanExecuteChanged();
                _selectArbitraryFolderCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public SyncFolderValidationResult FolderValidationResult
    {
        get => _folderValidationResult;
        private set
        {
            if (SetProperty(ref _folderValidationResult, value))
            {
                _saveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand SelectArbitraryFolderCommand => _selectArbitraryFolderCommand;

    public ICommand SaveCommand => _saveCommand;

    public ObservableCollection<SelectableFolderViewModel> SyncFolders { get; } = new();

    public void InitializeSelection()
    {
        var arbitraryFoldersToRemove = new List<SelectableFolderViewModel>();

        foreach (var syncedFolder in SyncFolders)
        {
            if (KnownFolders.IdsByPath.All(group => group.Key != syncedFolder.Path))
            {
                arbitraryFoldersToRemove.Add(syncedFolder);
            }
            else
            {
                syncedFolder.IsChecked = KnownFolders.IdsByPath.Any(group => group.Any(id => id == KnownFolders.Documents && group.Key == syncedFolder.Path));
            }
        }

        foreach (var folderToRemove in arbitraryFoldersToRemove)
        {
            SyncFolders.Remove(folderToRemove);
        }
    }

    public void RefreshSyncedFolders(HashSet<string> syncedFolderPaths)
    {
        foreach (var folder in SyncFolders)
        {
            if (syncedFolderPaths.Contains(folder.Path))
            {
                folder.IsChecked = true;
                folder.IsDisabled = true;
            }
        }
    }

    private bool CanSelectArbitraryFolder() => !IsSaving;

    private void OnSelectedFolderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableFolderViewModel.IsChecked))
        {
            FolderValidationResult = ValidateFolderSelection();
        }
    }

    private void SelectArbitraryFolder()
    {
        var folderPickingDialog = new OpenFolderDialog
        {
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };

        var result = folderPickingDialog.ShowDialog();

        if (result is not true)
        {
            return;
        }

        var selectedFolder = SyncFolders.FirstOrDefault(x => x.Path == folderPickingDialog.FolderName);

        if (selectedFolder is not null)
        {
            selectedFolder.IsChecked = true;
            return;
        }

        if (TryAddFolder(folderPickingDialog.FolderName, isChecked: true))
        {
            FolderValidationResult = ValidateFolderSelection();
        }
    }

    private bool TryAddFolder(string folderPath, bool isChecked)
    {
        if (!SelectableFolderViewModel.TryCreate(
                folderPath,
                isChecked,
                _fileSystemDisplayNameAndIconProvider,
                out var folder))
        {
            return false;
        }

        folder.PropertyChanged += OnSelectedFolderPropertyChanged;
        SyncFolders.Add(folder);
        return true;
    }

    private bool CanSave() => !IsSaving && FolderValidationResult == SyncFolderValidationResult.Succeeded;

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsSaving = true;

        var paths = SyncFolders.Where(x => x.IsChecked && !x.IsDisabled).Select(x => x.Path).ToList();
        await _syncFolderService.AddHostDeviceFoldersAsync(paths, cancellationToken).ConfigureAwait(true);

        SyncFoldersSaved = true;
        IsSaving = false;
    }

    private void OnSyncFoldersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        FolderValidationResult = ValidateFolderSelection();
    }

    private SyncFolderValidationResult ValidateFolderSelection()
    {
        var selectedFolders = SyncFolders.Where(x => x.IsChecked).ToList();

        var result = SyncFolderValidationResult.Succeeded;

        foreach (var (index, folder) in selectedFolders.Select((x, i) => (i, x)))
        {
            var otherPaths = selectedFolders.Select(x => x.Path).Where((_, i) => i != index).ToHashSet();

            folder.ValidationResult = _syncFolderService.ValidateSyncFolder(folder.Path, otherPaths);

            if (folder.ValidationResult is not SyncFolderValidationResult.Succeeded)
            {
                result = folder.ValidationResult;
            }
        }

        ErrorMessage = _messageBuilder.BuildErrorMessage(SyncFolders);

        return result;
    }
}
