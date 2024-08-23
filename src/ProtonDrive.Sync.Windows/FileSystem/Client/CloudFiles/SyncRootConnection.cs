using System;
using Vanara.PInvoke;

namespace ProtonDrive.Sync.Windows.FileSystem.Client.CloudFiles;

internal sealed class SyncRootConnection : IDisposable
{
    private readonly CldApi.CF_CONNECTION_KEY _connectionKey;

    public SyncRootConnection(CldApi.CF_CONNECTION_KEY connectionKey)
    {
        _connectionKey = connectionKey;
    }

    public void Dispose()
    {
        try
        {
            CldApi.CfDisconnectSyncRoot(_connectionKey).ThrowExceptionForHR();
        }
        catch (ArgumentException)
        {
            // Connection key is not known to the platform. It might already been disconnected.
        }
    }
}
