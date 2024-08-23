using System;

namespace ProtonDrive.App.Reporting;

public interface IErrorReporting
{
    bool IsEnabled { get; set; }

    void CaptureException(Exception ex);
}
