using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client;
using Proton.Drive.Sdk.Sync.Client.Authentication.Sessions;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.App.Windows.Configuration.Hyperlinks;

internal sealed class ForkingSessionUrlOpener : IForkingSessionUrlOpener
{
    private readonly ILogger<ForkingSessionUrlOpener> _logger;
    private readonly IUrlOpener _urlOpener;
    private readonly ISessionClient _sessionClient;

    public ForkingSessionUrlOpener(ILogger<ForkingSessionUrlOpener> logger, IUrlOpener urlOpener, ISessionClient sessionClient)
    {
        _logger = logger;
        _urlOpener = urlOpener;
        _sessionClient = sessionClient;
    }

    public async Task<bool> TryOpenUrlAsync(string url, string childClientId, CancellationToken cancellationToken)
    {
        var sessionSelector = await TryGetSessionSelectorAsync(childClientId, cancellationToken).ConfigureAwait(false);
        if (sessionSelector == null)
        {
            return false;
        }

        if (!TryGetUrlWithSessionSelector(url, sessionSelector, out var urlWithSessionSelector))
        {
            return false;
        }

        _urlOpener.OpenUrl(urlWithSessionSelector);

        _logger.LogInformation("Opened URL with appended session selector: {Url}", url);

        return true;
    }

    private bool TryGetUrlWithSessionSelector(string url, string sessionSelector, [MaybeNullWhen(false)] out string urlWithSessionSelector)
    {
        try
        {
            urlWithSessionSelector =
                new UriBuilder(url)
                {
                    Fragment = "selector=" + sessionSelector,
                }
                    .Uri
                    .AbsoluteUri;
        }
        catch (Exception ex) when (ex is UriFormatException or InvalidOperationException)
        {
            _logger.LogWarning("Failed to append session selector to the URL \"{Url}\": {ErrorMessage}", url, ex.Message);

            urlWithSessionSelector = default;
            return false;
        }

        return true;
    }

    private async Task<string?> TryGetSessionSelectorAsync(string childClientId, CancellationToken cancellationToken)
    {
        var parameters = new SessionForkingParameters
        {
            ChildClientId = childClientId,
            Independent = false,
        };

        try
        {
            return await _sessionClient.ForkSessionAsync(parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning("Forking session failed: {ErrorCode} {ErrorMessage}", ex.GetRelevantFormattedErrorCode(), ex.Message);

            return null;
        }
    }
}
