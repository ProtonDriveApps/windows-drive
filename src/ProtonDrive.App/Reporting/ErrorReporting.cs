using System;
using Sentry;
using Sentry.Extensibility;

namespace ProtonDrive.App.Reporting;

public sealed class ErrorReporting : IErrorReporting
{
    private readonly SentryOptions _options;

    private IDisposable _errorReportingHub;

    private ErrorReporting(SentryOptions options)
    {
        _options = options;

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
                _errorReportingHub = SentrySdk.Init(_options);
            }
            else
            {
                _errorReportingHub.Dispose();
            }
        }
    }

    public static IErrorReporting Initialize(SentryOptions options)
    {
        return new ErrorReporting(options);
    }

    public void CaptureException(Exception ex)
    {
        SentrySdk.CaptureException(ex);
    }
}
