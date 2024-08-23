using System;
using System.IO;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Windows.FileSystem.Client;
using static Vanara.PInvoke.CldApi;

namespace ProtonDrive.Sync.Windows.FileSystem.CloudFiles;

public static class FileSystemObjectCloudFilesExtensions
{
    public static PlaceholderState GetPlaceholderState(this FileSystemObject fsObject)
    {
        var attributeTagInfo = Internal.FileSystem.GetFileAttributeTagInfo(fsObject.FileHandle);
        return Internal.FileSystem.GetPlaceholderState((FileAttributes)attributeTagInfo.FileAttributes, attributeTagInfo.ReparseTag);
    }

    public static void SetPinState(this FileSystemObject fsObject, CF_PIN_STATE state, CF_SET_PIN_FLAGS flags)
    {
        CfSetPinState(fsObject.FileHandle, state, flags).ThrowExceptionForHR();
    }

    public static void RevertPlaceholder(this FileSystemObject fsObject)
    {
        CfRevertPlaceholder(fsObject.FileHandle, CF_REVERT_FLAGS.CF_REVERT_FLAG_NONE, IntPtr.Zero).ThrowExceptionForHR();
    }

    public static void ConvertToPlaceholder(this FileSystemObject fsObject, CF_PLACEHOLDER_CREATE_INFO fileIdentityInfo, CF_CONVERT_FLAGS flags)
    {
        CfConvertToPlaceholder(
                fsObject.FileHandle,
                fileIdentityInfo.FileIdentity,
                fileIdentityInfo.FileIdentityLength,
                flags,
                out _)
            .ThrowExceptionForHR();
    }
}
