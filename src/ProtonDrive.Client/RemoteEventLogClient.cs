using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.Events;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Client.Shares.Events;
using ProtonDrive.Client.Volumes.Events;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

internal sealed class RemoteEventLogClient : IEventLogClient<string>, IDisposable
{
    private readonly bool _isVolumeBased;
    private readonly string _volumeOrShareId;
    private readonly IVolumeEventClient _volumeEventClient;
    private readonly IShareEventClient _shareEventClient;
    private readonly IRemoteNodeService _remoteNodeService;
    private readonly IRepository<string> _anchorIdRepository;
    private readonly ILogger<RemoteEventLogClient> _logger;

    private readonly string _volumeOrShare;
    private readonly SingleAction _getEvents;
    private readonly ISchedulerTimer _getEventsTimer;

    private DriveEventResumeToken _resumeToken = DriveEventResumeToken.Start;
    private bool _enabled;

    public RemoteEventLogClient(
        bool isVolumeBased,
        string volumeOrShareId,
        IRepository<string> anchorIdRepository,
        TimeSpan pollInterval,
        IVolumeEventClient volumeEventClient,
        IShareEventClient shareEventClient,
        IRemoteNodeService remoteNodeService,
        IScheduler scheduler,
        ILogger<RemoteEventLogClient> logger)
    {
        _isVolumeBased = isVolumeBased;
        _volumeOrShareId = volumeOrShareId;
        _volumeEventClient = volumeEventClient;
        _shareEventClient = shareEventClient;
        _remoteNodeService = remoteNodeService;
        _anchorIdRepository = anchorIdRepository;
        _logger = logger;

        _volumeOrShare = isVolumeBased ? "volume" : "share";
        _getEvents = _logger.GetSingleActionWithExceptionsLoggingAndCancellationHandling(GetAllEventsAsync, nameof(RemoteEventLogClient));

        _getEventsTimer = scheduler.CreateTimer();
        _getEventsTimer.Interval = pollInterval;
        _getEventsTimer.Tick += OnGetEventsTimerTick;
    }

    public event EventHandler<EventLogEntriesReceivedEventArgs<string>>? LogEntriesReceived;

    public void Enable()
    {
        LoadResumeToken();

        _enabled = true;
        _getEventsTimer.Start();

        GetEventsAsync();
    }

    public void Disable()
    {
        _enabled = false;
        _getEventsTimer.Stop();
        _getEvents.Cancel();
    }

    public Task GetEventsAsync()
    {
        return _getEvents.RunAsync();
    }

    public void Dispose()
    {
        _getEventsTimer.Dispose();
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for the scheduled task to complete
        return _getEvents.CurrentTask;
    }

    private static EventLogEntry<string> ToEventLogEntry(EventType eventType, RemoteNode remoteNode)
    {
        // Modification time is used as Folder last write time, but default value is used as File last write time.
        return new EventLogEntry<string>(remoteNode.State == LinkState.Active ? ToChangeType(eventType) : EventLogChangeType.Deleted)
        {
            Name = remoteNode.Name,
            Path = remoteNode.Name,
            Id = remoteNode.Id,
            ParentId = remoteNode.ParentId,
            RevisionId = (remoteNode as RemoteFile)?.ActiveRevision?.Id,
            Attributes = remoteNode is RemoteFolder ? FileAttributes.Directory : default,
            LastWriteTimeUtc = remoteNode.ModificationTime,
            Size = (remoteNode as RemoteFile)?.PlainSize,
            SizeOnStorage = (remoteNode as RemoteFile)?.SizeOnStorage,
        };
    }

    private static EventLogChangeType ToChangeType(EventType eventType)
    {
        return eventType switch
        {
            EventType.Create => EventLogChangeType.CreatedOrMovedTo,
            EventType.Update => EventLogChangeType.Changed,
            EventType.UpdateMetadata => EventLogChangeType.ChangedOrMoved,
            EventType.Delete => EventLogChangeType.DeletedOrMovedFrom,
            _ => EventLogChangeType.Skipped,
        };
    }

    private void OnGetEventsTimerTick(object? sender, EventArgs eventArgs)
    {
        _getEvents.RunAsync();
    }

    private async Task GetAllEventsAsync(CancellationToken cancellationToken)
    {
        if (!_enabled)
        {
            return;
        }

        _logger.LogDebug("Started retrieving remote events on {VolumeOrShare} with ID={VolumeOrShareId}", _volumeOrShare, _volumeOrShareId);

        while (await GetEventsAsync(cancellationToken).ConfigureAwait(false))
        {
        }

        _logger.LogDebug("Finished retrieving remote events on {VolumeOrShare} with ID={VolumeOrShareId}", _volumeOrShare, _volumeOrShareId);
    }

    private async Task<bool> GetEventsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var events = await GetEvents(cancellationToken).ConfigureAwait(false);

