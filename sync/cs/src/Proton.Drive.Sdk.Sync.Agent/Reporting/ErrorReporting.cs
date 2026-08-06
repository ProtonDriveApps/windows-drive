using Proton.Drive.Sdk.Sync.Agent.Configuration;
using Proton.Drive.Shared.Reporting;
using Sentry.Extensibility;

namespace Proton.Drive.Sdk.Sync.Agent.Reporting;

internal sealed class ErrorReporting : IErrorReporting
{
    private readonly SentryOptionsProvider _optionsProvider;

    private IDisposable _errorReportingHub;

    public ErrorReporting(SentryOptionsProvider optionsProvider)
    {
        _optionsProvider = optionsProvider;
        _errorReportingHub = DisabledHub.Instance;
    }

    public bool IsEnabled
    {
        get => SentrySdk.IsEnabled;
        set
        {
            if (value == SentrySdk.IsEnabled)
            {
                return;
            }

            if (value)
            {
                _errorReportingHub = SentrySdk.Init(_optionsProvider.GetOptions());
            }
            else
            {
                _errorReportingHub.Dispose();
            }
        }
    }

    public void CaptureException(Exception ex)
    {
        SentrySdk.CaptureException(ex);
    }

    public void CaptureException(Exception ex, ErrorTag tag)
    {
        using (SentrySdk.PushScope())
        {
            SentrySdk.ConfigureScope(scope => scope.SetTag(tag.Key, tag.Value));
            SentrySdk.CaptureException(ex);
        }
    }

    public void CaptureError(string message)
    {
        SentrySdk.CaptureMessage(message, SentryLevel.Error);
    }

    public void CaptureWarning(string message)
    {
        SentrySdk.CaptureMessage(message, SentryLevel.Warning);
    }
}
