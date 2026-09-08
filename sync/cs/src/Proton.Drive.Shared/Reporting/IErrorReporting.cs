namespace Proton.Drive.Shared.Reporting;

public interface IErrorReporting
{
    bool IsEnabled { get; set; }

    void CaptureException(Exception ex, params ErrorTag[] tags);

    void CaptureException(Exception ex);

    void CaptureError(string message);

    void CaptureWarning(string message);
}
