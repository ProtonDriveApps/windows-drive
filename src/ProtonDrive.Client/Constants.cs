namespace ProtonDrive.Client;

public static class Constants
{
    public static readonly int MaxThumbnailSize = 1_024 * 60; // 60 KiB

    public static readonly int FileBlockSize = 1_024 * 1_024 * 4; // 4 MiB

    public static readonly int MaxBlockEncryptionOverhead = 56;
}
