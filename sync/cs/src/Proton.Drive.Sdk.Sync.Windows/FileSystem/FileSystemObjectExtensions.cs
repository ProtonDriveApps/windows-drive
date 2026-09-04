using System.ComponentModel;
using System.Text;
using Proton.Drive.Sdk.Sync.Windows.Interop;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem;

public static class FileSystemObjectExtensions
{
    private const int PathMaxLength = 260;

    public static LocalVolumeInfo GetVolumeInfo(this FileSystemObject fileSystemObject)
    {
        var volumeNameBuffer = new StringBuilder(PathMaxLength);
        var fileSystemNameBuffer = new StringBuilder(PathMaxLength);

        if (!Kernel32.GetVolumeInformationByHandle(
                fileSystemObject.FileHandle,
                volumeNameBuffer,
                volumeNameBuffer.Capacity,
                out var volumeSerialNumber,
                out var maximumComponentLength,
                out var fileSystemFlags,
                fileSystemNameBuffer,
                fileSystemNameBuffer.Capacity))
        {
            // Automatically gets the last Win32 error code and description
            throw new Win32Exception();
        }

        return new LocalVolumeInfo
        {
            VolumeName = volumeNameBuffer.ToString(),
            VolumeSerialNumber = unchecked((int)volumeSerialNumber),
            FileSystemName = fileSystemNameBuffer.ToString(),
            MaximumComponentLength = (int)maximumComponentLength,
            Attributes = (FileSystemAttributes)fileSystemFlags,
        };
    }

    public static void VerifyOpenByFileIdSupport(this FileSystemObject fileSystemObject)
    {
        using var fileSystemObjectById = FileSystemObject.OpenById(
            fileSystemObject.ObjectId,
            fileSystemObject,
            FileSystemFileAccess.ReadAttributes,
            FileShare.ReadWrite | FileShare.Delete,
            FileOptions.None);
    }
}
