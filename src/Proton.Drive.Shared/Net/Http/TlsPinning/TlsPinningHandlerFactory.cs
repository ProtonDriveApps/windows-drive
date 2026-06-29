namespace Proton.Drive.Shared.Net.Http.TlsPinning;

public sealed class TlsPinningHandlerFactory
{
    private readonly Func<ITlsPinningReportClient> _reportClientFactory;
    private readonly TlsPinningConfigFactory _tlsPinningConfigFactory;

    public TlsPinningHandlerFactory(Func<ITlsPinningReportClient> reportClientFactory, TlsPinningConfigFactory tlsPinningConfigFactory)
    {
        _reportClientFactory = reportClientFactory;
        _tlsPinningConfigFactory = tlsPinningConfigFactory;
    }

    public TlsPinningHandler CreateTlsPinningHandler(string clientName)
    {
        var tlsPinningConfig = _tlsPinningConfigFactory.CreateTlsPinningConfig(clientName);
        return new TlsPinningHandler(tlsPinningConfig, _reportClientFactory);
    }
}
