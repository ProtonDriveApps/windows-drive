using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ProtonDrive.Shared.Net.Http;

/// <summary>
/// Prevents sending HTTP requests based on delay value returned in HTTP response 429
/// (Too Many Requests). The handler responds with HTTP code 429 (Too Many Requests) while the
/// delay specified in the HTTP response Retry-After header has not passed.
/// </summary>
/// <remarks>
/// Request prevention is handled per HTTP endpoint identified by URI authority.
/// Maximum delay value is limited to 10 minutes.
/// </remarks>
public sealed class TooManyRequestsHandler : DelegatingHandler
{
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(10);

    private readonly TooManyRequestsBlockedEndpoints _blockedEndpoints;
    private readonly IClock _clock;
    private readonly ILogger<TooManyRequestsHandler> _logger;

    public TooManyRequestsHandler(
        TooManyRequestsBlockedEndpoints blockedEndpoints,
        IClock clock,
        ILogger<TooManyRequestsHandler> logger)
    {
        _blockedEndpoints = blockedEndpoints;
        _clock = clock;
        _logger = logger;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!CanSend(request))
        {
            return new HttpResponseMessage(HttpStatusCode.TooManyRequests) { RequestMessage = request };
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        return ProcessResponse(response);
    }

    private bool CanSend(HttpRequestMessage request)
    {
        var path = GetPath(request);
        if (string.IsNullOrEmpty(path))
        {
            return true;
        }

        if (!_blockedEndpoints.TryGetValue(path, out var retryAfter))
        {
            return true;
        }

        var now = _clock.UtcNow;
        if (retryAfter > now && retryAfter <= now + MaxRetryDelay)
        {
            return false;
        }

        if (_blockedEndpoints.TryRemove(path, out _))
        {
            _logger.LogInformation("Allowed HTTP requests to \"{Path}\"", path);
        }

        return true;
    }

    private HttpResponseMessage ProcessResponse(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.TooManyRequests)
        {
            return response;
        }

        var retryAfterDelta = response.Headers.RetryAfter?.Delta;
        if (retryAfterDelta == null || retryAfterDelta < TimeSpan.Zero)
        {
            return response;
        }

        var path = GetPath(response.RequestMessage);
        if (string.IsNullOrEmpty(path))
        {
            return response;
        }

        if (retryAfterDelta > MaxRetryDelay)
        {
            retryAfterDelta = MaxRetryDelay;
        }

        if (_blockedEndpoints.TryAdd(path, _clock.UtcNow + retryAfterDelta.Value))
        {
            _logger.LogInformation("Blocked HTTP requests to \"{Path}\" for {Delay}", path, retryAfterDelta.Value);
        }

        return response;
    }

    private string? GetPath(HttpRequestMessage? request)
    {
        var uri = request?.RequestUri;
        if (uri == null)
        {
            return null;
        }

        // The scheme and authority (domain) segments of the URI. Path and query segments are not included.
        return uri.GetLeftPart(UriPartial.Authority);
    }
}
