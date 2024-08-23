using System;
using System.ComponentModel;
using System.Diagnostics;

namespace ProtonDrive.App.Windows.Configuration.Hyperlinks;

internal sealed class UrlOpener : IUrlOpener
{
    public void OpenUrl(string url)
    {
        var processStartInfo = new ProcessStartInfo(url)
        {
            UseShellExecute = true,
            Verb = "open",
        };

        try
        {
            Process.Start(processStartInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or UnauthorizedAccessException)
        {
            // Silently ignore
        }
    }
}
