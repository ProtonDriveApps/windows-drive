using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client;

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

    public static async Task<T?> TryGetContentAsApiResponseAsync<T>(this Refit.ApiException exception)
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

    private static async Task<T> ThrowOnApiFailure<T>(this Task<T> origin)
    {
        try
        {
            return await origin.ConfigureAwait(false);
        }
        catch (Exception exception) when (ExceptionMapping.TryMapHttpClientException(exception, out var mappedException))
        {
            throw mappedException;
        }
    }

    private static async Task<Exception> MapToApiExceptionAsync<T>(this Refit.ApiException ex)
        where T : ApiResponse
    {
        var response = await ex.TryGetContentAsApiResponseAsync<T>().ConfigureAwait(false);

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
}
