using Proton.Drive.Sdk.Sync.Shared.Health;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

internal static class FileConsistencyGuardFileModelExtensions
{
    public static bool UpdateStatus(this FileConsistencyGuardFileModel file, long remoteContentVersion)
    {
        if (file.Status is not FileConsistencyGuardFileStatus.None)
        {
            return false;
        }

        if (file.ContentVersion == remoteContentVersion)
        {
            return file.UpdateStatus();
        }

        file.Status = FileConsistencyGuardFileStatus.Skipped;
        file.Reason = FileConsistencyGuardFileReason.Updated;
        file.Error = FileConsistencyGuardFileError.None;

        return true;
    }

    public static bool UpdateStatus(this FileConsistencyGuardFileModel file)
    {
        if (file.Status is not FileConsistencyGuardFileStatus.None and not FileConsistencyGuardFileStatus.Skipped)
        {
            return false;
        }

        // Zero size file
        if (file.LocalSize == 0 && (file.RemoteSize == 0 || file.RemotePlainSize == 0))
        {
            file.Status = FileConsistencyGuardFileStatus.Consistent;
            file.Reason = FileConsistencyGuardFileReason.SizeZero;
            file.Error = FileConsistencyGuardFileError.None;

            return true;
        }

        // Uploaded file (has odd content version)
        if (file.ContentVersion % 2 == 1)
        {
            file.Status = FileConsistencyGuardFileStatus.Skipped;
            file.Reason = FileConsistencyGuardFileReason.Uploaded;
            file.Error = FileConsistencyGuardFileError.None;

            return true;
        }

        // File size match
        if (file.LocalSize == file.RemoteSize || file.LocalSize == file.RemotePlainSize)
        {
            // Partial local file
            if (file.LocalHash == LocalFileMetadataUpdater.PartialFileIndicator)
            {
                file.Status = FileConsistencyGuardFileStatus.Consistent;
                file.Reason = FileConsistencyGuardFileReason.Partial;
                file.Error = FileConsistencyGuardFileError.None;

                return true;
            }
        }

        if (file.LocalHash is null || file.RemoteHash is null)
        {
            return false;
        }

        // Both hashes available
        if (file.LocalHash != LocalFileMetadataUpdater.PartialFileIndicator
            && file.RemoteHash != RemoteFileMetadataUpdater.MissingHashIndicator)
        {
            file.Status = file.RemoteHash.Equals(file.LocalHash)
                ? FileConsistencyGuardFileStatus.Consistent
                : FileConsistencyGuardFileStatus.Inconsistent;

            file.Reason = FileConsistencyGuardFileReason.Checksum;
            file.Error = FileConsistencyGuardFileError.None;

            return true;
        }

        // File size match
        if (file.LocalSize == file.RemoteSize || file.LocalSize == file.RemotePlainSize)
        {
            // Full local file
            if (file.TrailingZeroBytesLength is not null)
            {
                var numberOfBytesToVerify = FileSizeVerifier.GetNumberOfBytesToVerify(file.LocalSize);

                if (numberOfBytesToVerify == 0)
                {
                    file.Status = FileConsistencyGuardFileStatus.Skipped;
                    file.Reason = FileConsistencyGuardFileReason.SizeRule;
                    file.Error = FileConsistencyGuardFileError.None;

                    return true;
                }

                file.Status = FileConsistencyGuardFileStatus.Skipped;

                file.Reason = file.TrailingZeroBytesLength < numberOfBytesToVerify
                    ? FileConsistencyGuardFileReason.LastBytes
                    : FileConsistencyGuardFileReason.ChecksumUnknown;

                file.Error = FileConsistencyGuardFileError.None;

                return true;
            }
        }

        // File size mismatch
        else if (file.TryGetRemotePlainSize(out var remotePlainSize) && file.LocalSize != remotePlainSize)
        {
            file.Status = FileConsistencyGuardFileStatus.Inconsistent;
            file.Reason = FileConsistencyGuardFileReason.SizeMismatch;
            file.Error = FileConsistencyGuardFileError.None;

            return true;
        }

        // File size neither clearly match neither mismatch (local size does not match remote, remote plain size is unknown)
        else
        {
            // Full local file
            if (file.TrailingZeroBytesLength is not null)
            {
                var numberOfBytesToVerify = FileSizeVerifier.GetNumberOfBytesToVerify(file.LocalSize);

                if (numberOfBytesToVerify != 0)
                {
                    file.Status = FileConsistencyGuardFileStatus.Skipped;

                    file.Reason = file.TrailingZeroBytesLength < numberOfBytesToVerify
                        ? FileConsistencyGuardFileReason.LastBytes
                        : FileConsistencyGuardFileReason.ChecksumUnknown;

                    file.Error = FileConsistencyGuardFileError.None;

                    return true;
                }
            }

            file.Status = FileConsistencyGuardFileStatus.Skipped;

            file.Reason = FileSizeVerifier.FileIsNotTruncated(file.LocalSize)
                ? FileConsistencyGuardFileReason.SizeRule
                : FileConsistencyGuardFileReason.SizeUnknown;

            file.Error = FileConsistencyGuardFileError.None;

            return true;
        }

        return false;
    }

    private static bool TryGetRemotePlainSize(this FileConsistencyGuardFileModel file, out long remotePlainSize)
    {
        if (file.RemotePlainSize is not null)
        {
            remotePlainSize = file.RemotePlainSize.Value;
            return true;
        }

        if (file.RemoteSizeOnStorage is not null && file.RemoteSize < file.RemoteSizeOnStorage)
        {
            remotePlainSize = file.RemoteSize;
            return true;
        }

        remotePlainSize = 0;
        return false;
    }
}
