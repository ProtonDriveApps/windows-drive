using Proton.Drive.Shared;

namespace Proton.Drive.Sdk.Sync.Agent.Volumes;

public sealed record VolumeInfo
{
    public VolumeInfo(string id, string rootShareId, string rootLinkId)
    {
        Ensure.NotNullOrEmpty(id, nameof(id));
        Ensure.NotNullOrEmpty(rootShareId, nameof(rootShareId));
        Ensure.NotNullOrEmpty(rootLinkId, nameof(rootLinkId));

        Id = id;
        RootShareId = rootShareId;
        RootLinkId = rootLinkId;
    }

    public string Id { get; }
    public string RootShareId { get; }
    public string RootLinkId { get; }
}
