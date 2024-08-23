using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client.Core.Events.Contracts;

namespace ProtonDrive.Client.Core.Events;

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

        return new CoreEvents(ToResumeToken(eventsResponse))
        {
            HasAddressChanged = eventsResponse.AddressEvents.Any(),
            User = eventsResponse.User,
            Organization = eventsResponse.Organization,
            Subscription = eventsResponse.Subscription,
            UsedSpace = eventsResponse.UsedDriveSpace,
        };
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

        return default;
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
