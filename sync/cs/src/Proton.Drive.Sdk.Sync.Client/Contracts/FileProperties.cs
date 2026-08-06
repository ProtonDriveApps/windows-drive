using System.Text.Json.Serialization;
using Proton.Drive.Shared.Text.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed class FileProperties
{
    [JsonConverter(typeof(Base64JsonConverter))]
    public ReadOnlyMemory<byte> ContentKeyPacket { get; init; }

    public string? ContentKeyPacketSignature { get; init; }

    public RevisionHeader? ActiveRevision { get; init; }
}
