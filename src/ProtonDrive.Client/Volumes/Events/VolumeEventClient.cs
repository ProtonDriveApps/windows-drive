using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Events;

namespace ProtonDrive.Client.Volumes.Events;

internal sealed class VolumeEventClient : IVolumeEventClient
{
    private readonly IVolumeEventApiClient _apiClient;
    private readonly ILogger<VolumeEventClient> _logger;

    public VolumeEventClient(IVolumeEventApiClient apiClient, ILogger<VolumeEventClient> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public Task<DriveEvents> GetEventsAsync(string volumeId, DriveEventResumeToken resumeToken, CancellationToken cancellationToken)
    {
        return InternalGetEventsAsync(volumeId, resumeToken, cancellationToken);
    }

    private static DriveEventResumeToken ToResumeToken(EventListResponse eventsResponse)
    {
        return new DriveEventResumeToken
        {
            AnchorId = eventsResponse.AnchorId,
            HasMoreData = eventsResponse.HasMoreData,
            IsRefreshRequired = eventsResponse.RequiresRefresh,
        };
    }

    private async Task<DriveEvents> InternalGetEventsAsync(string volumeId, DriveEventResumeToken resumeToken, CancellationToken cancellationToken)
    {
        var anchorId = resumeToken.AnchorId;

        if (string.IsNullOrEmpty(anchorId))
        {
            // The API resume token (anchor ID) is unknown, refreshing it
            return new DriveEvents(await RefreshAsync(volumeId, cancellationToken).ConfigureAwait(false));
        }

        var eventsResponse = await GetEventsAsync(volumeId, anchorId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(eventsResponse.AnchorId))
        {
            // The track of event stream has been lost, refreshing the API resume token (anchor ID)
            return new DriveEvents(await RefreshAsync(volumeId, cancellationToken).ConfigureAwait(false));
        }

        return new DriveEvents(ToResumeToken(eventsResponse), eventsResponse.Events);
    }

    private async Task<DriveEventResumeToken> RefreshAsync(string volumeId, CancellationToken cancellationToken)
    {
        var anchorId = await GetLatestEventAsync(volumeId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(anchorId))
        {
            // Failed to obtain the API resume token (anchor ID), will retry next time
            return DriveEventResumeToken.Start;
        }

        // Successfully obtained the API resume token (anchor ID)
        return new DriveEventResumeToken
        {
            AnchorId = anchorId,
            IsRefreshRequired = true,
        };
    }

    private async Task<string?> GetLatestEventAsync(string volumeId, CancellationToken cancellationToken)
    {
        var response = await _apiClient.GetLatestEventAsync(volumeId, cancellationToken).Safe().ConfigureAwait(false);

        if (response.Succeeded && !string.IsNullOrEmpty(response.AnchorId))
        {
            return response.AnchorId;
        }

        _logger.LogWarning("Failed to get latest event on volume with ID={VolumeId}: {ErrorCode} {ErrorMessage}", volumeId, response.Code, response.Error);

        return default;
    }

    private async Task<EventListResponse> GetEventsAsync(string volumeId, string anchorId, CancellationToken cancellationToken)
    {
        try
        {
            return await _apiClient.GetEventsAsync(volumeId, anchorId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.ResponseCode is ResponseCode.InvalidEncryptedIdFormat)
        {
            _logger.LogError(
                "Failed to get events on volume with ID={VolumeId}: Invalid value of anchor ID={AnchorId}: {ErrorCode} {ErrorMessage}",
                volumeId,
                anchorId,
                ex.ResponseCode,
                ex.Message);

            return new EventListResponse
            {
                AnchorId = null,
                RequiresRefresh = true,
            };
        }
    }
}
