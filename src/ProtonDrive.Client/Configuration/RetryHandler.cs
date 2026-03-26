using System.Net;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace ProtonDrive.Client.Configuration;

/// <summary>
/// Retries transient HTTP request failures.
/// On request cancellation, returns last attempt result or re-throws last exception.
/// </summary>
internal sealed class RetryHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<WrappedHttpResponseMessage> _retryPipeline;

    public RetryHandler(int numberOfRetries)
    {
        _retryPipeline = CreateRetryPipeline(numberOfRetries);
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        WrappedHttpResponseMessage? lastResponse = null;
        Exception? lastException = null;

        var retryPolicy = GetRetryPolicy(request);

        try
        {
            var result = await retryPolicy.ExecuteAsync(InternalSendAsync, cancellationToken).ConfigureAwait(false);

            return result.Message;
        }
        catch (OperationCanceledException)
        {
            if (lastResponse != null)
            {
                return lastResponse.Message;
            }

            if (lastException != null)
            {
                throw lastException;
            }

            throw;
        }
        catch (Exception)
        {
            ClearLastResult();
            throw;
        }

        async ValueTask<WrappedHttpResponseMessage> InternalSendAsync(CancellationToken internalCancellationToken)
        {
            try
            {
                var response = await base.SendAsync(request, internalCancellationToken).ConfigureAwait(false);

                ClearLastResult();

                return lastResponse = new WrappedHttpResponseMessage(response);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ClearLastResult();
                lastException = ex;
                throw;
            }
        }

        void ClearLastResult()
        {
            // ReSharper disable once AccessToModifiedClosure
            lastResponse?.Message.Dispose();
            lastResponse = null;
            lastException = null;
        }
    }

    private static ResiliencePipeline<WrappedHttpResponseMessage> CreateRetryPipeline(int numberOfRetries)
    {
        if (numberOfRetries == 0)
        {
            return ResiliencePipeline<WrappedHttpResponseMessage>.Empty;
        }

        return new ResiliencePipelineBuilder<WrappedHttpResponseMessage>()
            .AddRetry(
                new RetryStrategyOptions<WrappedHttpResponseMessage>
                {
                    ShouldHandle = new PredicateBuilder<WrappedHttpResponseMessage>()
                        .HandleResult(args => args.Message.StatusCode is HttpStatusCode.TooManyRequests)
                        .HandleResult(args => args.Message.StatusCode is HttpStatusCode.RequestTimeout)
                        .HandleResult(args => args.Message.StatusCode is >= HttpStatusCode.InternalServerError and < (HttpStatusCode)600)
                        .Handle<HttpRequestException>()
                        .Handle<BrokenCircuitException>()
                        .Handle<TimeoutException>(),
                    MaxRetryAttempts = numberOfRetries,
                    DelayGenerator = args => ValueTask.FromResult((TimeSpan?)TimeSpan.FromSeconds(Math.Pow(2.5, args.AttemptNumber) / 4)),
                })
            .Build();
    }

    private ResiliencePipeline<WrappedHttpResponseMessage> GetRetryPolicy(HttpRequestMessage requestMessage)
    {
        return requestMessage.GetRetryIsDisabled() ? ResiliencePipeline<WrappedHttpResponseMessage>.Empty : _retryPipeline;
    }

    // Wraps HttpResponseMessage into non-disposable class to prevent disposal by Polly upon cancellation
    private class WrappedHttpResponseMessage(HttpResponseMessage message)
    {
        public HttpResponseMessage Message { get; } = message;
    }
}
