using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Proton.Drive.App.Windows.Views;

namespace Proton.Drive.App.Windows.Dialogs.HumanVerification;

internal sealed class HumanVerificationDialogViewModel : ObservableObject, IDialogViewModel
{
    private const int WebviewAddedHeight = 130;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<HumanVerificationDialogViewModel> _logger;

    private string? _receivedToken;
    private int _height;
    private bool _verificationTokenReceived;

    public HumanVerificationDialogViewModel(string url, ILogger<HumanVerificationDialogViewModel> logger)
    {
        Url = new Uri(url);
        _logger = logger;
        OnMessageReceivedCommand = new RelayCommand<CoreWebView2WebMessageReceivedEventArgs>(OnWebMessageReceived);
    }

    public string? Title => null;

    public Uri Url { get; }

    public ICommand OnMessageReceivedCommand { get; }

    public int Height
    {
        get => _height;
        private set => SetProperty(ref _height, value);
    }

    public bool VerificationTokenReceived
    {
        get => _verificationTokenReceived;
        private set => SetProperty(ref _verificationTokenReceived, value);
    }

    public string? ReceivedToken
    {
        get => _receivedToken;
        private set => SetProperty(ref _receivedToken, value);
    }

    private void OnWebMessageReceived(CoreWebView2WebMessageReceivedEventArgs? args)
    {
        if (args == null)
        {
            return;
        }

        try
        {
            var message = JsonSerializer.Deserialize<CaptchaMessage>(args.WebMessageAsJson, JsonSerializerOptions);

            switch (message?.Type)
            {
                case CaptchaMessageTypes.Height:
                    Height = message.Height + WebviewAddedHeight;
                    break;

                case CaptchaMessageTypes.TokenResponse:
                    ReceivedToken = message.Token;
                    VerificationTokenReceived = true;
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Failed to deserialized the web message: {Message}", ex.Message);
        }
    }
}
