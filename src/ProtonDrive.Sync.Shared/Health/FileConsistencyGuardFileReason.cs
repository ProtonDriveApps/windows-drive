namespace ProtonDrive.Sync.Shared.Health;

public enum FileConsistencyGuardFileReason
{
    None = 0,
    Partial = 1,
    Updated = 2,
    Deleted = 3,
    CreationTime = 4,
    SizeZero = 5,
    SizeMismatch = 6,
    LastByte = 7,
    Checksum = 8,
    Uploaded = 9,
    SizeRule = 10,
}