            return await ProcessEventsAsync(events, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning(
                "Failed to retrieve remote events on {VolumeOrShare} with ID={VolumeOrShareId}: {ErrorCode} {ErrorMessage}",
                _volumeOrShare,
                _volumeOrShareId,
                ex is ApiException apiException ? apiException.ResponseCode : null,
                ex.Message);

            return false;
        }
    }

    private Task<DriveEvents> GetEvents(CancellationToken cancellationToken)
    {
        return _isVolumeBased
            ? _volumeEventClient.GetEventsAsync(_volumeOrShareId, _resumeToken, cancellationToken)
            : _shareEventClient.GetEventsAsync(_volumeOrShareId, _resumeToken, cancellationToken);
    }

    private async Task<bool> ProcessEventsAsync(DriveEvents result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (result.ResumeToken.IsRefreshRequired)
        {
            OnLogEntriesReceived(new EventLogEntry<string>(EventLogChangeType.Skipped), result.ResumeToken);

            _resumeToken = result.ResumeToken;

            return result.ResumeToken.HasMoreData;
        }

        int numberOfEvents = 0;
        var entries = new List<EventLogEntry<string>>(result.Events.Count);

        foreach (var item in result.Events)
        {
            try
            {
                numberOfEvents++;

                if (item.Link == null)
                {
                    _logger.LogError("Link cannot be null");
                }
                else if (item.Type == EventType.Delete)
                {
                    entries.Add(
                        new EventLogEntry<string>(EventLogChangeType.Deleted)
                        {
                            Id = item.Link.Id,
                        });
                }
                else if (_isVolumeBased && string.IsNullOrEmpty(item.ContextShareId))
                {
                    _logger.LogError(
                        "Context share is null for event type {EventType}, link ID={LinkId}, volume ID={VolumeId}",
                        item.Type,
                        item.Link.Id,
                        _volumeOrShareId);
                }
                else
                {
                    if (item.Link.ParentId == null)
                    {
                        _logger.LogInformation(
                            "Event received for the root link: {EventType}, link ID={LinkID}, {VolumeOrShare} ID={VolumeOrShareId}",
                            item.Type,
                            item.Link.Id,
                            _volumeOrShare,
                            _volumeOrShareId);
                    }

                    var shareId = (_isVolumeBased ? item.ContextShareId : _volumeOrShareId) ?? throw new InvalidOperationException();

                    var remoteNode = await _remoteNodeService.GetRemoteNodeAsync(shareId, item.Link, cancellationToken).ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();

                    entries.Add(ToEventLogEntry(item.Type, remoteNode));
                }
            }
            catch (ApiException ex)
            {
                _logger.LogError(
                    "Failed to get the link with ID={LinkId} on share with ID={ShareId}, {VolumeOrShare} ID={VolumeOrShareId}: {ErrorCode} {ErrorMessage}",
                    item.Link?.Id,
                    item.ContextShareId,
                    _volumeOrShare,
                    _volumeOrShareId,
                    ex.ResponseCode,
                    ex.CombinedMessage());

                return false;
            }
            catch (Exception ex) when (ex is CryptographicException or KeyPassphraseUnavailableException)
            {
                _logger.LogError(
                    "Failed to decrypt the link with ID={LinkId} on share with ID={ShareId}, {VolumeOrShare} ID={VolumeOrShareId}: {ErrorMessage}",
                    item.Link?.Id,
                    item.ContextShareId,
                    _volumeOrShare,
                    _volumeOrShareId,
                    ex.CombinedMessage());

                OnLogEntriesReceived(new EventLogEntry<string>(EventLogChangeType.Error), _resumeToken);

                return false;
            }
        }

        // Reporting even when there are no events and API resume token (anchor ID) has not changed to indicate successful events retrieval
        OnLogEntriesReceived(entries.AsReadOnly(), result.ResumeToken);
        _resumeToken = result.ResumeToken;

        if (numberOfEvents > 0 || result.ResumeToken.HasMoreData)
        {
            _logger.LogInformation(
                "{NumberOfEvents} remote event(s) received on {VolumeOrShare} with ID={VolumeOrShareId}",
                numberOfEvents,
                _volumeOrShare,
                _volumeOrShareId);
        }

        return result.ResumeToken.HasMoreData;
    }

    private void OnLogEntriesReceived(EventLogEntry<string> entry, DriveEventResumeToken resumeToken)
    {
        OnLogEntriesReceived([entry], resumeToken);
    }

    private void OnLogEntriesReceived(IReadOnlyCollection<EventLogEntry<string>> entries, DriveEventResumeToken resumeToken)
    {
        LogEntriesReceived?.Invoke(
            this,
            new EventLogEntriesReceivedEventArgs<string>(entries, () => SaveResumeToken(resumeToken)));
    }

    private void SaveResumeToken(DriveEventResumeToken resumeToken)
    {
        _anchorIdRepository.Set(resumeToken.AnchorId);
    }

    private void LoadResumeToken()
    {
        _resumeToken = new DriveEventResumeToken { AnchorId = _anchorIdRepository.Get() };
    }
}
