using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Proton.Drive.App.Windows.Services;
using Proton.Drive.Sdk.Sync.Client.Configuration;
using Proton.Drive.Shared.HumanVerification;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows.Dialogs.HumanVerification;

internal sealed class HumanVerifier : IHumanVerifier
{
    private readonly string _coreBaseUrl;
    private readonly IScheduler _uiDispatcherScheduler;
    private readonly IDialogService _dialogService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<HumanVerifier> _logger;

    private int _isVerifying;

    public HumanVerifier(
        DriveApiConfig config,
        [FromKeyedServices("Dispatcher")] IScheduler scheduler,
        IDialogService dialogService,
        ILoggerFactory loggerFactory)
    {
        _coreBaseUrl = config.CoreBaseUrl?.ToString() ?? throw new InvalidOperationException("Core base URL is missing");
        _uiDispatcherScheduler = scheduler;
        _dialogService = dialogService;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<HumanVerifier>();
    }

    public async Task<string?> VerifyAsync(string captchaToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _isVerifying, 1, 0) != 0)
        {
            _logger.LogInformation("Human verification already in progress, failing concurrent request");
            return null;
        }

        try
        {
            if (!IsSupported())
            {
                return null;
            }

            var captchaDialogViewModel = new HumanVerificationDialogViewModel(
                $"{_coreBaseUrl}v4/captcha?Token={captchaToken}",
                _loggerFactory.CreateLogger<HumanVerificationDialogViewModel>());

            _logger.LogInformation("Opening Human verification dialog");

            await _uiDispatcherScheduler
                .Schedule(() => _dialogService.ShowHumanVerificationDialog(captchaDialogViewModel), cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation(
                "Human verification {SucceededOrFailed}",
                string.IsNullOrEmpty(captchaDialogViewModel.ReceivedToken) ? "failed" : "succeeded");

            return captchaDialogViewModel.ReceivedToken;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Human verification cancelled");
            throw;
        }
        finally
        {
            Interlocked.Exchange(ref _isVerifying, 0);
        }
    }

    private bool IsSupported()
    {
        try
        {
            var browserVersionString = CoreWebView2Environment.GetAvailableBrowserVersionString();

            var isSupported = !string.IsNullOrEmpty(browserVersionString) && browserVersionString != "0.0.0.0";

            if (isSupported)
            {
                _logger.LogInformation("Human verification is supported, WebView2 browser version: \"{BrowserVersionString}\"", browserVersionString);
            }
            else
            {
                _logger.LogWarning("Human verification not supported, WebView2 browser version: \"{BrowserVersionString}\"", browserVersionString);
            }

            return isSupported;
        }
        catch (Exception e)
        {
            _logger.LogWarning("Human verification not supported, WebView2 not installed or supported: {ErrorMessage}", e.Message);
            return false;
        }
    }
}
