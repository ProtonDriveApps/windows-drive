using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Logging;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Integration;

internal sealed class OpenByFileIdSupportVerifier : IOpenByFileIdSupportVerifier
{
    private readonly ConcurrentDictionary<int, byte> _failedVolumeSerialNumbers = new();

    private readonly ILogger<OpenByFileIdSupportVerifier> _logger;

    public OpenByFileIdSupportVerifier(ILogger<OpenByFileIdSupportVerifier> logger)
    {
        _logger = logger;
    }

    public bool SupportsOpenByFileId(FileSystemObject folder, int volumeSerialNumber)
    {
        // Verification is cheap when it succeeds, so it runs for every folder. It can be slow to fail on network
        // or filter driven volumes, so failures are remembered per volume and not retried. A failure can be object
        // specific (restrictive ACL, sharing violation) or transient, so this under-reports support for the whole
        // volume until restart. Acceptable for diagnostics, not for control flow.
        if (_failedVolumeSerialNumbers.ContainsKey(volumeSerialNumber))
        {
            return false;
        }

        try
        {
            folder.VerifyOpenByFileIdSupport();
            return true;
        }
        catch (Exception ex)
        {
            // Anything raised here is a failure to verify. This diagnostic can never affect the outcome of the operation that requested it.
            var pathToLog = _logger.GetSensitiveValueForLogging(folder.FullPath);
            _logger.LogWarning(
                "Failed to verify open by file ID for path \"{Path}\": {ExceptionType} {ErrorCode}",
                pathToLog,
                ex.GetType().Name,
                ex.GetRelevantFormattedErrorCode());

            _failedVolumeSerialNumbers.TryAdd(volumeSerialNumber, value: 0);

            return false;
        }
    }
}
