using System;
using ProtonDrive.App.SystemIntegration;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class SystemClipboard : IClipboard
{
    public void SetText(string value)
    {
        // Depending on the Windows account configuration, the clipboard contents can be persisted in the user's clipboard history,
        // synchronized across devices and uploaded to the cloud, due to the Cloud Clipboard feature. As a result,
        // it is recommended to restrict the scope of the clipboard implementation to avoid unintended information leaks.
        // TODO: Apply more restrictive options to the contents copied to the user's clipboard by using the options Microsoft (WinRT documentation):
        // https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.datatransfer.clipboardcontentoptions?view=winrt-22621
        throw new NotSupportedException();
    }
}
