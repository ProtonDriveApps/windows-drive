using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Proton.Drive.App.Windows.Dialogs.HumanVerification;

internal partial class HumanVerificationDialogWindow : IClosableDialog
{
    private readonly ILogger<HumanVerificationDialogWindow> _logger;

    public HumanVerificationDialogWindow(CoreWebView2CreationProperties creationProperties, ILogger<HumanVerificationDialogWindow> logger)
    {
        _logger = logger;
        InitializeComponent();

        // WebView2's CreationProperties must be set before the control initializes its Core WebView2 environment.
        // If you bind CreationProperties to a view-model property, the binding is typically applied after the dialog's
        // components are constructed and often after the WebView2 begins initializing (especially if the Source property is set).
        // That results in the property being ignored, or the environment being in a bad state, and nothing navigates.
        WebView2.CreationProperties = creationProperties;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        WebView2.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // WebView2's CoreWebView2 must be fully initialized before we can access runtime settings.
        // This is why we cannot set IsReputationCheckingRequired or other CoreWebView2.Settings properties in the constructor
        // (the CoreWebView2 instance does not exist yet).
        // Placing this code in the Loaded event ensures the control is ready and safe to configure.
        _ = DisabledDiagnosticDataCollectionAsync().ContinueWith(t => LogException(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task DisabledDiagnosticDataCollectionAsync()
    {
        var options = new CoreWebView2EnvironmentOptions
        {
            IsCustomCrashReportingEnabled = true,
        };

        var env = await CoreWebView2Environment.CreateAsync(null, WebView2.CreationProperties.UserDataFolder, options).ConfigureAwait(true);

        await WebView2.EnsureCoreWebView2Async(env).ConfigureAwait(true);

        WebView2.CoreWebView2.Settings.IsReputationCheckingRequired = false;
    }

    private void LogException(AggregateException? exception)
    {
        if (exception is null)
        {
            return;
        }

        foreach (var ex in exception.InnerExceptions)
        {
            _logger.LogWarning("Disabling ViewWeb2 diagnostic data collection failed: {Message}", ex.Message);
        }
    }
}
