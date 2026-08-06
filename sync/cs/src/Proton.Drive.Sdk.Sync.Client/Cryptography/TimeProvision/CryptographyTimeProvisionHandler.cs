namespace Proton.Drive.Sdk.Sync.Client.Cryptography.TimeProvision;

internal sealed class CryptographyTimeProvisionHandler : DelegatingHandler
{
    private readonly CryptographyTimeProvider _timeProvider;

    public CryptographyTimeProvisionHandler(CryptographyTimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var responseMessage = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (responseMessage.Headers.Date is { } time)
        {
            _timeProvider.UpdateTime(time);
        }

        return responseMessage;
    }
}
