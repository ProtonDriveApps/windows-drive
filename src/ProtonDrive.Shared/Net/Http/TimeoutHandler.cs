using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Net.Http;

/// <summary>
/// Throws <see cref="TimeoutException"/> in case there is no HTTP response received
/// withing the specified timeout period.
/// </summary>
/// <remarks>
/// The exception thrown by the <see cref="HttpClient"/> when the timeout is elapsed
/// does not let you determine the cause of the error. When a timeout occurs it throws
/// a <see cref="TaskCanceledException"/>. Therefore, there is no way to determine from
/// the exception if the request was actually canceled, or if a timeout occurred.
/// </remarks>
public sealed class TimeoutHandler : DelegatingHandler
{
    private readonly TimeSpan _timeout;

    public TimeoutHandler(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellationSource.CancelAfter(_timeout);
        try
        {
            return await base.SendAsync(request, cancellationSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("HTTP request timed out");
        }
    }
}
