using System.Text.Json.Serialization;
using Proton.Drive.Sdk.Sync.Client.Photos.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed class RevisionUpdateParameters
{
    public RevisionUpdateParameters(
        string manifestSignature,
        string signatureEmailAddress,
        string? extendedAttributes)
    {
        ManifestSignature = manifestSignature;
        SignatureEmailAddress = signatureEmailAddress;
        ExtendedAttributes = extendedAttributes;
    }

    public string ManifestSignature { get; }

    [JsonPropertyName("SignatureAddress")]
    public string SignatureEmailAddress { get; }

    [JsonPropertyName("XAttr")]
    public string? ExtendedAttributes { get; }

    [JsonPropertyName("Photo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PhotoRevisionDetails? PhotoDetails { get; set; }
}
