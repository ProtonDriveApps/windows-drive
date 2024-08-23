using System;
using System.Text.Json.Serialization;
using ProtonDrive.BlockVerification;
using ProtonDrive.Shared.Text.Serialization;

namespace ProtonDrive.Client.Contracts;

internal sealed class BlockCreationParameters
{
    public BlockCreationParameters(int index, int size, string encryptedSignature, ReadOnlyMemory<byte> hash, VerificationToken? verificationToken)
    {
        Index = index;
        Size = size;
        EncryptedSignature = encryptedSignature;
        Hash = hash;
        VerifierOutput = verificationToken is not null ? new(verificationToken.Value.AsReadOnlyMemory()) : default;
    }

    public int Index { get; }
    public int Size { get; }

    [JsonPropertyName("EncSignature")]
    public string EncryptedSignature { get; }

    [JsonConverter(typeof(Base64JsonConverter))]
    public ReadOnlyMemory<byte> Hash { get; }

    [JsonPropertyName("Verifier")]
    public BlockVerifierOutput? VerifierOutput { get; }
}
