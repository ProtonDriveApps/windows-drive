using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;
using Vanara.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.CldApi;
using Kernel32 = Vanara.PInvoke.Kernel32;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal static class NodeInfoExtensions
{
    private const FileAttributes AffectedAttributes = FileAttributes.Hidden
                                                    | FileAttributes.Directory
                                                    | FileAttributes.ReadOnly
                                                    | FileAttributes.System
                                                    | FileAttributes.Temporary;

    public static FileSystemObject Open(this NodeInfo<long> info, FileSystemFileAccess access, FileShare share)
    {
        try
        {
            var fsObject = FileSystemObject.Open(info.Path, FileMode.Open, access, share, FileOptions.None);

            try
            {
                fsObject.ThrowIfTypeMismatch(info);
                fsObject.ThrowIfIdentityMismatch(info.Id);

                return fsObject;
            }
            catch
            {
                fsObject.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, info.Id, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static FileSystemFile OpenAsFileForWriting(this NodeInfo<long> info)
    {
        var file = info.OpenAsFile(FileMode.Open, FileSystemFileAccess.ReadWrite, FileShare.None);

        try
        {
            file.ThrowIfMetadataMismatch(info);

            try
            {
                file.Truncate();
            }
            catch (Exception ex) when (ExceptionMapping.TryMapException(ex, file.ObjectId, out var mappedException))
            {
                throw mappedException;
            }

            return file;
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    public static FileSystemFile OpenAsFile(this NodeInfo<long> info, FileSystemFileAccess access, FileShare share)
    {
        return OpenAsFile(info, FileMode.Open, access, share);
    }

    public static FileSystemFile OpenAsFile(
        this NodeInfo<long> info,
        FileMode mode,
        FileSystemFileAccess access,
        FileShare share,
        FileAttributes attributes = default,
        FileOptions options = FileOptions.Asynchronous,
        FileSystemFile? templateFile = null)
    {
        try
        {
            attributes &= AffectedAttributes;

            var file = FileSystemFile.Open(info.Path, mode, access, share, attributes, options, templateFile?.FileHandle);

            try
            {
                file.ThrowIfIdentityMismatch(info.Id);

                return file;
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, info.Id, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static FileSystemDirectory OpenAsDirectory(this NodeInfo<long> info, FileSystemFileAccess access, FileShare share)
    {
        try
        {
            var directory = FileSystemDirectory.Open(info.Path, access, share);

            try
            {
                directory.ThrowIfIdentityMismatch(info.Id);

                return directory;
            }
            catch
            {
                directory.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, info.Id, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static FileSystemDirectory OpenParentDirectory(this NodeInfo<long> info, FileSystemFileAccess access, FileShare share)
    {
        var pathSpan = Path.TrimEndingDirectorySeparator(info.Path.AsSpan());
        var parentDirectoryPath = Path.GetDirectoryName(pathSpan);

        try
        {
            var directory = FileSystemDirectory.Open(new string(parentDirectoryPath), access, share);

            try
            {
                directory.ThrowIfIdentityMismatch(info.ParentId);

                return directory;
            }
            catch
            {
                directory.Dispose();
                throw;
            }
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, info.ParentId, out var mappedException))
        {
            throw mappedException;
        }
    }

    public static FileSystemFile CreateTemporaryFile(this NodeInfo<long> info, FileSystemFile templateFile)
    {
        var tempFile = info.OpenAsFile(
            FileMode.CreateNew,
            FileSystemFileAccess.ReadWrite | FileSystemFileAccess.Delete,
            FileShare.None,
            info.Attributes | FileAttributes.Hidden,
            templateFile: templateFile);

        try
        {
            try
            {
                tempFile.CreationTimeUtc = templateFile.CreationTimeUtc;

                if (!tempFile.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    tempFile.Attributes |= FileAttributes.Hidden;
                }
            }
            catch (Exception ex) when (ExceptionMapping.TryMapException(ex, tempFile.ObjectId, includeObjectId: false, out var mappedException))
            {
                throw mappedException;
            }
        }
        catch
        {
            tempFile.TryDelete();
            tempFile.Dispose();
            throw;
        }

        return tempFile;
    }

    public static NodeInfo<long> CreatePlaceholderFile(this NodeInfo<long> info, FileSystemDirectory parentDirectory)
    {
        using var file = info.CreatePlaceholderFile(parentDirectory, FileSystemFileAccess.ReadAttributes, FileShare.Read);

        return file.ToNodeInfo(parentDirectory.ObjectId, refresh: false);
    }

    public static FileSystemFile CreatePlaceholderFile(
        this NodeInfo<long> info,
        FileSystemDirectory parentDirectory,
        FileSystemFileAccess access,
        FileShare share)
    {
        using var placeholderCreationInfo = info.ToPlaceholderCreationInfo();

        try
        {
            CfCreatePlaceholders(
                    parentDirectory.FullPath,
                    new[] { placeholderCreationInfo.Value },
                    1,
                    CF_CREATE_FLAGS.CF_CREATE_FLAG_STOP_ON_ERROR,
                    out _)
                .ThrowExceptionForHR();
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, id: default, out var mappedException))
        {
            throw mappedException;
        }

        // We don't know File ID of the just created placeholder file
        var newInfo = info.Copy().WithId(default);

        return newInfo.OpenAsFile(access, share);
    }

    public static void SetInSync(this NodeInfo<long> info, out PlaceholderState placeholderState, out FileAttributes attributes)
    {
        using var fsObject = info.Open(FileSystemFileAccess.WriteAttributes, FileShare.Read);

        fsObject.ThrowIfMetadataMismatch(info);

        attributes = fsObject.Attributes;
        placeholderState = fsObject.GetPlaceholderState().ThrowIfInvalid();

        if (fsObject.Attributes.IsExcluded())
        {
            // The file or folder is excluded from sync
            return;
        }

        if (placeholderState.HasFlag(PlaceholderState.InSync))
        {
            return;
        }

        if (placeholderState.HasFlag(PlaceholderState.Placeholder))
        {
            fsObject.SetInSync();
        }
        else
        {
            using var placeholderCreationInfo = info.ToPlaceholderCreationInfo();

            fsObject.ConvertToPlaceholder(placeholderCreationInfo.Value, CF_CONVERT_FLAGS.CF_CONVERT_FLAG_MARK_IN_SYNC);
        }

        // File attributes and/or placeholder state have changed
        attributes = fsObject.Attributes;
        placeholderState = fsObject.GetPlaceholderState().ThrowIfInvalid();
    }

    public static unsafe void NotifyChanges(this NodeInfo<long> info)
    {
        fixed (char* pathPointer = info.Path)
        {
            Shell32.SHChangeNotify(Shell32.SHCNE.SHCNE_UPDATEITEM, Shell32.SHCNF.SHCNF_PATHW, new nuint(pathPointer));
        }
    }

    public static void Dehydrate(this NodeInfo<long> info)
    {
        // The caller must acquire an exclusive handle to the file if it intends to dehydrate the file or data corruption can occur.
        // Note that the CloudFilter platform does not validate the exclusiveness of the handle.
        using var fsObject = info.OpenAsFile(FileSystemFileAccess.WriteAttributes, FileShare.None);

        fsObject.ThrowIfMetadataMismatch(info);

        using var placeholderCreationInfo = info.ToPlaceholderCreationInfo();

        fsObject.UpdatePlaceholder(
            placeholderCreationInfo.Value.FsMetadata,
            CF_UPDATE_FLAGS.CF_UPDATE_FLAG_VERIFY_IN_SYNC | CF_UPDATE_FLAGS.CF_UPDATE_FLAG_DEHYDRATE);
    }

    public static Disposable<CF_PLACEHOLDER_CREATE_INFO> ToPlaceholderCreationInfo(this NodeInfo<long> info)
    {
        // File placeholder creation fails if no identity is specified
        var identity = new SafeCoTaskMemString(string.Empty);

        var lastWriteLongFileTime = info.LastWriteTimeUtc >= DateTime.FromFileTimeUtc(0) ? info.LastWriteTimeUtc.ToFileTimeUtc() : 0;
        var lastWriteFileTime = new FILETIME
        {
            dwLowDateTime = (int)(lastWriteLongFileTime & 0xffffffff),
            dwHighDateTime = (int)(lastWriteLongFileTime >> 32),
        };

        var placeholder = new CF_PLACEHOLDER_CREATE_INFO
        {
            FileIdentity = identity,
            FileIdentityLength = identity.Size,
            RelativeFileName = info.Name,
            Flags = CF_PLACEHOLDER_CREATE_FLAGS.CF_PLACEHOLDER_CREATE_FLAG_MARK_IN_SYNC,
            FsMetadata = new CF_FS_METADATA
            {
                FileSize = info.Size,
                BasicInfo = new Kernel32.FILE_BASIC_INFO
                {
                    FileAttributes = (FileFlagsAndAttributes)info.Attributes,
                    LastWriteTime = lastWriteFileTime,
                    ChangeTime = default,
                    CreationTime = default,
                    LastAccessTime = default,
                },
            },
        };

        if ((info.Attributes & FileAttributes.Directory) != 0)
        {
            placeholder.Flags |= CF_PLACEHOLDER_CREATE_FLAGS.CF_PLACEHOLDER_CREATE_FLAG_DISABLE_ON_DEMAND_POPULATION;
            placeholder.FsMetadata.FileSize = 0;
        }

        return Disposable.Create(placeholder, identity);
    }

    public static NodeInfo<long> ToTempFileInfo(this NodeInfo<long> info, string tempFileName)
    {
        var fileName = string.IsNullOrEmpty(tempFileName) ? info.Name : tempFileName;

        return info.Copy()
            .WithId(default)
            .WithName(fileName)
            .WithPath(Path.Combine(Path.GetDirectoryName(info.Path) ?? string.Empty, fileName))
            .WithSize(0)
            .WithAttributes(info.Attributes | FileAttributes.Hidden);
    }

    public static void ThrowIfNameIsInvalid(this NodeInfo<long> info)
    {
        info.GetNameAndThrowIfInvalid();
    }

    public static string GetNameAndThrowIfInvalid(this NodeInfo<long> info)
    {
        var name = !string.IsNullOrEmpty(info.Name) ? info.Name : Path.GetFileName(info.Path);

        if (FileSystemObject.GetNameValidationResult(name) is { } message)
        {
            throw new FileSystemClientException<long>(message, FileSystemErrorCode.InvalidName, default);
        }

        return name;
    }
}
