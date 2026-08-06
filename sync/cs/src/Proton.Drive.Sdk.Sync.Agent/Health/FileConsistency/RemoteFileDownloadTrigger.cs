using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter;
using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Health;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

internal sealed class RemoteFileDownloadTrigger
{
    private readonly IManagedAdapter<long> _remoteAdapter;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<RemoteFileDownloadTrigger> _logger;

    public RemoteFileDownloadTrigger(
        IManagedAdapter<long> remoteAdapter,
        IErrorCounter errorCounter,
        ILogger<RemoteFileDownloadTrigger> logger)
    {
        _remoteAdapter = remoteAdapter;
        _errorCounter = errorCounter;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(FileConsistencyGuardFileModel file, CancellationToken cancellationToken)
    {
        try
        {
            await _remoteAdapter.TriggerPseudoFileEditAsync(file.Id, file.ContentVersion, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (FileRevisionProviderException ex)
        {
            HandleTriggeringFileDownloadFailure(ex, file);
            return false;
        }
    }

    private static void MarkSkipped(FileConsistencyGuardFileModel file, FileConsistencyGuardFileReason reason)
    {
        file.Status = FileConsistencyGuardFileStatus.Skipped;
        file.Reason = reason;
        file.Error = FileConsistencyGuardFileError.None;
    }

    private static void MarkRootDisabled(FileConsistencyGuardFileModel file)
    {
        file.Error = FileConsistencyGuardFileError.RootDisabled;
    }

    private static void MarkFailed(FileConsistencyGuardFileModel file)
    {
        file.Error = FileConsistencyGuardFileError.RemoteFailed;
    }

    private void HandleTriggeringFileDownloadFailure(FileRevisionProviderException ex, FileConsistencyGuardFileModel file)
    {
        if (ex.ProviderErrorCode is FileRevisionProviderErrorCode.RootDisabled)
        {
            MarkRootDisabled(file);
        }
        else if (ex.ProviderErrorCode is FileRevisionProviderErrorCode.NotAFile
                 || ex.ErrorCode is FileSystemErrorCode.ObjectNotFound)
        {
            MarkSkipped(file, FileConsistencyGuardFileReason.Deleted);
        }
        else if (ex.ProviderErrorCode is FileRevisionProviderErrorCode.ContentVersionDiverged)
        {
            MarkSkipped(file, FileConsistencyGuardFileReason.Updated);
        }
        else
        {
            _logger.LogWarning("File consistency guard: Triggering pseudo edit of file with Id={Id} failed: {ErrorMessage}", file.Id, ex.CombinedMessage());
            MarkFailed(file);
            _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, ex);
        }
    }
}
