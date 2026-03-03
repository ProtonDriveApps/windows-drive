namespace ProtonDrive.App.Health;

internal static class RemoteFileSizeVerifier
{
    public static bool IsNotAffected(long fileSize)
    {
        const int defaultFileBlockSize = 4 * 1024 * 1024;
        const int defaultDotNetBufferSize = 80 * 1024;
        const int driveSdkBufferSize = 132 * 1024;
        const int chunkSize = 4 * 1024;

        var remainder = fileSize % defaultDotNetBufferSize;

        if (remainder is > 0 and < chunkSize)
        {
            return false;
        }

        remainder = (fileSize % defaultFileBlockSize) % driveSdkBufferSize;

        if (remainder is > 0 and < chunkSize)
        {
            return false;
        }

        return true;
    }
}
