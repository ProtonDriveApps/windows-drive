using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Metrics;
using Vanara.PInvoke;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client;

internal sealed class ImmediatelyHydratingOnDemandRevisionCreationProcess : ClassicRevisionCreationProcess
{
    public ImmediatelyHydratingOnDemandRevisionCreationProcess(
        FileSystemFile file,
        NodeInfo<long> initialInfo,
        NodeInfo<long> fileInfo,
        NodeInfo<long> finalInfo,
        bool checksumVerificationEnabled,
        Action<Progress>? progressCallback,
        Action<MetricEvent> recordMetricEvent)
        : base(file, initialInfo, fileInfo, finalInfo, checksumVerificationEnabled, progressCallback, recordMetricEvent)
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
