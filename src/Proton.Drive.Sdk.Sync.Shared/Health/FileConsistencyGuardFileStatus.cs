namespace Proton.Drive.Sdk.Sync.Shared.Health;

public enum FileConsistencyGuardFileStatus
{
    None = 0,
    Skipped = 1,
    Consistent = 2,
    Inconsistent = 3,
    Sanitized = 4,
}
