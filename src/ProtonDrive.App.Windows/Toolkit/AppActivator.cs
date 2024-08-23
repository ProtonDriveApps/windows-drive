using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.InterProcessCommunication;
using ProtonDrive.App.Windows.Interop;
using ProtonDrive.App.Windows.InterProcessCommunication;

namespace ProtonDrive.App.Windows.Toolkit;

public static class AppActivator
{
    public static void ActivateExistingProcessWindow()
    {
        ActivateExistingProcessWindowAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private static async Task ActivateExistingProcessWindowAsync(CancellationToken cancellationToken)
    {
        var windowHandle = await ShowWindowAsync(cancellationToken).ConfigureAwait(false);

        ActivateWindow(windowHandle);
    }

    /// <summary>
    /// Requests other running app instance to show the window.
    /// Returns the window handle on success, default value on failure.
    /// </summary>
    private static async Task<long> ShowWindowAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await UnsafeShowWindowAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or JsonException)
        {
            // Ignore
            return default;
        }
    }

    private static async Task<long> UnsafeShowWindowAsync(CancellationToken cancellationToken)
    {
        var ipcClient = await NamedPipeBasedIpcClient.ConnectAsync(NamedPipeBasedIpcServer.PipeName, TimeSpan.FromSeconds(1), cancellationToken)
            .ConfigureAwait(false);

        await using (ipcClient.ConfigureAwait(false))
        {
            await ipcClient.WriteAsync(IpcMessageType.AppActivationCommand, cancellationToken).ConfigureAwait(false);

            return await ipcClient.ReadAsync<long>(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Brings the window of other app instance to the foreground.
    /// </summary>
    private static void ActivateWindow(long windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        User32.SetForegroundWindow(new IntPtr(windowHandle));
    }
}
