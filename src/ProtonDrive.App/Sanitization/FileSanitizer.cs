using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Reporting;
using ProtonDrive.App.Settings;
using ProtonDrive.App.Telemetry;
using ProtonDrive.Client.Sanitization;
using ProtonDrive.DataAccess.Databases;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Telemetry;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Sanitization;

internal sealed class FileSanitizer
{
    private const string ProtonDocsExtension = ".protondoc";

    private readonly Version _currentVersion;
    private readonly ClientInstanceSettings _settings;
    private readonly IDocumentSanitizationApiClient _documentSanitizationApiClient;
    private readonly IReadOnlyCollection<RemoteToLocalMapping> _mappings;
    private readonly IErrorReporting _errorReporting;
    private readonly Func<long, CancellationToken, Task<bool>> _tryMarkRemoteNodeAsDirtyAsync;
    private readonly RemoteAdapterDatabase _remoteAdapterDatabase;
    private readonly LocalAdapterDatabase _localAdapterDatabase;
    private readonly FileSanitizationProvider _provider;
    private readonly Lazy<Task<ILookup<long, FileSanitizationJob>>> _jobs;
    private readonly SyncStatistics _statistics;
    private readonly IErrorCounter _errorCounter;
    private readonly ILogger<FileSanitizer> _logger;

    public FileSanitizer(
        AppConfig appConfig,
        ClientInstanceSettings settings,
        IDocumentSanitizationApiClient documentSanitizationApiClient,
        IReadOnlyCollection<RemoteToLocalMapping> mappings,
        Func<long, CancellationToken, Task<bool>> tryMarkRemoteNodeAsDirtyAsync,
        RemoteAdapterDatabase remoteAdapterDatabase,
        LocalAdapterDatabase localAdapterDatabase,
        FileSanitizationProvider provider,
        IErrorReporting errorReporting,
        SyncStatistics statistics,
        IErrorCounter errorCounter,
        ILogger<FileSanitizer> logger)
    {
        _currentVersion = appConfig.AppVersion;
        _settings = settings;
        _documentSanitizationApiClient = documentSanitizationApiClient;
        _mappings = mappings;
        _tryMarkRemoteNodeAsDirtyAsync = tryMarkRemoteNodeAsDirtyAsync;
        _remoteAdapterDatabase = remoteAdapterDatabase;
        _localAdapterDatabase = localAdapterDatabase;
        _provider = provider;
        _statistics = statistics;
        _errorCounter = errorCounter;
        _logger = logger;
        _errorReporting = errorReporting;

        _jobs = new(() => GetJobsAsync(CancellationToken.None));
    }

    public bool IsActive { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var isFirstAppRun = _settings.LastSanitization.Version is null;
        IsActive = isFirstAppRun;

        if (!IsActive)
        {
            _logger.LogInformation("Document name migration: Already run");
            return;
        }

        ILookup<long, FileSanitizationJob> jobs;

        try
        {
            if (_remoteAdapterDatabase.AdapterTreeRepository.GetLastId() == default)
            {
                _statistics.DocumentNameMigration.IncrementNumberOfMigrationsSkipped();
                _logger.LogInformation("Document name migration: Skipped due to empty remote database");
                return;
            }

            _statistics.DocumentNameMigration.IncrementNumberOfMigrationsStarted();

            jobs = await _jobs.Value.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _errorCounter.Add(ErrorScope.DocumentNameMigration, ex);
            _statistics.DocumentNameMigration.IncrementNumberOfMigrationsFailed();
            ReportException("Failed to get file sanitization jobs", ex);
            return;
        }

        if (jobs.Count == 0)
        {
            HandleCompletion();
            return;
        }

        _provider.SyncActivityChanged -= OnSyncActivityChanged;
        _provider.SyncActivityChanged += OnSyncActivityChanged;

        try
        {
            await MarkRemoteNodesAsDirtyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _errorCounter.Add(ErrorScope.DocumentNameMigration, ex);
            _statistics.DocumentNameMigration.IncrementNumberOfMigrationsFailed();
            ReportException("Failed to mark nodes as dirty", ex);
            return;
        }

        if (jobs.SelectMany(x => x).All(x => x.IsFinished))
        {
            HandleCompletion();
        }
    }

    public void Stop()
    {
        _provider.SyncActivityChanged -= OnSyncActivityChanged;
    }

