using System.Text.Json.Serialization;
using Proton.Drive.Sdk.Sync.Client.Contracts;

namespace Proton.Drive.Sdk.Sync.Agent.Settings;

public sealed class RemoteReplica
{
    public string? VolumeId { get; set; }
    public string? ShareId { get; set; }
    public string? RootLinkId { get; set; }

    [JsonPropertyName("RootFolderName")]
    public string? RootItemName { get; set; }

    /// <summary>
    /// Automatically generated volume identity for internal use.
    /// </summary>
    public int InternalVolumeId { get; set; }

    public bool IsReadOnly { get; set; }
    public LinkType RootItemType { get; set; } = LinkType.Folder;
}
