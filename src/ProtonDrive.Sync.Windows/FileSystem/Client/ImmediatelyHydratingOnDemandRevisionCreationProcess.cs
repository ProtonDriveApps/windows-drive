using System;
using System.IO;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;
using Vanara.PInvoke;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal sealed class ImmediatelyHydratingOnDemandRevisionCreationProcess : ClassicRevisionCreationProcess
{
    public ImmediatelyHydratingOnDemandRevisionCreationProcess(
        FileSystemFile file,
        NodeInfo<long> initialInfo,
        NodeInfo<long> fileInfo,
        NodeInfo<long> finalInfo,
        Action<Progress>? progressCallback)
        : base(file, initialInfo, fileInfo, finalInfo, progressCallback)
    {
    }

    protected override void OnReplacingOriginalFile(FileSystemFile originalFile, FileSystemFile tempFile)
    {
        using var file = tempFile.ReOpen(FileSystemFileAccess.ReadAttributes, FileShare.ReadWrite | FileShare.Delete);

        using var placeholderCreationInfo = NodeInfo<long>.File().ToPlaceholderCreationInfo();

        file.ConvertToPlaceholder(placeholderCreationInfo.Value, CldApi.CF_CONVERT_FLAGS.CF_CONVERT_FLAG_MARK_IN_SYNC);

        if (originalFile.Attributes.IsPinned())
        {
            file.SetPinState(CldApi.CF_PIN_STATE.CF_PIN_STATE_PINNED, CldApi.CF_SET_PIN_FLAGS.CF_SET_PIN_FLAG_NONE);
        }
        else if (originalFile.Attributes.IsDehydrationRequested())
        {
            // The placeholder file was marked for freeing space while the revision was being downloaded.
            // We preserve the un-pinned flag for the file to be automatically dehydrated later.
            file.SetPinState(CldApi.CF_PIN_STATE.CF_PIN_STATE_UNPINNED, CldApi.CF_SET_PIN_FLAGS.CF_SET_PIN_FLAG_NONE);
        }
    }
}
