using System;
using System.Text.Json.Serialization;
using ProtonDrive.Shared.Text.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class FileProperties
{
    [JsonConverter(typeof(Base64JsonConverter))]
    public ReadOnlyMemory<byte> ContentKeyPacket { get; set; }

    public string? ContentKeyPacketSignature { get; set; }

    public RevisionHeader? ActiveRevision { get; set; }
}
