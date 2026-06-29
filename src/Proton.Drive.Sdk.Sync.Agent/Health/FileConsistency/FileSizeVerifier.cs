namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

internal static class FileSizeVerifier
{
    internal const int DefaultFileBlockSize = 4 * 1024 * 1024;
    internal const int DriveSdkBufferSize = 132 * 1024;
    internal const int ChunkSize = 4 * 1024;

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
