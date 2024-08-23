using System;
using System.Text.Json.Serialization;
using ProtonDrive.Shared.Text.Serialization;

namespace ProtonDrive.Client.Contracts;

public struct BlockVerifierOutput
{
    public BlockVerifierOutput(ReadOnlyMemory<byte> token)
    {
        Token = token;
    }

    [JsonConverter(typeof(Base64JsonConverter))]
    public ReadOnlyMemory<byte> Token { get; }
}
