using System.Net.Mime;
using System.Text.Json;

namespace Proton.Drive.Shared.Net.Http;

public static class ApiResponseExtensions
{
    public static async Task<T?> TryReadFromJsonAsync<T>(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType != MediaTypeNames.Application.Json)
        {
            return default;
        }

        var content = await ReadContentNoneDestructiveAsync(response, cancellationToken).ConfigureAwait(false);

        return TryReadApiResponseFromJson<T>(content);
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

    private static T? TryReadApiResponseFromJson<T>(byte[] content)
    {
        try
        {
            var response = JsonSerializer.Deserialize<T>(content);
            if (response != null)
            {
                return response;
            }
        }
        catch
        {
            // Ignore
        }

        return default;
    }
}
