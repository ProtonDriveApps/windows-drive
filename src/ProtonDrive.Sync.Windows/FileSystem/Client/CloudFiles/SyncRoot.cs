using Vanara.PInvoke;

namespace ProtonDrive.Sync.Windows.FileSystem.Client.CloudFiles;

internal static class SyncRoot
{
    public static SyncRootConnection Connect(string syncRootPath, SyncRootCallbackDispatcher callbackDispatcher)
    {
        CldApi
            .CfConnectSyncRoot(
                syncRootPath,
                callbackDispatcher.CallbackTable,
                default,
                CldApi.CF_CONNECT_FLAGS.CF_CONNECT_FLAG_REQUIRE_PROCESS_INFO | CldApi.CF_CONNECT_FLAGS.CF_CONNECT_FLAG_REQUIRE_FULL_FILE_PATH,
                out var connectionKey)
            .ThrowExceptionForHR();

        return new SyncRootConnection(connectionKey);
    }
}
