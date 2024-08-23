using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Polly.CircuitBreaker;

namespace ProtonDrive.Client;

public static class ApiResponseExtensions
{
    public static async Task<T> ReadFromJsonAsync<T>(this Task<HttpResponseMessage> origin, CancellationToken cancellationToken)
        where T : ApiResponse
    {
        var response = await origin.ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return await ReadFromJsonAsync<T>(response, cancellationToken).ConfigureAwait(false) ??
                   throw new ApiException(ResponseCode.Unknown, "Failed to deserialize response");
        }

        try
        {
            return await ReadFromJsonAsync<T>(response, cancellationToken).ConfigureAwait(false) ??
                   throw new HttpRequestException(response.ReasonPhrase, null, response.StatusCode);
        }
        catch
        {
            throw new HttpRequestException(response.ReasonPhrase, null, response.StatusCode);
        }
    }

    public static async Task<ApiResponse?> TryReadFromJsonAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType != MediaTypeNames.Application.Json)
        {
            return null;
        }

        var content = await ReadContentNoneDestructiveAsync(response, cancellationToken).ConfigureAwait(false);

        return TryReadApiResponseFromJson(content);
    }

    public static async Task<T> Safe<T>(this Task<T> origin)
        where T : ApiResponse, new()
    {
        try
        {
            return await origin.WithApiException().ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            return new T { Code = ex.ResponseCode, Error = ex.Message };
        }
    }

    public static async Task ThrowOnFailureToDelete<T>(this Task<T> origin)
        where T : ApiResponse
    {
        try
        {
            await origin.ThrowOnFailure().ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.ResponseCode is ResponseCode.DoesNotExist)
        {
            // The item might have already been deleted.
            // If it is missing, the request is considered as succeeded.
        }
    }

    public static async Task<T> ThrowOnFailure<T>(this Task<T> origin)
        where T : ApiResponse
    {
        var response = await origin.WithApiException().ConfigureAwait(false);

        if (!response.Succeeded)
        {
            throw new ApiException(response.Code, response.Error ?? "API request failed");
        }

        return response;
    }

    public static Task<HttpResponseMessage> ThrowOnFailure(this Task<HttpResponseMessage> origin)
    {
        return EnsureSuccess().ThrowOnApiFailure();

        async Task<HttpResponseMessage> EnsureSuccess()
        {
            var response = await origin.ConfigureAwait(false);

            return response.EnsureSuccessStatusCode();
        }
    }

    public static async Task<T> WithApiFailureMapping<T>(this Task<T> origin)
    {
        try
        {
            return await origin.ThrowOnApiFailure().ConfigureAwait(false);
        }
        catch (Refit.ApiException ex)
        {
            throw await ex.MapToApiExceptionAsync<ApiResponse>().ConfigureAwait(false);
        }
    }

    public static async Task<T> ThrowOnApiFailure<T>(this Task<T> origin)
    {
        try
        {
            return await origin.ConfigureAwait(false);
        }
        catch (BrokenCircuitException ex)
        {
            throw new ApiException(ResponseCode.Offline, "API not available", ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode != null)
        {
            throw new ApiException(ex.StatusCode.Value, (ResponseCode)ex.StatusCode.Value, ex.Message, ex);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException socketException)
        {
            throw new ApiException(ResponseCode.SocketError, socketException.Message, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(ResponseCode.Unknown, ex.InnerException?.Message ?? ex.Message, ex);
        }
        catch (TimeoutException ex)
        {
            throw new ApiException(ResponseCode.Timeout, "API request timed out", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new ApiException(ResponseCode.Timeout, "API request timed out", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new ApiException(ResponseCode.Unknown, "API request failed", ex);
        }
        catch (JsonException ex)
        {
            throw new ApiException(ResponseCode.Unknown, "Failed to deserialize JSON content", ex);
        }
    }

    public static async Task<Exception> MapToApiExceptionAsync<T>(this Refit.ApiException ex)
        where T : ApiResponse
    {
        var response = await ex.GetContentAsApiResponseAsync<T>().ConfigureAwait(false);

        return ex.StatusCode switch
        {
            >= HttpStatusCode.BadRequest when response is not null
                => new ApiException<T>(ex.StatusCode, response.Code, response.Error ?? "API request failed", response, ex)
                {
                    IsMessageAuthoritative = !string.IsNullOrEmpty(response.Error),
                },
            _ when ex.InnerException is JsonException
                => new ApiException<T>(ex.StatusCode, (ResponseCode)ex.StatusCode, "Failed to deserialize JSON content", response, ex),
            _ => new ApiException<T>(ex.StatusCode, (ResponseCode)ex.StatusCode, "API request failed", response, ex),
        };
    }

    public static async Task<T?> GetContentAsApiResponseAsync<T>(this Refit.ApiException exception)
        where T : ApiResponse
    {
        try
        {
            var response = await exception.GetContentAsAsync<T>().ConfigureAwait(false);
            if (response != null && response.Code != default)
            {
                return response;
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private static async Task<T> WithApiException<T>(this Task<T> origin)
        where T : ApiResponse
    {
        try
        {
            return await origin.ThrowOnApiFailure().ConfigureAwait(false);
        }
        catch (Refit.ApiException ex)
        {
            throw await MapToApiExceptionAsync<T>(ex).ConfigureAwait(false);
        }
    }

    private static async Task<T?> ReadFromJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType == MediaTypeNames.Application.Json)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return default;
    }

    private static ApiResponse? TryReadApiResponseFromJson(byte[] content)
    {
        try
        {
            var response = JsonSerializer.Deserialize<ApiResponse>(content);
            if (response != null && response.Code != default)
            {
                return response;
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private static async Task<byte[]> ReadContentNoneDestructiveAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var origin = response.Content;
        var content = await origin.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var clonedContent = new ByteArrayContent(content);

        foreach (var (key, value) in origin.Headers)
        {
            clonedContent.Headers.TryAddWithoutValidation(key, value);
        }

        // HttpContent can be read only once, therefore we replace it with a fresh clone so that it can be read once again
        response.Content = clonedContent;
        origin.Dispose();

        return content;
    }
}
