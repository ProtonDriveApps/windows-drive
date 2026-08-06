using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Proton.Drive.App.SystemIntegration;
using Proton.Drive.App.Windows.SystemIntegration;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;

namespace Proton.Drive.App.Windows.Views.Main.MyComputer;

internal sealed class AddFoldersViewModel : ObservableObject, IDialogViewModel
{
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ISyncFolderService _syncFolderService;
    private readonly IKnownFolders _knownFolders;
    private readonly AddFoldersValidationResultMessageBuilder _messageBuilder;
    private readonly ILogger<AddFoldersViewModel> _logger;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly RelayCommand _selectArbitraryFolderCommand;

    private bool _syncFoldersSaved;
    private bool _isSaving;
    private bool _isInitializingSelection;
    private SyncFolderValidationResult _folderValidationResult;
    private string? _errorMessage;
    private bool _newFolderSelected;

    public AddFoldersViewModel(
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ISyncFolderService syncFolderService,
        IKnownFolders knownFolders,
        AddFoldersValidationResultMessageBuilder messageBuilder,
        ILogger<AddFoldersViewModel> logger)
    {
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _syncFolderService = syncFolderService;
        _knownFolders = knownFolders;
        _messageBuilder = messageBuilder;
        _logger = logger;

        foreach (var knownFolder in _knownFolders.IdsByPath)
        {
            TryAddFolder(knownFolder.Key, isChecked: false);
        }

        _selectArbitraryFolderCommand = new RelayCommand(SelectArbitraryFolder, CanSelectArbitraryFolder);
        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);

        SyncFolders.CollectionChanged += OnSyncFoldersCollectionChanged;
    }

    string? IDialogViewModel.Title => null;

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

    public IAsyncRelayCommand SaveCommand => _saveCommand;

    public ObservableCollection<SelectableFolderViewModel> SyncFolders { get; } = [];

    private bool NewFolderSelected
    {
        get => _newFolderSelected;
        set
        {
            if (SetProperty(ref _newFolderSelected, value))
            {
                _saveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public void InitializeSelection()
    {
        _isInitializingSelection = true;

        var arbitraryFoldersToRemove = new List<SelectableFolderViewModel>();

        foreach (var syncedFolder in SyncFolders)
        {
            if (_knownFolders.IdsByPath.All(group => group.Key != syncedFolder.Path))
            {
                arbitraryFoldersToRemove.Add(syncedFolder);
            }
            else
            {
                syncedFolder.IsChecked = _knownFolders.IdsByPath.Any(group => group.Any(id => id == _knownFolders.Documents && group.Key == syncedFolder.Path));
            }
        }

        foreach (var folderToRemove in arbitraryFoldersToRemove)
        {
            SyncFolders.Remove(folderToRemove);
        }

        _isInitializingSelection = false;

        ValidateFolderSelection();
    }

    public void RefreshSyncedFolders(HashSet<string> syncedFolderPaths)
    {
        _isInitializingSelection = true;

        foreach (var folder in SyncFolders.Where(x => syncedFolderPaths.Contains(x.Path)))
        {
            folder.IsChecked = true;
            folder.IsDisabled = true;
        }

        NewFolderSelected = false;

        _isInitializingSelection = false;
    }

    private bool CanSelectArbitraryFolder() => !IsSaving;

    private void OnSelectedFolderPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableFolderViewModel.IsChecked))
        {
            ValidateFolderSelection();
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
            ValidateFolderSelection();
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

    private bool CanSave() => !IsSaving && FolderValidationResult == SyncFolderValidationResult.Succeeded && NewFolderSelected;

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsSaving = true;

        var paths = SyncFolders.Where(x => x is { IsChecked: true, IsDisabled: false }).Select(x => x.Path).ToList();

        if (paths.Count > 0)
        {
            await _syncFolderService.AddHostDeviceFoldersAsync(paths, cancellationToken).ConfigureAwait(true);
        }

        SyncFoldersSaved = true;
        NewFolderSelected = false;
        IsSaving = false;
    }

    private void OnSyncFoldersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ValidateFolderSelection();
    }

    private void ValidateFolderSelection()
    {
        if (_isInitializingSelection)
        {
            return;
        }

        var selectedFolders = SyncFolders.Where(x => x is { IsChecked: true, IsDisabled: false }).ToList();

        NewFolderSelected = selectedFolders.Count != 0;

        var result = SyncFolderValidationResult.Succeeded;

        foreach (var (index, folder) in selectedFolders.Select((x, i) => (i, x)))
        {
            var otherPaths = selectedFolders.Select(x => x.Path).Where((_, i) => i != index);

            folder.ValidationResult = _syncFolderService.ValidateSyncFolder(folder.Path, otherPaths);

            if (folder.ValidationResult is not SyncFolderValidationResult.Succeeded)
            {
                result = folder.ValidationResult;
            }
        }

        ErrorMessage = _messageBuilder.BuildErrorMessage(SyncFolders);

        if (result is not SyncFolderValidationResult.Succeeded)
        {
            _logger.LogWarning("Folder selection validation failed due to {ErrorType}: {Message}", result, ErrorMessage);
        }

        FolderValidationResult = result;
    }
}
