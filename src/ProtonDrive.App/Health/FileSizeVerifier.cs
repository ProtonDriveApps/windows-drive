namespace ProtonDrive.App.Health;

internal static class FileSizeVerifier
{
    private const int DefaultFileBlockSize = 4 * 1024 * 1024;
    private const int DriveSdkBufferSize = 132 * 1024;
    private const int ChunkSize = 4 * 1024;

    public static long GetNumberOfBytesToVerify(long remoteFileSize)
    {
        var lastBlockSize = remoteFileSize % DefaultFileBlockSize;
        var driveSdkRemainder = lastBlockSize % DriveSdkBufferSize;
        return driveSdkRemainder < ChunkSize ? driveSdkRemainder : 0;
    }

    public static bool FileIsNotTruncated(long localFileSize)
    {
        return GetNumberOfBytesToVerify(localFileSize + 1) != 1;
    }
}
