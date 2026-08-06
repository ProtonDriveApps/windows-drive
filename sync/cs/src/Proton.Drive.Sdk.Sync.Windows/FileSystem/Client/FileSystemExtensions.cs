using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Windows.FileSystem.OnDemand;
using Proton.Drive.Shared.IO;
using static Vanara.PInvoke.CldApi;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client;

internal static class FileSystemExtensions
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.None, // By default, Hidden and System attributes are skipped
    };

    public static NodeInfo<long> ToNodeInfo(this FileSystemEntry entry, long parentId)
    {
        return new NodeInfo<long>()
            .WithId(entry.ObjectId)
            .WithName(entry.Name)
            .WithAttributes(entry.Attributes)
            .WithLastWriteTimeUtc(entry.LastWriteTimeUtc)
            .WithSize(entry.Size)
            .WithParentId(parentId)
            .WithPlaceholderState(entry.GetPlaceholderState());
    }

    public static NodeInfo<long> ToNodeInfo(this FileSystemObject fsObject, long parentId, bool refresh)
    {
        try
        {
            if (refresh)
            {
                fsObject.Refresh();
            }

            return new NodeInfo<long>()
                .WithId(fsObject.ObjectId)
                .WithName(fsObject.Name)
                .WithAttributes(fsObject.Attributes)
                .WithLastWriteTimeUtc(fsObject.LastWriteTimeUtc)
                .WithSize(fsObject.Size)
                .WithParentId(parentId)
                .WithPlaceholderState(fsObject.GetPlaceholderState());
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id: null, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static PlaceholderState GetPlaceholderState(this FileSystemObject fsObject)
    {
        try
        {
            return FileSystemObjectCloudFilesExtensions.GetPlaceholderState(fsObject);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id: null, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static void SetAttributes(this FileSystemObject fsObject, NodeInfo<long> info)
    {
        const FileAttributes affectedAttributes =
            FileAttributes.Hidden
            | FileAttributes.Directory
            | FileAttributes.ReadOnly
            | FileAttributes.System
            | FileAttributes.Temporary;

        try
        {
            var newAttributes = (fsObject.Attributes & ~affectedAttributes) | (info.Attributes & affectedAttributes);

            if (fsObject.Attributes != newAttributes)
            {
                fsObject.Attributes = newAttributes != default ? newAttributes : FileAttributes.Normal;
            }
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, info.Id, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static void SetLastWriteTime(this FileSystemObject fsObject, NodeInfo<long> info)
    {
        try
        {
            fsObject.LastWriteTimeUtc = info.LastWriteTimeUtc;
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, info.Id, includeObjectId: false, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static void Rename(this FileSystemObject fsObject, string newName, bool includeObjectId, bool replaceIfExists = false)
    {
        try
        {
            fsObject.Rename(newName, replaceIfExists);
        }
        catch (ArgumentException ex)
        {
            throw new FileSystemClientException<long>(FileSystemErrorCode.InvalidName, includeObjectId ? fsObject.ObjectId : default, ex);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, fsObject.ObjectId, includeObjectId, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static void Move(this FileSystemObject fsObject, FileSystemDirectory newParent, string newName, bool includeObjectId, bool replaceIfExists = false)
    {
        try
        {
            fsObject.Move(newParent, newName, replaceIfExists);
        }
        catch (ArgumentException ex)
        {
            throw new FileSystemClientException<long>(FileSystemErrorCode.InvalidName, includeObjectId ? fsObject.ObjectId : default, ex);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, fsObject.ObjectId, includeObjectId, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static void TryDelete(this FileSystemFile file)
    {
        try
        {
            file.Delete();
        }
        catch
        {
            // Ignore
        }
    }

    public static void Delete(this FileSystemObject fsObject, NodeInfo<long> info)
    {
        fsObject.Delete(info.Attributes.HasFlag(FileAttributes.ReadOnly));
    }

    public static FileSystemFileAccess GetAccessForDeletion(this FileAttributes attributes, bool deleteReadOnly)
    {
        var access = FileSystemFileAccess.Delete;

        if (!attributes.HasFlag(FileAttributes.Directory))
        {
            if (deleteReadOnly && attributes.HasFlag(FileAttributes.ReadOnly))
            {
                // Need to remove read-only attribute before deleting file
                access |= FileSystemFileAccess.WriteAttributes;
            }
        }
        else
        {
            access |= FileSystemFileAccess.ReadData;
        }

        return access;
    }

    private static void Delete(this FileSystemObject fsObject, bool deleteReadOnly)
    {
        switch (fsObject)
        {
            case FileSystemDirectory folder:
                folder.DeleteFolder(deleteReadOnly);
                break;
            case FileSystemFile file:
                file.DeleteFile(deleteReadOnly);
                break;
            default:
                throw new InvalidOperationException();
        }
    }

    private static void DeleteFolder(this FileSystemDirectory folder, bool deleteReadOnly)
    {
        foreach (var childEntry in folder.EnumerateFileSystemEntries(default, EnumerationOptions))
        {
            using var fsObject = FileSystemObject.Open(
                Path.Combine(folder.FullPath, childEntry.Name),
                FileMode.Open,
                GetAccessForDeletion(childEntry.Attributes, deleteReadOnly),
                FileShare.Read | FileShare.Delete,
                FileOptions.None);

            Delete(fsObject, deleteReadOnly);
        }

        folder.Delete();
    }

    private static void DeleteFile(this FileSystemFile file, bool deleteReadOnly)
    {
        if (!deleteReadOnly)
        {
            file.ThrowIfReadOnlyFile();
        }
        else if (file.Attributes.HasFlag(FileAttributes.ReadOnly))
        {
            file.Attributes &= ~FileAttributes.ReadOnly;
        }

        file.Delete();
    }

    public static void ConvertToPlaceholder(this FileSystemObject fsObject, CF_PLACEHOLDER_CREATE_INFO creationInfo, CF_CONVERT_FLAGS flags)
    {
        try
        {
            FileSystemObjectCloudFilesExtensions.ConvertToPlaceholder(fsObject, creationInfo, flags);

            // Converting to a placeholder changes file attributes
            fsObject.Refresh();
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, fsObject.ObjectId, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static void UpdatePlaceholder(this FileSystemObject fsObject, CF_FS_METADATA metadata, CF_UPDATE_FLAGS flags)
    {
        try
        {
            long updateUsn = 0;

            // The caller must acquire an exclusive handle to the file if it intends to dehydrate the file or data corruption can occur.
            // Note that the CloudFilter platform does not validate the exclusiveness of the handle.
            CfUpdatePlaceholder(
                    fsObject.FileHandle,
                    metadata,
                    FileIdentity: IntPtr.Zero,
                    FileIdentityLength: 0,
                    DehydrateRangeArray: default,
                    DehydrateRangeCount: default,
                    flags,
                    ref updateUsn)
                .ThrowExceptionForHR();
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, fsObject.ObjectId, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static void SetInSync(this FileSystemObject fsObject)
    {
        try
        {
            CfSetInSyncState(fsObject.FileHandle, CF_IN_SYNC_STATE.CF_IN_SYNC_STATE_IN_SYNC, CF_SET_IN_SYNC_FLAGS.CF_SET_IN_SYNC_FLAG_NONE)
                .ThrowExceptionForHR();
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, fsObject.ObjectId, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static void SetPinState(this FileSystemObject fsObject, CF_PIN_STATE state, CF_SET_PIN_FLAGS flags)
    {
        try
        {
            CfSetPinState(fsObject.FileHandle, state, flags).ThrowExceptionForHR();
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, fsObject.ObjectId, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static void ThrowIfIdentityMismatch(this FileSystemObject actual, long expectedId)
    {
        if (expectedId == default)
        {
            return;
        }

        long actualId;

        try
        {
            actualId = actual.ObjectId;
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id: null, out var mappedException))
        {
            throw mappedException;
        }

        ThrowIfIdentityMismatch(actualId, expectedId);
    }

    public static void ThrowIfIdentityMismatch(this FileSystemEntry actual, long expectedId)
    {
        ThrowIfIdentityMismatch(actual.ObjectId, expectedId);
    }

    public static void ThrowIfTypeMismatch(this FileSystemObject actual, NodeInfo<long> expected)
    {
        FileAttributes actualAttributes;

        try
        {
            actualAttributes = actual.Attributes;
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, expected.Id, out var mappedException))
        {
            throw mappedException;
        }

        if (((actualAttributes ^ expected.Attributes) & FileAttributes.Directory) != 0)
        {
            throw new FileSystemClientException<long>(
                $"Wrong file system object type, expected {ObjectType(expected.Attributes)} but found {ObjectType(actual.Attributes)}",
                FileSystemErrorCode.MetadataMismatch,
                expected.Id);
        }

        static string ObjectType(FileAttributes attributes) =>
            (attributes & FileAttributes.Directory) != 0 ? "Directory" : "File";
    }

    public static void ThrowIfMetadataMismatch(this FileSystemObject actual, NodeInfo<long> expected)
    {
        FileAttributes actualAttributes;
        DateTime actualLastWriteTimeUtc;
        long actualSize;

        try
        {
            actualLastWriteTimeUtc = actual.LastWriteTimeUtc;
            actualAttributes = actual.Attributes;
            actualSize = actual.Size;
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id: null, out var mappedException))
        {
            throw mappedException;
        }

        if (expected.LastWriteTimeUtc != DateTime.MinValue && actualLastWriteTimeUtc != expected.LastWriteTimeUtc)
        {
            throw new FileSystemClientException<long>(
                "Local optimistic locking failure: Last write time has diverged",
                FileSystemErrorCode.MetadataMismatch,
                expected.Id);
        }

        if (actualAttributes.HasFlag(FileAttributes.Directory))
        {
            return;
        }

        /* The following metadata checks apply to files only */

        if (expected.Size >= 0 && actualSize != expected.Size)
        {
            throw new FileSystemClientException<long>(
                "Local optimistic locking failure: File size has diverged",
                FileSystemErrorCode.MetadataMismatch,
                expected.Id);
        }
    }

    public static void ThrowIfPartial(this FileSystemObject fsObject, long id)
    {
        if (fsObject.GetPlaceholderState().HasFlag(PlaceholderState.Partial))
        {
            throw new FileSystemClientException<long>(FileSystemErrorCode.Partial, id, innerException: null);
        }
    }

    public static void ThrowIfReadOnlyFile(this FileSystemObject fsObject)
    {
        if (!fsObject.Attributes.HasFlag(FileAttributes.Directory) &&
            fsObject.Attributes.HasFlag(FileAttributes.ReadOnly))
        {
            throw new FileSystemClientException<long>("File is read-only", FileSystemErrorCode.ReadOnlyFile, fsObject.ObjectId, innerException: null);
        }
    }

    private static void ThrowIfIdentityMismatch(long actualId, long expectedId)
    {
        if (!expectedId.Equals(default) && !actualId.Equals(expectedId))
        {
            throw new FileSystemClientException<long>(
                $"Wrong file system object ID, expected {expectedId} but found {actualId}",
                FileSystemErrorCode.IdentityMismatch,
                expectedId);
        }
    }
}