    private async Task MarkRemoteNodesAsDirtyAsync(CancellationToken cancellationToken)
    {
        var jobs = await _jobs.Value.WaitAsync(cancellationToken).ConfigureAwait(false);

        foreach (var job in jobs.SelectMany(x => x))
        {
            var remoteNode = job.RemoteNode;
            if (remoteNode is null)
            {
                _logger.LogInformation(
                    "Document name migration: Document with volume ID {VolumeId} and link ID {LinkId} was not mapped",
                    job.VolumeId,
                    job.LinkId);

                _statistics.DocumentNameMigration.IncrementNumberOfNonMappedDocuments();

                job.IsFinished = true;
                continue;
            }

            _logger.LogInformation(
                "Document name migration: Document with volume ID {VolumeId} and link ID {LinkId} is mapped with internal ID {Id}",
                job.VolumeId,
                job.LinkId,
                remoteNode.Id);

            if (remoteNode.Name.EndsWith(ProtonDocsExtension, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Document name migration: The name on the remote node for document with internal ID {Id} already has the desired extension",
                    remoteNode.Id);

                _statistics.DocumentNameMigration.IncrementNumberOfDocumentsNotRequiringRename();

                job.IsFinished = true;
                continue;
            }

            var localNode = _localAdapterDatabase.AdapterTreeRepository.NodeById(remoteNode.Id);
            if (localNode?.Name.EndsWith(ProtonDocsExtension, StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogInformation(
                    "Document name migration: The name on the local node for document with internal ID {Id} already has the desired extension",
                    remoteNode.Id);

                _statistics.DocumentNameMigration.IncrementNumberOfDocumentsNotRequiringRename();

                job.IsFinished = true;
                continue;
            }

            await _tryMarkRemoteNodeAsDirtyAsync.Invoke(remoteNode.Id, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Document name migration: Document with internal ID {Id} requires migration, remote node marked as dirty", remoteNode.Id);

            _statistics.DocumentNameMigration.IncrementNumberOfDocumentRenamingAttempts();
        }
    }

    private async Task<ILookup<long, FileSanitizationJob>> GetJobsAsync(CancellationToken cancellationToken)
    {
        var documentIdentities = await GetDocumentIdentitiesAsync(cancellationToken).ConfigureAwait(false);

        var volumeIdMap = _mappings
            .Where(x => x.Remote.VolumeId is not null)
            .DistinctBy(x => x.Remote.VolumeId)
            .ToDictionary(x => x.Remote.VolumeId!, x => x.Remote.InternalVolumeId);

        return documentIdentities.Select(
            documentIdentity =>
            {
                var job = new FileSanitizationJob(documentIdentity.VolumeId, documentIdentity.LinkId);

                if (volumeIdMap.TryGetValue(documentIdentity.VolumeId, out var internalVolumeId))
                {
                    job.RemoteNode = _remoteAdapterDatabase.AdapterTreeRepository.NodeByAltId((internalVolumeId, job.LinkId));
                }

                return job;
            }).ToLookup(x => x.RemoteNode?.Id ?? -1);
    }

    private async Task<IReadOnlyList<DocumentIdentity>> GetDocumentIdentitiesAsync(CancellationToken cancellationToken)
    {
        var response = await _documentSanitizationApiClient.GetLinksAsync(cancellationToken).ConfigureAwait(false);
        return response.Documents;
    }

    private async void OnSyncActivityChanged(object? sender, SyncActivityItem<long> item)
    {
        if (!IsActive
            || item.ActivityType is not (SyncActivityType.Rename or SyncActivityType.Move)
            || item.Replica != Replica.Local
            || !item.Name.EndsWith(ProtonDocsExtension, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var jobs = await _jobs.Value.ConfigureAwait(false);

            var job = jobs[item.Id].FirstOrDefault();
            if (job?.IsFinished != false)
            {
                return;
            }

            switch (item.Status)
            {
                case SyncActivityItemStatus.Succeeded:
                    _statistics.DocumentNameMigration.IncrementNumberOfRenamedDocuments();
                    _logger.LogInformation("Document name migration: Name of document with internal ID {Id} successfully migrated", item.Id);
                    job.IsFinished = true;
                    break;

                case SyncActivityItemStatus.Warning:
                case SyncActivityItemStatus.Failed:
                    _statistics.DocumentNameMigration.IncrementNumberOfFailedDocumentRenamingAttempts();
                    _logger.LogInformation(
                        "Document name migration: Renaming of document with internal ID {Id} faced warning or error: {ErrorMessage}",
                        item.Id,
                        item.ErrorMessage);
                    job.IsFinished = true;
                    break;
            }

            if (jobs.SelectMany(x => x).All(x => x.IsFinished))
            {
                HandleCompletion();
            }
        }
        catch (Exception ex)
        {
            ReportException($"File sanitizer failed to process sync activity event \"{item.Status}\" for item with ID {item.Id}", ex);
        }
    }

    private void HandleCompletion()
    {
        IsActive = false;

        _logger.LogInformation("Document name migration: Completed");

        _statistics.DocumentNameMigration.IncrementNumberOfMigrationsCompleted();

        PersistCompletion();

        Stop();
    }

    private void ReportException(string message, Exception exception)
    {
        _errorReporting.CaptureException(new FileSanitizationException(message, exception));
    }

    private void PersistCompletion()
    {
        _settings.LastSanitization = (DateTime.UtcNow, _currentVersion);
    }
}
