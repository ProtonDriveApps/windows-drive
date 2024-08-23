namespace ProtonDrive.App.InterProcessCommunication;

public static class IpcMessageType
{
    public static readonly string SyncRootPathsQuery = nameof(SyncRootPathsQuery);
    public static readonly string RemoteIdsQuery = nameof(RemoteIdsQuery);
    public static readonly string AppActivationCommand = nameof(AppActivationCommand);
    public static readonly string OpenDocumentCommand = nameof(OpenDocumentCommand);
}
