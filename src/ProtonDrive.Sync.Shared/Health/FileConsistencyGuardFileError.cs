namespace ProtonDrive.Sync.Shared.Health;

public enum FileConsistencyGuardFileError
{
    None = 0,
    RootDisabled = 1,
    LocalDiverged = 2,
    RemoteDiverged = 3,
    LocalFailed = 4,
    RemoteFailed = 5,
}
