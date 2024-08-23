using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Reporting;
using ProtonDrive.App.Settings;
using ProtonDrive.App.Sync;
using ProtonDrive.App.Telemetry;
using ProtonDrive.Client.Sanitization;
using ProtonDrive.DataAccess.Databases;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Sanitization;

internal sealed class FileSanitizationProvider : ISyncActivityAware
{
    private readonly AppConfig _appConfig;
    private readonly ClientInstanceSettings _settings;
    private readonly IDocumentSanitizationApiClient _documentSanitizationApiClient;
    private readonly SyncStatistics _syncStatistics;
    private readonly IErrorCounter _errorCounter;
    private readonly IErrorReporting _errorReporting;
    private readonly ILoggerFactory _loggerFactory;

    public FileSanitizationProvider(
        AppConfig appConfig,
        ClientInstanceSettings settings,
        IDocumentSanitizationApiClient documentSanitizationApiClient,
        SyncStatistics syncStatistics,
        IErrorCounter errorCounter,
        IErrorReporting errorReporting,
        ILoggerFactory loggerFactory)
    {
        _appConfig = appConfig;
        _settings = settings;
        _documentSanitizationApiClient = documentSanitizationApiClient;
        _syncStatistics = syncStatistics;
        _errorCounter = errorCounter;
        _errorReporting = errorReporting;
        _loggerFactory = loggerFactory;
    }

    public event EventHandler<SyncActivityItem<long>>? SyncActivityChanged;

    public FileSanitizer Create(
        RemoteAdapterDatabase remoteAdapterDatabase,
        LocalAdapterDatabase localAdapterDatabase,
        IReadOnlyCollection<RemoteToLocalMapping> mappings,
        Func<long, CancellationToken, Task<bool>> tryMarkNodeAsDirtyAsync)
    {
        return new FileSanitizer(
            _appConfig,
            _settings,
            _documentSanitizationApiClient,
            mappings,
            tryMarkNodeAsDirtyAsync,
            remoteAdapterDatabase,
            localAdapterDatabase,
            this,
            _errorReporting,
            _syncStatistics,
            _errorCounter,
            _loggerFactory.CreateLogger<FileSanitizer>());
    }

    public void OnSyncActivityChanged(SyncActivityItem<long> item)
    {
        SyncActivityChanged?.Invoke(this, item);
    }
}
