namespace Proton.Drive.Shared.Reporting;

public interface IErrorReporting
{
    bool IsEnabled { get; set; }

    void CaptureException(Exception ex, ErrorTag tag);

    void CaptureException(Exception ex);

    void CaptureError(string message);

    void CaptureWarning(string message);
}
