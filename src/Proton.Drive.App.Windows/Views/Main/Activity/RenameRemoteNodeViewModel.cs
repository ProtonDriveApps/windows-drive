using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Proton.Drive.App.Windows.Toolkit;
using Proton.Drive.App.Windows.Toolkit.Converters;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Sdk.Sync.Windows.FileSystem;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.App.Windows.Views.Main.Activity;

internal sealed class RenameRemoteNodeViewModel : ObservableValidator, IDialogViewModel, ISyncStateAware, IDeferredValidationResolver
{
    private readonly ISyncService _syncService;
    private readonly IRemoteIdsFromNodeIdProvider _remoteIdsProvider;
    private readonly IRemoteFileSystemClientFactory _remoteFileSystemClientFactory;
    private readonly INumberSuffixedNameGenerator _numberSuffixedNameGenerator;
    private readonly ILogger<RenameRemoteNodeViewModel> _logger;

    private SyncActivityItemViewModel? _syncActivityItem;
    private SyncState _syncState = SyncState.Terminated;
    private string? _newName;
    private FileSystemErrorCode? _errorCode;
    private bool _hasClosingBeenRequested;

    public RenameRemoteNodeViewModel(
        ISyncService syncService,
        IRemoteIdsFromNodeIdProvider remoteIdsProvider,
        IRemoteFileSystemClientFactory remoteFileSystemClientFactory,
        INumberSuffixedNameGenerator numberSuffixedNameGenerator,
        ILogger<RenameRemoteNodeViewModel> logger)
    {
        _syncService = syncService;
        _remoteIdsProvider = remoteIdsProvider;
        _remoteFileSystemClientFactory = remoteFileSystemClientFactory;
        _numberSuffixedNameGenerator = numberSuffixedNameGenerator;
        _logger = logger;

        RenameCommand = new AsyncRelayCommand(RenameAsync, CanRename);
    }

    public string? Title { get; } = Resources.Strings.Main_Activity_Rename_Title;

    public string OriginalName => _syncActivityItem?.Name ?? string.Empty;

    public FileSystemErrorCode? ErrorCode
    {
        get => _errorCode;
        private set => SetProperty(ref _errorCode, value);
    }

    [DeferredValidation]
    public string? NewName
    {
        get => _newName;
        set
        {
            if (SetProperty(ref _newName, value))
            {
                ErrorCode = null;
                ValidateAllProperties();
                RenameCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand RenameCommand { get; }

    // TODO: Move this property in IDialogViewModel
    public bool HasClosingBeenRequested
    {
        get => _hasClosingBeenRequested;
        private set => SetProperty(ref _hasClosingBeenRequested, value);
    }

    private SyncActivityItemViewModel SyncActivityItem => _syncActivityItem ?? throw new InvalidOperationException($"{nameof(SyncActivityItem)} is not set");

    void ISyncStateAware.OnSyncStateChanged(SyncState value)
    {
        _syncState = value;
    }

    ValidationResult? IDeferredValidationResolver.Validate(string? memberName)
    {
        return memberName switch
        {
            nameof(NewName) => ValidateSanitizedName(NewName),
            _ => ValidationResult.Success,
        };
    }

    public void SetSyncActivityItem(SyncActivityItemViewModel item)
    {
        HasClosingBeenRequested = false;
        ErrorCode = null;

        _syncActivityItem = item;
        OnPropertyChanged(nameof(OriginalName));

        NewName = _numberSuffixedNameGenerator
            .GenerateNames(_syncActivityItem.Name, _syncActivityItem.NodeType is NodeType.Directory ? NameType.Folder : NameType.File)
            .First();
    }

    public bool CanRename()
    {
        return _syncActivityItem is not null && !string.IsNullOrEmpty(NewName) && ValidateSanitizedName(NewName) == ValidationResult.Success;
    }

    public async Task RenameAsync(CancellationToken cancellationToken)
    {
        if (SyncActivityItem.RootId is null)
        {
            _logger.LogError("Failed to rename remote node: unknown mapping root ID");
            return;
        }

        var remoteIds = await _remoteIdsProvider
            .GetRemoteIdsOrDefaultAsync(SyncActivityItem.RootId.Value, SyncActivityItem.DataItem.Id, cancellationToken)
            .ConfigureAwait(false);

        if (!remoteIds.HasValue)
        {
            _logger.LogError("Failed to rename remote node: unknown remote node link ID");
            return;
        }

        if (_syncState.Status is not SyncStatus.Synchronizing)
        {
            _syncService.PauseUntil(time: null);
        }

        try
        {
            var clientParameters = new FileSystemClientParameters(remoteIds.Value.VolumeId, remoteIds.Value.ShareId);
            var client = _remoteFileSystemClientFactory.CreateClient(clientParameters);

            var nodeInfo = (SyncActivityItem.NodeType is NodeType.Directory ? NodeInfo<string>.Directory() : NodeInfo<string>.File())
                .WithId(remoteIds.Value.LinkId)
                .WithName(OriginalName);

            // The null-forgiving operator (!) is safe here as the CanExecute method guarantees this value is not null.
            await client.RenameAsync(nodeInfo, NewName!, cancellationToken).ConfigureAwait(true);

            HasClosingBeenRequested = true;
        }
        catch (FileSystemClientException ex)
        {
            ErrorCode = ex.ErrorCode;
            _logger.LogError("Failed to rename remote node with ID=\"{LinkId}\": {ErrorMessage}", remoteIds.Value.LinkId, ex.CombinedMessage());
            ValidateAllProperties();
        }
        finally
        {
            _syncService.Resume();
        }
    }

    private ValidationResult? ValidateSanitizedName(string? name)
    {
        if (ErrorCode is not null)
        {
            return _errorCode switch
            {
                FileSystemErrorCode.DuplicateName => new ValidationResult(Resources.Strings.Main_Activity_Rename_NewName_Error_AlreadyExists),
                _ => new ValidationResult(Resources.Strings.Main_Activity_Rename_NewName_Error_Failed),
            };
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return new ValidationResult(Resources.Strings.Main_Activity_Rename_NewName_Error_Required);
        }

        var validationResult = FileSystemObject.ValidateName(name);

        var errorMessage = EnumToDisplayTextConverter.Convert(validationResult) ?? Resources.Strings.Main_Activity_Rename_NewName_Error_ContainsInvalidCharacter;

        return validationResult is FileSystemNameValidationResult.Valid
            ? ValidationResult.Success
            : new ValidationResult(errorMessage);
    }
}
