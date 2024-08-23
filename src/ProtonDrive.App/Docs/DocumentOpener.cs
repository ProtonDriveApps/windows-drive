using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Sync;
using ProtonDrive.App.Telemetry;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Diagnostics;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Telemetry;

namespace ProtonDrive.App.Docs;

public sealed class DocumentOpener
{
    private readonly UrlConfig _config;
    private readonly IRemoteIdsFromLocalPathProvider _remoteIdsFromLocalPathProvider;
    private readonly IOsProcesses _osProcesses;
    private readonly IAddressKeyProvider _addressKeyProvider;
    private readonly OpenedDocumentsCounters _counters;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<DocumentOpener> _logger;

    public DocumentOpener(
        UrlConfig config,
        IRemoteIdsFromLocalPathProvider remoteIdsFromLocalPathProvider,
        IOsProcesses osProcesses,
        IAddressKeyProvider addressKeyProvider,
        OpenedDocumentsCounters counters,
        IErrorCounter errorCounter,
        ILogger<DocumentOpener> logger)
    {
        _config = config;
        _remoteIdsFromLocalPathProvider = remoteIdsFromLocalPathProvider;
        _osProcesses = osProcesses;
        _addressKeyProvider = addressKeyProvider;
        _counters = counters;
        _errorCounter = errorCounter;
        _logger = logger;
    }

    public async Task TryOpenAsync(string documentPath, CancellationToken cancellationToken)
    {
        try
        {
            var remoteIds = await _remoteIdsFromLocalPathProvider.GetRemoteIdsOrDefaultAsync(documentPath, cancellationToken).ConfigureAwait(false);

            if (remoteIds is null)
            {
                _counters.IncrementFailures(documentPath);

                _logger.LogWarning("Failed to open document: could not get remote identity from local path");
                return;
            }

            var userDefaultAddress = await _addressKeyProvider.GetUserDefaultAddressAsync(cancellationToken).ConfigureAwait(false);

            var uriBuilder = new UriBuilder(_config.Docs)
            {
                Path = "doc",
                Query = $"volumeId={remoteIds.Value.VolumeId}&linkId={remoteIds.Value.LinkId}&email={userDefaultAddress.EmailAddress}",
            };

            _osProcesses.Open(uriBuilder.Uri.ToString());

            _counters.IncrementSuccesses(documentPath);
        }
        catch (Exception e)
        {
            _counters.IncrementFailures(documentPath);
            _errorCounter.Add(ErrorScope.DocumentOpening, e);

            _logger.LogError(
                "Unknown error when attempting to open document: {ExceptionType} {ErrorCode}",
                e.GetType().Name,
                e.GetRelevantFormattedErrorCode());
        }
    }
}
