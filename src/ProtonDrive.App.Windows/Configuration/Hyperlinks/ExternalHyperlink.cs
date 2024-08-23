namespace ProtonDrive.App.Windows.Configuration.Hyperlinks;

internal sealed class ExternalHyperlink : IExternalHyperlink
{
    private readonly IUrlOpener _urlOpener;
    private readonly string _url;

    public ExternalHyperlink(IUrlOpener urlOpener, string url)
    {
        _urlOpener = urlOpener;
        _url = url;
    }

    public void Open()
    {
        _urlOpener.OpenUrl(_url);
    }
}
