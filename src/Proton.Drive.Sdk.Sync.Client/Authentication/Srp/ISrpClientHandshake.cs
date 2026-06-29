namespace Proton.Drive.Sdk.Sync.Client.Authentication.Srp;

internal interface ISrpClientHandshake
{
    byte[] Ephemeral { get; }
    byte[] Proof { get; }

    bool TryComputeSharedKey(ReadOnlySpan<byte> serverProof);
}
