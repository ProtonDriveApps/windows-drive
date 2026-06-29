using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client.Core.Events.Contracts;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Core.Events;

internal sealed class CoreEventClient : ICoreEventClient, ICoreEventProvider
{
    private readonly ICoreEventApiClient _apiClient;
    private readonly ILogger<CoreEventClient> _logger;

    public CoreEventClient(
        ICoreEventApiClient apiClient,
        ILogger<CoreEventClient> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public event EventHandler<CoreEvents>? EventsReceived;

    public async Task<CoreEvents> GetEventsAsync(CoreEventResumeToken resumeToken, CancellationToken cancellationToken)
    {
        var events = await InternalGetEventsAsync(resumeToken, cancellationToken).ConfigureAwait(false);

        EventsReceived?.Invoke(this, events);

        return events;
    }

    private static CoreEventResumeToken ToResumeToken(CoreEventListResponse eventsResponse)
    {
        return new CoreEventResumeToken
        {
            AnchorId = eventsResponse.AnchorId,
            HasMoreData = eventsResponse.HasMoreData,
            IsRefreshRequired = eventsResponse.RefreshMask.HasFlag(CoreEventsRefreshMask.Everything),
        };
    }

    private async Task<CoreEvents> InternalGetEventsAsync(CoreEventResumeToken resumeToken, CancellationToken cancellationToken)
    {
        var anchorId = resumeToken.AnchorId;

        if (string.IsNullOrEmpty(anchorId))
        {
            // The API resume token (anchor ID) is unknown, refreshing it
            return new CoreEvents(await RefreshAsync(cancellationToken).ConfigureAwait(false));
        }

        var eventsResponse = await GetEventsAsync(anchorId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(eventsResponse.AnchorId))
        {
            // The track of event stream has been lost, refreshing the API resume token (anchor ID)
            return new CoreEvents(await RefreshAsync(cancellationToken).ConfigureAwait(false));
        }

        var coreEvents = new CoreEvents(ToResumeToken(eventsResponse))
        {
            HasAddressChanged = eventsResponse.AddressEvents.Any(),
            HasSettingsChanged = eventsResponse.UserSettings is not null,
            User = eventsResponse.User,
            Organization = eventsResponse.Organization,
            Subscription = eventsResponse.Subscription,
            UsedSpace = eventsResponse.SplitStorageUsedSpace,
            DriveUsedSpace = eventsResponse.User?.ProductUsedSpace.Drive,
        };

        var worthLogging =
            coreEvents.User is not null ||
            coreEvents.HasAddressChanged ||
            coreEvents.HasSettingsChanged;

        if (worthLogging)
        {
            _logger.LogInformation(
                "Received core events, changed: user: {UserHasChanged}, address: {AddressHasChanged}, settings: {SettingsHasChanged}",
                coreEvents.User is not null,
                coreEvents.HasAddressChanged,
                coreEvents.HasSettingsChanged);
        }

        return coreEvents;
    }

    private async Task<CoreEventResumeToken> RefreshAsync(CancellationToken cancellationToken)
    {
        var anchorId = await GetLatestEventAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(anchorId))
        {
            // Failed to obtain the API resume token (anchor ID), will retry next time
            return CoreEventResumeToken.Start;
        }

        // Successfully obtained the API resume token (anchor ID)
        return new CoreEventResumeToken
        {
            AnchorId = anchorId,
            IsRefreshRequired = true,
        };
    }

    private async Task<string?> GetLatestEventAsync(CancellationToken cancellationToken)
    {
        var response = await _apiClient.GetLatestEventAsync(cancellationToken).Safe().ConfigureAwait(false);

        if (response.Succeeded && !string.IsNullOrEmpty(response.AnchorId))
        {
            return response.AnchorId;
        }

        _logger.LogWarning("Failed to get latest core event: {ErrorCode} {ErrorMessage}", response.Code, response.Error);

        return null;
    }

    private async Task<CoreEventListResponse> GetEventsAsync(string anchorId, CancellationToken cancellationToken)
    {
        try
        {
            return await _apiClient.GetEventsAsync(anchorId, cancellationToken)
                .ThrowOnFailure()
                .ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.ResponseCode is ResponseCode.InvalidEncryptedIdFormat)
        {
            _logger.LogError(
                "Failed to get core events: Invalid value of anchor ID={AnchorId}: {ErrorCode} {ErrorMessage}",
                anchorId,
                ex.ResponseCode,
                ex.Message);

            return new CoreEventListResponse
            {
                AnchorId = null,
                RefreshMask = CoreEventsRefreshMask.Everything,
            };
        }
    }
}
