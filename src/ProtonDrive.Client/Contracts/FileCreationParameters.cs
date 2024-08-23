using System;
using System.Text.Json.Serialization;
using ProtonDrive.Shared.Text.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class FileCreationParameters : NodeCreationParameters
{
    [JsonPropertyName("MIMEType")]
    public string? MediaType { get; set; }

    [JsonConverter(typeof(Base64JsonConverter))]
    public ReadOnlyMemory<byte> ContentKeyPacket { get; set; }

    public string? ContentKeyPacketSignature { get; set; }

    [JsonPropertyName("ClientUID")]
    public string ClientId { get; set; } = string.Empty;
}
