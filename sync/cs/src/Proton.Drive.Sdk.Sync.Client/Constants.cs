namespace Proton.Drive.Sdk.Sync.Client;

public static class Constants
{
    public const int MaxSmallThumbnailSizeOnRemote = 1_024 * 60; // 60 KiB

    public const int MaxLargeThumbnailSizeOnRemote = 1_024 * 1_024; // 1 MiB

    public const int MaxThumbnailEncryptionOverhead = 512;

    public const int FileBlockSize = 1_024 * 1_024 * 4; // 4 MiB

    public const int MaxBlockEncryptionOverhead = 56;
}
