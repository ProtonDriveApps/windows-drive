namespace Proton.Drive.Sdk.Sync.Shared.Health;

public enum FileConsistencyGuardFileReason
{
    None = 0,
    Partial = 1,
    Updated = 2,
    Deleted = 3,
    CreationTime = 4,
    SizeZero = 5,
    SizeMismatch = 6,
    LastBytes = 7,
    Checksum = 8,
    Uploaded = 9,
    SizeRule = 10,
    SizeUnknown = 11,
    ChecksumUnknown = 12,
}
