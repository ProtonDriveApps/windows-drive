using ProtonDrive.Sync.Adapter.Trees.Adapter;

namespace ProtonDrive.App.Sanitization;

internal sealed record FileSanitizationJob(string VolumeId, string LinkId)
{
    public AdapterTreeNodeModel<long, string>? RemoteNode { get; set; }

    public bool IsFinished { get; set; }
}
