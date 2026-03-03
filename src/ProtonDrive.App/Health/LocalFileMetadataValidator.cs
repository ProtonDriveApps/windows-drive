using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Health;

namespace ProtonDrive.App.Health;

internal sealed class LocalFileMetadataValidator
{
    private readonly IFileRevisionProvider<long> _fileRevisionProvider;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<LocalFileMetadataValidator> _logger;

    public LocalFileMetadataValidator(
        IFileRevisionProvider<long> fileRevisionProvider,
        IErrorCounter errorCounter,
        ILogger<LocalFileMetadataValidator> logger)
    {
        _fileRevisionProvider = fileRevisionProvider;
        _errorCounter = errorCounter;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(FileConsistencyGuardFileModel file, CancellationToken cancellationToken)
    {
        try
        {
            await using ((await _fileRevisionProvider.OpenFileForReadingAsync(file.Id, file.ContentVersion, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false))
            {
            }

            return true;
        }
        catch (FileRevisionProviderException ex) when (ex.ErrorCode is FileSystemErrorCode.Partial)
        {
            return true;
        }
        catch (FileRevisionProviderException ex)
        {
            HandleOpeningFileForReadingFailure(ex, file);
            return false;
        }
    }

    private static void MarkSkipped(FileConsistencyGuardFileModel file, FileConsistencyGuardFileReason reason)
    {
        file.Status = FileConsistencyGuardFileStatus.Skipped;
        file.Reason = reason;
        file.Error = FileConsistencyGuardFileError.None;
    }

    private static void MarkLocalDiverged(FileConsistencyGuardFileModel file)
    {
        file.Error = FileConsistencyGuardFileError.LocalDiverged;
    }

    private static void MarkRootDisabled(FileConsistencyGuardFileModel file)
    {
        file.Error = FileConsistencyGuardFileError.RootDisabled;
    }

    private static void MarkLocalFailed(FileConsistencyGuardFileModel file)
    {
        file.Error = FileConsistencyGuardFileError.LocalFailed;
    }

    private void HandleOpeningFileForReadingFailure(FileRevisionProviderException ex, FileConsistencyGuardFileModel file)
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
        else if (ex.ErrorCode is
                 FileSystemErrorCode.DirectoryNotFound or
                 FileSystemErrorCode.PathNotFound or
                 FileSystemErrorCode.IdentityMismatch or
                 FileSystemErrorCode.MetadataMismatch)
        {
            _logger.LogWarning("File consistency guard: Opening file with Id={Id} failed: {ErrorMessage}", file.Id, ex.CombinedMessage());
            MarkLocalDiverged(file);
            _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, ex);
        }
        else
        {
            _logger.LogWarning("File consistency guard: Opening file with Id={Id} failed: {ErrorMessage}", file.Id, ex.CombinedMessage());
            MarkLocalFailed(file);
            _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, ex);
        }
    }
}
