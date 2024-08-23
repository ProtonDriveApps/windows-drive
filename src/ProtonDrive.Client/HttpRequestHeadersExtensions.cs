using System;
using System.Net.Http.Headers;
using ProtonDrive.Client.Configuration;

namespace ProtonDrive.Client;

internal static class HttpRequestHeadersExtensions
{
    private const string DefaultLanguage = "en-US,en";

    public static void AddApiRequestHeaders(this HttpRequestHeaders headerCollection, DriveApiConfig config, string locale)
    {
        var version = config.ClientVersion
                      ?? throw new InvalidOperationException("Cannot configure an HTTP client without a client version.");
        var userAgent = config.UserAgent
                        ?? throw new InvalidOperationException("Cannot configure an HTTP client without a user agent.");
        var contentType = config.ContentType
                          ?? throw new InvalidOperationException("Cannot configure an HTTP client without an encoding.");

        headerCollection.Add("Accept-Language", DefaultLanguage);
        headerCollection.Add("x-pm-appversion", version);
        headerCollection.Add("x-pm-locale", locale);
        headerCollection.Add("User-Agent", userAgent);
        headerCollection.Add("Accept", contentType);
    }
}
