using ProtonDrive.Sync.Shared.Health;

namespace ProtonDrive.App.Health;

internal static class FileConsistencyGuardFileModelExtensions
{
    public static bool UpdateStatus(this FileConsistencyGuardFileModel file, long remoteContentVersion)
    {
        if (file.Status is not FileConsistencyGuardFileStatus.None and not FileConsistencyGuardFileStatus.Inconsistent)
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
        if (file.Status is not FileConsistencyGuardFileStatus.None)
        {
            return false;
        }

        if (file is { LocalSize: 0, RemoteSize: 0 })
        {
            file.Status = FileConsistencyGuardFileStatus.Skipped;
            file.Reason = FileConsistencyGuardFileReason.SizeZero;
            file.Error = FileConsistencyGuardFileError.None;

            return true;
        }

        if (file.LocalSize == file.RemoteSize)
        {
            // Uploaded files have odd content version
            if (file.ContentVersion % 2 == 1)
            {
                file.Status = FileConsistencyGuardFileStatus.Skipped;
                file.Reason = FileConsistencyGuardFileReason.Uploaded;
                file.Error = FileConsistencyGuardFileError.None;

                return true;
            }

            if (RemoteFileSizeVerifier.IsNotAffected(file.RemoteSize))
            {
                file.Status = FileConsistencyGuardFileStatus.Skipped;
                file.Reason = FileConsistencyGuardFileReason.SizeRule;
                file.Error = FileConsistencyGuardFileError.None;

                return true;
            }
        }

        if (file.LocalSize != file.RemoteSize &&
            (file.RemoteHash == RemoteFileMetadataUpdater.MissingHashIndicator ||
            file.LocalHash == LocalFileMetadataUpdater.PartialFileIndicator))
        {
            file.Status = FileConsistencyGuardFileStatus.Inconsistent;
            file.Reason = FileConsistencyGuardFileReason.SizeMismatch;
            file.Error = FileConsistencyGuardFileError.None;

            return true;
        }

        if (file.LocalHash == LocalFileMetadataUpdater.PartialFileIndicator)
        {
            file.Status = FileConsistencyGuardFileStatus.Skipped;
            file.Reason = FileConsistencyGuardFileReason.Partial;
            file.Error = FileConsistencyGuardFileError.None;

            return true;
        }

        if (file.RemoteHash == RemoteFileMetadataUpdater.MissingHashIndicator)
        {
            if (file.LastByteIsNonZero == true)
            {
                file.Status = FileConsistencyGuardFileStatus.Consistent;
                file.Reason = FileConsistencyGuardFileReason.LastByte;
                file.Error = FileConsistencyGuardFileError.None;

                return true;
            }

            if (file.LastByteIsNonZero == false)
            {
                file.Status = FileConsistencyGuardFileStatus.Inconsistent;
                file.Reason = FileConsistencyGuardFileReason.LastByte;
                file.Error = FileConsistencyGuardFileError.None;

                return true;
            }
        }

        if (file.LocalHash is not null && file.RemoteHash is not null)
        {
            file.Status = file.RemoteHash.Equals(file.LocalHash)
                ? FileConsistencyGuardFileStatus.Consistent
                : FileConsistencyGuardFileStatus.Inconsistent;

            file.Reason = FileConsistencyGuardFileReason.Checksum;
            file.Error = FileConsistencyGuardFileError.None;

            return true;
        }

        return false;
    }
}
