using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Views.Shared;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Windows.Views.Offer;

internal sealed class OfferViewModel : ObservableObject, ICloseable, IDialogViewModel
{
    private readonly IForkingSessionUrlOpener _urlOpener;
    private readonly ILogger<OfferViewModel> _logger;

    private bool _closingRequested;
    private Notifications.Offers.Offer? _offer;

    public OfferViewModel(IForkingSessionUrlOpener urlOpener, ILogger<OfferViewModel> logger)
    {
        _urlOpener = urlOpener;
        _logger = logger;

        GetDealCommand = new AsyncRelayCommand(GetDealAsync);
    }

    public string Title => "Proton Drive";

    public ImageSource? Image { get; private set; }

    public ICommand GetDealCommand { get; }

    public bool ClosingRequested
    {
        get => _closingRequested;
        set => SetProperty(ref _closingRequested, value);
    }

    private Notifications.Offers.Offer Offer => _offer ?? throw new ArgumentNullException(nameof(Offer));

    public void Close() { }

    public bool SetDataItem(Notifications.Offers.Offer offer)
    {
        try
        {
            _offer = offer;
            Image = new BitmapImage(new Uri(offer.ImageFilePath, UriKind.Absolute));
        }
        catch (Exception ex) when (ex is FormatException || ex.IsFileAccessException())
        {
            _logger.LogWarning("Unable to display an offer: {ErrorMessage}", ex.CombinedMessage());
            return false;
        }

        return true;
    }

    private async Task GetDealAsync(CancellationToken cancellationToken)
    {
        if (ClosingRequested)
        {
            return;
        }

        var openingSucceeded = await _urlOpener.TryOpenUrlAsync(Offer.AccountAppUrl, "web-account-lite", cancellationToken).ConfigureAwait(true);

        if (openingSucceeded)
        {
            ClosingRequested = true;
        }
    }
}
