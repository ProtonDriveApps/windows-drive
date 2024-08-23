using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

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
}
