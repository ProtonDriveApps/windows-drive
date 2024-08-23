using System;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record PublicKeyListResponse : ApiResponse
{
    public PublicKeyList Address { get; set; } = new();

    [JsonPropertyName("CatchAll")]
    public PublicKeyList? CatchAllAddress { get; set; }

    [JsonPropertyName("Unverified")]
    public PublicKeyList? UnverifiedAddress { get; set; }

    public string[] Warnings { get; set; } = Array.Empty<string>();

    [JsonPropertyName("ProtonMX")]
    public bool IsProtonMxDomain { get; set; }

    [JsonPropertyName("IsProton")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool IsOfficialProtonAddress { get; set; }
}
